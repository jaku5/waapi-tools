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
        static void Main(string[] args)
        {
            _Main().Wait();
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
                    DisplayActorsToConvert(actorsToConvert);

                    Console.WriteLine("\nWould you like to convert these actor-mixers to virtual folders? (y/n)");
                    if (Console.ReadLine()?.ToLower() == "y")
                    {
                        await ConvertActorsToFoldersAsync(client, actorsToConvert);
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
            var query = new JObject(new JProperty("waql", "$ from type actormixer where ancestors.any(type = \"actormixer\")"));
            var options = new JObject(new JProperty("return", new string[] { "name", "id", "path", "parent.id" }));

            JObject result = await client.Call(ak.wwise.core.@object.get, query, options);

            return result["return"] as JArray;
        }

        private static async Task<JArray> ProcessActorsAsync(JsonClient client, JArray actors)
        {
            var actorsToConvert = new JArray();

            foreach (var actor in actors)
            {
                // Get actor's first actor-mixer type ancestor
                var ancestorsQuery = new JObject(
                    new JProperty("waql", $"$ \"{actor["id"]}\" select ancestors.first(type = \"actormixer\")"));

                var ancestorsOptions = new JObject(
                    new JProperty("return", new string[] { "id", "name" }));

                JObject ancestorsResult = await client.Call(ak.wwise.core.@object.get, ancestorsQuery, ancestorsOptions);

                var ancestorsArray = ancestorsResult["return"] as JArray;

                var ancestor = ancestorsArray[0];

                // Check for differences between the actor and its ancestor
                Console.WriteLine($"Processing: {actor["name"]} (ID: {actor["id"]})");

                JObject diff = await client.Call(ak.wwise.core.@object.diff, new JObject(
                                                    new JProperty("source", actor["id"]),
                                                    new JProperty("target", ancestor["id"])));

                // Check for states differences
                var stateQuery = new JObject(
                    new JProperty("waql", $"$ \"{actor["id"]}\""));

                var stateOptions = new JObject(
                   new JProperty("return", new string[] { "stateGroups" }));

                JObject stateResult = await client.Call(ak.wwise.core.@object.get, stateQuery, stateOptions);

                var ancestorStateQuery = new JObject(
                    new JProperty("waql", $"$ \"{ancestor["id"]}\""));

                var ancestorStateOptions = new JObject(
                   new JProperty("return", new string[] { "stateGroups" }));

                JObject ancestorStateResult = await client.Call(ak.wwise.core.@object.get, ancestorStateQuery, ancestorStateOptions);

                // Check if the actor is referenced by an event action
                var referenceQuery = new JObject(
                    new JProperty("waql", $"$ \"{actor["id"]}\" select referencesTo where type:\"action\""));

                var referenceOptions = new JObject(
                    new JProperty("return", new string[] { "id" }));

                JObject referenceResult = await client.Call(ak.wwise.core.@object.get, referenceQuery, referenceOptions);

                // Additional check on actor for rtpc presence
                var rtpcQuery = new JObject(
                    new JProperty("waql", $"$ \"{actor["id"]}\" where rtpc.any()"));

                var rtpcOptions = new JObject(
                    new JProperty("return", new string[] { "id" }));

                JObject rtpcResult = await client.Call(ak.wwise.core.@object.get, rtpcQuery);

                // Additional check on actor for state presence
                var statePresenceQuery = new JObject(
                    new JProperty("waql", $"$ \"{actor["id"]}\" where stateGroups.any()"));

                var statePresenceOptions = new JObject(
                    new JProperty("return", new string[] { "id" }));

                JObject statePresenceResult = await client.Call(ak.wwise.core.@object.get, statePresenceQuery);

                // Create a list of actors to convert 
                bool hasNoDiffProperties = diff["properties"] is JArray diffPropertiesArray && !diffPropertiesArray.Any();
                bool hasNoDiffLists = diff["lists"] is JArray diffListsArray && !diffListsArray.Any(item => item.ToString().Contains("RTPC")) || rtpcResult["return"] is JArray rtpcResultArray && !rtpcResultArray.Any();
                bool hasNoReferences = referenceResult["return"] is JArray referenceResultArray && !referenceResultArray.Any();
                bool hasNoStateDifferences = stateResult["return"].ToString() == ancestorStateResult["return"].ToString();
                bool hasNoState = statePresenceResult["return"] is JArray statePresenceResultArray && !statePresenceResultArray.Any();

                if (hasNoDiffProperties && hasNoDiffLists && hasNoReferences && (hasNoStateDifferences || hasNoState))
                {
                    actorsToConvert.Add(new JObject(
                        new JProperty("id", actor["id"]),
                        new JProperty("name", actor["name"]),
                        new JProperty("path", actor["path"]),
                        new JProperty("ancestor.id", ancestor["id"]),
                        new JProperty("ancestor.name", ancestor["name"]),
                        new JProperty("parent.id", actor["parent.id"]),
                        new JProperty("hasNoState", hasNoState)));
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
                    new JProperty("waql", $"$ {actorPath} select children"));

                JObject resultChildren = await client.Call(ak.wwise.core.@object.get, queryChildren);

                // Copy the children to the new folder
                if (resultChildren["return"] is JArray resultsArrayChildren && resultsArrayChildren.Any())
                {
                    foreach (var child in resultChildren["return"])
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
                    string note = actor["hasNoState"]?.ToObject<bool>() == false
                        ? " (Note: This actor has state group.)"
                        : string.Empty;

                    Console.WriteLine($"- {actor["name"]} (ID: {actor["id"]}){note}");
                }
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
    }
}
