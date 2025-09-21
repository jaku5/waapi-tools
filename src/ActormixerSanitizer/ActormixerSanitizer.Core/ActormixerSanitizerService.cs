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
        private readonly JsonClient _client;
        private const string WaqlKey = "waql";
        private const string ReturnKey = "return";
        private const string ObjectGetUri = ak.wwise.core.@object.get;
        private const string ActorCandidatesQuery = "$ from type actormixer where ancestors.any(type = \"actormixer\")";
        private readonly static List<string> _unityProperties = new List<string> { "Volume", "Pitch", "Lowpass", "Highpass", "MakeUpGain" };

        public event EventHandler<string> LogMessage;
        public event EventHandler Disconnected;
        public event EventHandler ProjectStateChanged;

        private bool _isDirty = false;
        private List<int> _subscriptionIds = new List<int>();

        // Track only IDs we care about so we don't mark dirty for unrelated changes.
        private HashSet<string> _trackedActorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _trackedParentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public async Task SubscribeToChangesAsync(List<ActorMixerInfo> actors)
        {
                await UnsubscribeFromChangesAsync();

            // Populate tracked id sets based on the current scan results
            _trackedActorIds = new HashSet<string>(actors.Where(a => !string.IsNullOrEmpty(a.Id)).Select(a => a.Id), StringComparer.OrdinalIgnoreCase);
            _trackedParentIds = new HashSet<string>(actors.Where(a => !string.IsNullOrEmpty(a.ParentId)).Select(a => a.ParentId), StringComparer.OrdinalIgnoreCase);

            // Subscribe to name changes -> only interested if object is in our list
            var nameChangedId = await _client.Subscribe(ak.wwise.core.@object.nameChanged, null, OnNameChanged);
            _subscriptionIds.Add(nameChangedId);

            // Subscribe to postDeleted -> interested if deleted object is either a tracked actor or a parent of one
            var postDeletedId = await _client.Subscribe(ak.wwise.core.@object.postDeleted, null, OnObjectsPostDeleted);
            _subscriptionIds.Add(postDeletedId);

            foreach (var actor in actors)
            {
                foreach (var property in _unityProperties)
                {
                    var options = new JObject(
                        new JProperty("object", actor.Id),
                        new JProperty("property", property));
                    var propertyChangedId = await _client.Subscribe(ak.wwise.core.@object.propertyChanged, options, OnProjectStateChanged);
                    _subscriptionIds.Add(propertyChangedId);
                }
            }
        }

        // Keep original behavior for propertyChanged subscriptions and any other generic triggers
        private void OnProjectStateChanged(JObject json)
        {
            _isDirty = true;
            ProjectStateChanged?.Invoke(this, EventArgs.Empty);
        }

        // Handler for name changes: only mark dirty if the changed object is one we track.
        private void OnNameChanged(JObject json)
        {
            try
            {
                var ids = GetIdsFromPublish(json).ToList();
                if (ids.Any(id => _trackedActorIds.Contains(id)))
                {
                    _isDirty = true;
                    ProjectStateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"Error processing nameChanged event: {ex.Message}");
            }
        }

        // Handler for postDeleted: mark dirty only if deleted object is a tracked actor or a parent of a tracked actor.
        private void OnObjectsPostDeleted(JObject json)
        {
            try
            {
                var ids = GetIdsFromPublish(json).ToList();
                if (ids.Any(id => _trackedActorIds.Contains(id) || _trackedParentIds.Contains(id)))
                {
                    _isDirty = true;
                    ProjectStateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"Error processing postDeleted event: {ex.Message}");
            }
        }

        // Attempt to extract object id(s) from a publish message in a resilient way.
        private IEnumerable<string> GetIdsFromPublish(JObject json)
        {
            if (json == null) yield break;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void InspectToken(JToken token)
            {
                if (token == null) return;

                switch (token.Type)
                {
                    case JTokenType.String:
                    case JTokenType.Guid:
                        var s = token.ToString();
                        if (!string.IsNullOrWhiteSpace(s)) seen.Add(s);
                        break;

                    case JTokenType.Object:
                        // Prefer explicit "id" property
                        var idProp = token["id"];
                        if (idProp != null && (idProp.Type == JTokenType.String || idProp.Type == JTokenType.Guid))
                        {
                            seen.Add(idProp.ToString());
                        }
                        else
                        {
                            // Recursively collect any nested "id" properties
                            foreach (var t in token.SelectTokens("$..id"))
                            {
                                if (t != null && (t.Type == JTokenType.String || t.Type == JTokenType.Guid))
                                    seen.Add(t.ToString());
                            }
                        }
                        break;

                    case JTokenType.Array:
                        foreach (var child in token as JArray)
                            InspectToken(child);
                        break;

                    default:
                        // No-op for other token types
                        break;
                }
            }

            // Inspect common WAAPI publish shapes explicitly
            InspectToken(json["object"]);
            InspectToken(json["objects"]);
            InspectToken(json["ids"]);
            InspectToken(json["id"]);
            InspectToken(json[ReturnKey]);

            // Fallback: find any property named "id" anywhere in the payload
            foreach (var idToken in json.SelectTokens("$..id"))
            {
                if (idToken != null && (idToken.Type == JTokenType.String || idToken.Type == JTokenType.Guid))
                    seen.Add(idToken.ToString());
            }

            foreach (var id in seen)
                yield return id;
        }

        public async Task UnsubscribeFromChangesAsync()
        {
            foreach (var id in _subscriptionIds)
            {
                await _client.Unsubscribe(id);
            }
            _subscriptionIds.Clear();

            // clear tracked ids so future publishes are ignored until next SubscribeToChangesAsync
            _trackedActorIds.Clear();
            _trackedParentIds.Clear();
        }

        public ActormixerSanitizerService()
        {
            _client = new JsonClient();
            _client.Disconnected += () => Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public void Disconnect()
        {
            _subscriptionIds.Clear();
            _client.Disconnect();
        }

        public async Task ConnectAsync()
        {
            await _client.Connect();
        }

        public async Task<List<ActorMixerInfo>> GetSanitizableMixersAsync()
        {
            await _client.Call(ak.wwise.core.undo.beginGroup);

            var actorQuery = BuildQueryStrings(ActorCandidatesQuery, _unityProperties);
            var actors = await GetActorMixersAsync(_client, actorQuery.Item1, actorQuery.Item2);

            if (actors == null || !actors.Any())
                return new List<ActorMixerInfo>();

            var processedActors = await ProcessActorsAsync(_client, actors);
            await RemoveActorsWithActiveStates(_client, processedActors);

            await _client.Call(ak.wwise.core.undo.endGroup, new JObject(
                new JProperty("displayName", "Create and remove temp query")));

            return processedActors.Select(a => new ActorMixerInfo
            {
                Id = a["id"]?.ToString(),
                Name = a["name"]?.ToString(),
                Path = a["path"]?.ToString(),
                ParentId = a["parent.id"]?.ToString(),
                Notes = a["notes"]?.ToString(),
                AncestorId = a["ancestor.id"]?.ToString(),
                AncestorName = a["ancestor.name"]?.ToString()
            }).ToList();
        }

        public async Task ConvertToFoldersAsync(List<ActorMixerInfo> actors)
        {
            await _client.Call(ak.wwise.core.undo.beginGroup);

            foreach (var actor in actors)
            {
                LogMessage?.Invoke(this, $"Converting: {actor.Name}");
                var tempName = $"{actor.Name}Temp";

                await _client.Call(ak.wwise.core.@object.create, new JObject(
                    new JProperty("parent", actor.ParentId),
                    new JProperty("type", "Folder"),
                    new JProperty("name", tempName),
                    new JProperty("notes", actor.Notes)));

                var actorPath = $"\"{actor.Path.Replace("\\\\", "\\")}\"";
                var folderPath = $"{actor.Path.Replace("\\\\", "\\")}Temp";

                // Move children
                var childrenResult = await QueryWaapiAsync(_client, $"$ {actorPath} select children", new[] { "name", "id" });
                if (childrenResult[ReturnKey] is JArray children && children.Any())
                {
                    foreach (var child in children)
                    {
                        await _client.Call(ak.wwise.core.@object.move, new JObject(
                            new JProperty("object", child["id"]),
                            new JProperty("parent", folderPath)));
                    }
                }

                // Delete original actor and rename folder
                await _client.Call(ak.wwise.core.@object.delete, new JObject(
                    new JProperty("object", actor.Id)));

                await _client.Call(ak.wwise.core.@object.setName, new JObject(
                    new JProperty("object", folderPath),
                    new JProperty("value", actor.Name)));
            }

            await _client.Call(ak.wwise.core.undo.endGroup, new JObject(
                new JProperty("displayName", "Sanitize Actor-Mixers")));
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
                //LogMessage?.Invoke(client, $"Processing: {actor["name"]} (ID: {actor["id"]})");

                JObject diff = await client.Call(ak.wwise.core.@object.diff, new JObject(
                                                    new JProperty("source", actor["id"]),
                                                    new JProperty("target", ancestor["id"])));

                if (diff["properties"] is JArray diffArray && diffArray.Any())
                {
                    foreach (var result in diff["properties"])
                    {
                        var propertyName = result;
                        propertiesToCheck.Add(propertyName.ToString());

                        foreach (var property in _unityProperties)
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
                foreach (var property in _unityProperties)
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

        private static async Task<JObject> QueryWaapiAsync(JsonClient client, string waql, string[] returnFields)
        {
            var query = new JObject(new JProperty(WaqlKey, waql));
            var options = new JObject(new JProperty(ReturnKey, returnFields));

            return await client.Call(ObjectGetUri, query, options);
        }

        public async Task SelectInProjectExplorer(string actorId)
        {
            await _client.Call(ak.wwise.ui.commands.execute, new JObject(
                new JProperty("command", "FindInProjectExplorerSelectionChannel1"),
                new JProperty("objects", new JArray(actorId))));
        }

        public async Task ShowInListView(List<ActorMixerInfo> actors)
        {
            var actorsToShow = new JArray();

            foreach (var actor in actors)
            {
                actorsToShow.Add(actor.Id);
            }
            await _client.Call(ak.wwise.ui.commands.execute, new JObject(
            new JProperty("command", "ShowListView"),
            new JProperty("objects", new JArray(actorsToShow))));
        }
    }
}
