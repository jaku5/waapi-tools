/******************************************************************************

The content of this file includes portions of the AUDIOKINETIC Wwise Technology
released in source code form as part of the SDK installer package.

Commercial License Usage

Licensees holding valid commercial licenses to the AUDIOKINETIC Wwise Technology
may use this file in accordance with the end user license agreement provided 
with the software or, alternatively, in accordance with the terms contained in a
written agreement between you and Audiokinetic Inc.

Apache License Usage

Alternatively, this file may be used under the Apache License, Version 2.0 (the 
"Apache License"); you may not use this file except in compliance with the 
Apache License. You may obtain a copy of the Apache License at 
http://www.apache.org/licenses/LICENSE-2.0.

Unless required by applicable law or agreed to in writing, software distributed
under the Apache License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES
OR CONDITIONS OF ANY KIND, either express or implied. See the Apache License for
the specific language governing permissions and limitations under the License.

  Copyright (c) 2020 Audiokinetic Inc.

*******************************************************************************/

using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using JPAudio.WaapiTools.ClientJson;

namespace JPAudio.WaapiTools.Tool.ActormixerSanitizer
{
    class Program
    {
        private const string WaqlKey = "waql";
        private const string ReturnKey = "return";
        private const string ObjectGetUri = ak.wwise.core.@object.get;
        static async Task Main(string[] args)
        {
            await _Main();
        }

        static async Task _Main()
        {
            try
            {
                JsonClient client = await InitializeClientAsync();
                JArray actors = await GetActorMixersAsync(client);
                
                if (actors != null && actors.Any())
                {
                    JArray actorsToConvert = await ProcessActorsAsync(client, actors);

                    if (actorsToConvert.Any())
                    {
                       await client.Call(ak.wwise.core.undo.beginGroup);
                       await RemoveActorsWithActiveStates(client, actorsToConvert);
                       await client.Call(ak.wwise.core.undo.endGroup, new JObject(
                                           new JProperty("displayName", "Create And Remove Temporary Query")));
                    }

                    DisplayActorsToConvert(actorsToConvert);

                    Console.WriteLine("\nWould you like to convert these actor-mixers to virtual folders? (y/n)");
                    if (Console.ReadLine()?.ToLower() == "y")
                    {
                        await client.Call(ak.wwise.core.undo.beginGroup);
                        await ConvertActorsToFoldersAsync(client, actorsToConvert);
                        await client.Call(ak.wwise.core.undo.endGroup, new JObject(
                                            new JProperty("displayName", "Sanitize Actor-Mixers")));
                    }
                    else
                    {
                        Console.WriteLine("User cancelled.");
                    }
                }
                else
                {
                    PrintNoCandidatesMessage();
                }

                ExitProgram();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
            }
        }

        private static async Task<JsonClient> InitializeClientAsync()
        {
            JsonClient client = new JsonClient();
            await client.Connect();
            client.Disconnected += () => Console.WriteLine("We lost connection!");
            return client;
        }

        private static async Task<JArray> GetActorMixersAsync(JsonClient client)
        {
            JObject result = await QueryWaapiAsync(client, "$ from type actormixer where ancestors.any(type = \"actormixer\")", new[] { "name", "id", "path", "parent.id" });

            return result[ReturnKey] as JArray;
        }

        private static async Task<JArray> ProcessActorsAsync(JsonClient client, JArray actors)
        {
            var actorsToConvert = new JArray();

            foreach (var actor in actors)
            {
                // Get actor's first actor-mixer type ancestor
                // TODO: Check if can be simplified
                JObject ancestorsResult = await QueryWaapiAsync(client, $"$ \"{actor["id"]}\" select ancestors.first(type = \"actormixer\")", new string[] { "id", "name" });

                var ancestorsArray = ancestorsResult[ReturnKey] as JArray;

                var ancestor = ancestorsArray[0];

                // Check for differences between the actor and its ancestor
                Console.WriteLine($"Processing: {actor["name"]} (ID: {actor["id"]})");

                JObject diff = await client.Call(ak.wwise.core.@object.diff, new JObject(
                                                    new JProperty("source", actor["id"]),
                                                    new JProperty("target", ancestor["id"])));
                
                // Check if the actor is referenced by an event action
                JObject referenceResult = await QueryWaapiAsync(client, $"$ \"{actor["id"]}\" select referencesTo where type:\"action\"", new string[] { "id" });

                // Additional check on actor for rtpc presence
                JObject rtpcResult = await QueryWaapiAsync(client, $"$ \"{actor["id"]}\" where rtpc.any()", new string[] { "id" });

                // Create a list of actors to convert 
                bool hasNoDiffProperties = diff["properties"] is JArray diffPropertiesArray && !diffPropertiesArray.Any();
                bool hasNoDiffLists = diff["lists"] is JArray diffListsArray && !diffListsArray.Any(item => item.ToString().Contains("RTPC")) || rtpcResult[ReturnKey] is JArray rtpcResultArray && !rtpcResultArray.Any();
                bool hasNoReferences = referenceResult[ReturnKey] is JArray referenceResultArray && !referenceResultArray.Any();

                if (hasNoDiffProperties && hasNoDiffLists && hasNoReferences)
                {
                    actorsToConvert.Add(new JObject(
                        new JProperty("id", actor["id"]),
                        new JProperty("name", actor["name"]),
                        new JProperty("path", actor["path"]),
                        new JProperty("ancestor.id", ancestor["id"]),
                        new JProperty("ancestor.name", ancestor["name"]),
                        new JProperty("parent.id", actor["parent.id"])));
                }
            }

            return actorsToConvert;
        }

