using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using JPAudio.WaapiTools.ClientJson;
using System.Collections.Generic;

namespace JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core
{
    public class ActormixerSanitizerService
    {

        private const string WaqlKey = "waql";
        private const string ReturnKey = "return";
        private const string ObjectGetUri = ak.wwise.core.@object.get;
        private const string ActorCandidatesQuery = "$ from type actormixer where ancestors.any(type = \"actormixer\")";

        private readonly static List<string> unityProperties = new List<string> { "Volume", "Pitch", "Lowpass", "Highpass", "MakeUpGain" };

        static async Task Main(string[] args)
        {
            await _Main();
        }

        static async Task _Main()
        {
            try
            {
                JsonClient client = await InitializeClientAsync();

                var actorQuery = BuildQueryStrings(ActorCandidatesQuery, unityProperties);

                JArray actors = await GetActorMixersAsync(client, actorQuery.Item1, actorQuery.Item2);

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

        private static (string, string[]) BuildQueryStrings(string actorQuery, List<string> unityProperties)
        {
            string query = actorQuery;
            string[] returnOptions = { "name", "id", "path", "parent.id", "notes" };

            foreach (var property in unityProperties)
            {
                query = query + ($" and (randomizer(\"{property.ToLower()}\") = null or randomizer(\"{property.ToLower()}\").enabled = false or (randomizer(\"{property.ToLower()}\").min = 0 and randomizer(\"{property.ToLower()}\").max = 0))");
                returnOptions = returnOptions.Append(property).ToArray();
            }

            return (query, returnOptions);
        }

        private static async Task<JsonClient> InitializeClientAsync()
        {
            JsonClient client = new JsonClient();
            await client.Connect();
            client.Disconnected += () => Console.WriteLine("We lost connection!");
            return client;
        }

        private static async Task<JArray> GetActorMixersAsync(JsonClient client, string query, string[] returnOptions)
        {
            JObject result = await QueryWaapiAsync(client, query, returnOptions);

            return result[ReturnKey] as JArray;
        }

        private static async Task<JArray> ProcessActorsAsync(JsonClient client, JArray actors)
        {
            var actorsToConvert = new JArray();

            foreach (var actor in actors)
            {
                var propertiesToCheck = new List<string>();

                bool hasDiffProperties = true;
                bool hasDiffUnityProperties = true;

                int unityPropertiesDiffCount = 0;

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

                if (diff["properties"] is JArray diffArray && diffArray.Any())
                {
                    foreach (var result in diff["properties"])
                    {
                        var propertyName = result;
                        propertiesToCheck.Add(propertyName.ToString());

                        foreach (var property in unityProperties)
                        {
                            propertiesToCheck.Remove(property.ToString());
                        }
                    }

                    if (!propertiesToCheck.Any())
                        hasDiffProperties = false;
                }

                else
                {
                    hasDiffProperties = false;
                }

                // Check if the value of unity properties is 0 and randomize is not in use
                foreach (var property in unityProperties)
                {
                    if (actor[$"{property}"].ToString() != "0")
                        unityPropertiesDiffCount++;
                }

                if (unityPropertiesDiffCount == 0)
                    hasDiffUnityProperties = false;

                // Check the actor for active rtpc presence
                JObject rtpcResult = await QueryWaapiAsync(client, $"$ \"{actor["id"]}\" where rtpc.any(@ControlInput.any())", new string[] { "id" });
                bool hasActiveRtpcs = rtpcResult[ReturnKey] is JArray rtpcResultArray && rtpcResultArray.Any();

                // Check if the actor is referenced by an event action
                JObject referenceResult = await QueryWaapiAsync(client, $"$ \"{actor["id"]}\" select referencesTo where type:\"action\"", new string[] { "id" });
                bool hasReferences = referenceResult[ReturnKey] is JArray referenceResultArray && referenceResultArray.Any();

                // Create a list of actors to convert 
                if (!hasDiffProperties && !hasDiffUnityProperties && !hasActiveRtpcs && !hasReferences)
                {
                    actorsToConvert.Add(new JObject(
                        new JProperty("id", actor["id"]),
                        new JProperty("name", actor["name"]),
                        new JProperty("path", actor["path"]),
                        new JProperty("ancestor.id", ancestor["id"]),
                        new JProperty("ancestor.name", ancestor["name"]),
                        new JProperty("parent.id", actor["parent.id"]),
                        new JProperty("notes", actor["notes"])));
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
                    new JProperty("name", tempName),
                    new JProperty("notes", actor["notes"])));
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
            // TODO: Improve waql query to check for active states only in the actor-mixer without unity properties diff
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