        private static async Task ConvertActorsToFoldersAsync(JsonClient client, JArray actorsToConvert)
        {
            // Create a folder for each actor
            foreach (var actor in actorsToConvert)
            {
                var tempName = $"{actor["name"]}Temp";

                Console.WriteLine($"\nConverting: {actor["name"]} (ID: {actor["id"]})");

                await client.Call(ak.wwise.core.@object.create, new JObject(
                    new JProperty("parent", actor["parent.id"]),
                    new JProperty("type", "Folder"),
                    new JProperty("name", tempName)));
            }

            // Get children of the actor
            var childrenToMove = new JArray();

            foreach (var actor in actorsToConvert)
            {
                var actorPath = $"\"{actor["path"].ToString().Replace("\\\\", "\\")}\"";
                var folderPath = $"{actor["path"].ToString().Replace("\\\\", "\\")}Temp";

                Console.WriteLine($"\nMoving children of: {actor["name"]} (ID: {actor["id"]})");

                var queryChildren = new JObject(
                    new JProperty(WaqlKey, $"$ {actorPath} select children"));

                JObject resultChildren = await client.Call(ObjectGetUri, queryChildren);

                // Copy the children to the new folder
                if (resultChildren[ReturnKey] is JArray resultsArrayChildren && resultsArrayChildren.Any())
                {
                    foreach (var child in resultChildren[ReturnKey])
                    {
                        Console.WriteLine($"\nMoving: {child["name"]} (ID: {child["id"]})");

                        await client.Call(ak.wwise.core.@object.move, new JObject(
                            new JProperty("object", child["id"]),
                            new JProperty("parent", folderPath)));
                    }
                }
                // Delete the actor
                Console.WriteLine($"\nDeleting: {actor["name"]} (ID: {actor["id"]}");

                await client.Call(ak.wwise.core.@object.delete, new JObject(
                     new JProperty("object", actor["id"])));

                // Rename the folder
                await client.Call(ak.wwise.core.@object.setName, new JObject(
                    new JProperty("object", folderPath),
                    new JProperty("value", actor["name"])));
            }
        }

        private static void DisplayActorsToConvert(JArray actorsToConvert)
        {
            Console.Clear();

            if (!actorsToConvert.Any())
            {
                PrintNoCandidatesMessage();
                ExitProgram();
            }

            else
            {
                Console.WriteLine("The following actor-mixers can be converted to virtual folders:\n");
                foreach (var actor in actorsToConvert)
                {
                    Console.WriteLine($"- {actor["name"]} (ID: {actor["id"]})");
                }
            }
        }

        private static async Task RemoveActorsWithActiveStates(JsonClient client, JArray actorsToConvert)
        {
            // Check if any state is present in the actor-mixers and create a temporary query if so
            JObject stateReference = await QueryWaapiAsync(client, "$ from type stateGroup where referencesTo.any(type = \"actormixer\" and ancestors.any(type = \"actormixer\")) select children where name != \"None\"", ["id"]);

            if (stateReference[ReturnKey].Any())
            {
                JObject stateQuery = await CreateTemporaryQuery(client);
                await CreateTemporarySearchCriteria(client, stateReference, stateQuery["id"].ToString());

                // Remove any actor-mixers with active states from the list of actors to convert
                JObject activeStates = await QueryWaapiAsync(client, $"$ from query \"{stateQuery["id"]}\"", ["id"]);
                foreach (JToken actor in activeStates[ReturnKey])
                {
                    var actorToRemove = actorsToConvert
                        .FirstOrDefault(a => a["id"]?.ToString() == actor["id"]?.ToString());

                    if (actorToRemove != null)
                    {
                        actorsToConvert.Remove(actorToRemove);
                    }
                }

                // Delete the temporary query
                await client.Call(ak.wwise.core.@object.delete, new JObject(
                                    new JProperty("object", stateQuery["id"].ToString())));
            }
        }

        private static async Task<JObject> CreateTemporaryQuery(JsonClient client, string actorsToQuery = "$ from type actormixer where ancestors.any(type = \"actormixer\")")
        {
            return await client.Call(ak.wwise.core.@object.create, new JObject(
                new JProperty("parent", "\\Queries\\Default Work Unit"),
                new JProperty("type", "Query"),
                new JProperty("name", "TemporaryStateQuery"),
                new JProperty("@LogicalOperator", "1"),
                new JProperty("@ObjectType", "10"),
                new JProperty("@WAQL", actorsToQuery)));
        }

        private static async Task CreateTemporarySearchCriteria(JsonClient client, JObject stateReference, string parentQueryId)
        {
            foreach (JToken result in stateReference[ReturnKey])
            {
                var stateId = result["id"];

                await client.Call(ak.wwise.core.@object.create, new JObject(
                new JProperty("parent", parentQueryId),
                new JProperty("type", "SearchCriteria"),
                new JProperty("name", "{E2BA49FE-AADD-4726-A72E-9958C70A9F19}"),
                new JProperty("@State", stateId),
                new JProperty("@StatePropertyUsage", "2")));
            }
        }

        private static void ExitProgram()
        {
            Console.WriteLine("\nDone. Press any key to exit.");
            Console.ReadLine();

            Environment.Exit(0);
        }

        private static void PrintNoCandidatesMessage()
        {
            Console.Clear();
            Console.WriteLine("No actor-mixers to sanitize found. Good job!");
        }
        private static async Task<JObject> QueryWaapiAsync(JsonClient client, string waql, string[] returnFields)
        {
            var query = new JObject(new JProperty(WaqlKey, waql));
            var options = new JObject(new JProperty(ReturnKey, returnFields));

            return await client.Call(ObjectGetUri, query, options);
        }
    }
}
