using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using JPAudio.WaapiTools.ClientJson;
using JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Models;

namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core
{
    /// <summary>
    /// Automates the community "jump to custom cue" technique to audition a single
    /// interactive-music transition in-editor, without playing through the material
    /// that precedes it.
    ///
    /// The operation is non-destructive: the production structure is copied into a
    /// throwaway Work Unit and all scaffolding is built around the copy. Teardown
    /// deletes that Work Unit, so the whole thing collapses to a single clean undo,
    /// and the project is never saved.
    /// </summary>
    public class TransitionAuditionerService : ITransitionAuditionerService
    {
        private readonly IJsonClient _client;
        private readonly ILogger<TransitionAuditionerService> _logger;

        private const string ReturnKey = "return";
        private const string WaqlKey = "waql";
        private const string InteractiveMusicHierarchy = "\\Interactive Music Hierarchy";
        private const string TempWorkUnitName = "TransitionAuditioner_Temp";
        private const string HarnessContainerName = "AuditionHarness";

        // @CueType value for a Custom cue (Entry = 0, Exit = 1, Custom = 2).
        private const int CustomCueType = 2;

        // How far before the segment's end to place the audition cue, so you can jump
        // straight to the run-up into the transition instead of playing the whole segment.
        // Hardcoded for the MVP; intended to become a UI setting later.
        public int AuditionCueOffsetFromEndMs { get; set; } = 1000;

        // Shared name for the custom cues we create and for the transition rule that matches them.
        private string AuditionCueName => $"Audition_End-{AuditionCueOffsetFromEndMs}ms";

        // Types that make sense as an audition target — they own (or contain) the
        // transitions we want to reach directly.
        private static readonly HashSet<string> AuditionableTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "MusicSwitchContainer",
            "MusicPlaylistContainer",
            "MusicSegment",
        };

        public event EventHandler<string>? StatusUpdated;
        public event EventHandler<string>? NotificationRequested;
        public event EventHandler? Disconnected;

        public bool IsConnected { get; private set; }
        public bool IsSetUp => Session != null;
        public string? ProjectName { get; private set; }
        public string? WwiseVersion { get; private set; }
        public AuditionSession? Session { get; private set; }

        public TransitionAuditionerService(IJsonClient client, ILogger<TransitionAuditionerService> logger)
        {
            _client = client;
            _client.Disconnected += () =>
            {
                IsConnected = false;
                Disconnected?.Invoke(this, EventArgs.Empty);
            };
            _logger = logger;
        }

        public async Task ConnectAsync()
        {
            await _client.Connect();
            IsConnected = true;

            try
            {
                var projectInfo = await _client.Call(ak.wwise.core.getProjectInfo);
                ProjectName = projectInfo?["name"]?.ToString();

                var info = await _client.Call(ak.wwise.core.getInfo);
                WwiseVersion = info?["version"]?["displayName"]?.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to fetch project info: {Message}", ex.Message);
            }
        }

        public void Disconnect()
        {
            IsConnected = false;
            _client.Disconnect();
        }

        public async Task<MusicObjectInfo?> GetSelectedTargetAsync()
        {
            var selection = await _client.Call(
                ak.wwise.ui.getSelectedObjects,
                null,
                new JObject(new JProperty(ReturnKey, new JArray("id", "name", "type", "path"))));

            if (selection?["objects"] is not JArray objects || objects.Count == 0)
            {
                Notify("Nothing is selected. Select an interactive-music container in the Project Explorer, then run the tool.");
                return null;
            }

            var match = objects.FirstOrDefault(o =>
                AuditionableTypes.Contains(o["type"]?.ToString() ?? string.Empty));

            if (match == null)
            {
                Notify("The selection is not an interactive-music object. Select a Music Switch Container, Music Playlist Container, or Music Segment.");
                return null;
            }

            return ToMusicObject(match);
        }

        public async Task<AuditionSession> SetUpAuditionAsync(MusicObjectInfo target, CancellationToken cancellationToken = default)
        {
            if (Session != null)
            {
                // Never stack scaffolding — clean up any previous audition first.
                await TeardownAsync();
            }

            var session = new AuditionSession { Target = target };

            // Group the whole build so that, combined with the temp Work Unit, the user
            // can also collapse it with a single Ctrl+Z even if teardown is skipped.
            await _client.Call(ak.wwise.core.undo.beginGroup);

            try
            {
                Status($"Creating temporary Work Unit for \"{target.Name}\"...");
                var workUnit = await _client.Call(ak.wwise.core.@object.create, new JObject(
                    new JProperty("parent", InteractiveMusicHierarchy),
                    new JProperty("type", "WorkUnit"),
                    new JProperty("name", TempWorkUnitName),
                    new JProperty("onNameConflict", "rename")));
                session.TempWorkUnitId = RequireId(workUnit, "temp Work Unit");
                cancellationToken.ThrowIfCancellationRequested();

                Status("Copying the target structure into the temporary Work Unit...");
                var copy = await _client.Call(ak.wwise.core.@object.copy, new JObject(
                    new JProperty("object", target.Id),
                    new JProperty("parent", session.TempWorkUnitId),
                    new JProperty("onNameConflict", "rename")));
                session.CopyId = RequireId(copy, "structure copy");
                cancellationToken.ThrowIfCancellationRequested();

                Status("Building the Music Switch Container harness...");
                var switchContainer = await _client.Call(ak.wwise.core.@object.create, new JObject(
                    new JProperty("parent", session.TempWorkUnitId),
                    new JProperty("type", "MusicSwitchContainer"),
                    new JProperty("name", HarnessContainerName),
                    new JProperty("onNameConflict", "rename")));
                session.SwitchContainerId = RequireId(switchContainer, "harness container");

                Status("Reparenting the copy under the harness...");
                await _client.Call(ak.wwise.core.@object.move, new JObject(
                    new JProperty("object", session.CopyId),
                    new JProperty("parent", session.SwitchContainerId),
                    new JProperty("onNameConflict", "rename")));
                cancellationToken.ThrowIfCancellationRequested();

                await CreateAuditionCuesAsync(session, cancellationToken);
                session.CustomCues = await GetCustomCuesAsync(session.CopyId);

                await AssignGenericPathAsync(session);
                await TryConfigureTransitionRuleAsync(session);

                Status("Creating a transport so you can audition...");
                var transport = await _client.Call(ak.wwise.core.transport.create, new JObject(
                    new JProperty("object", session.SwitchContainerId)));
                if (transport?["transport"] != null)
                {
                    session.TransportId = transport["transport"]!.Value<int>();
                }

                Session = session;
                Status("Ready. Press Play in the Wwise Transport Control to audition the transition.");
                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audition setup failed; rolling back.");
                Status("Setup failed — cleaning up.");
                // Roll back whatever was created so we never leave the project littered.
                await SafeTeardownAsync(session);
                throw;
            }
            finally
            {
                // End (but never cancel) the group: the create calls already happened, and
                // teardown's delete is what we rely on for cleanup, not group cancellation.
                await SafeCall(() => _client.Call(ak.wwise.core.undo.endGroup, new JObject(
                    new JProperty("displayName", "Set up transition audition"))));
            }
        }

        public async Task TeardownAsync()
        {
            if (Session == null)
                return;

            var session = Session;
            Session = null;
            await SafeTeardownAsync(session);
            Status("Cleaned up. The project was not modified or saved.");
        }

        public async Task ShowInProjectExplorerAsync()
        {
            if (Session?.SwitchContainerId is not { Length: > 0 } id)
                return;

            await SafeCall(() => _client.Call(ak.wwise.ui.commands.execute, new JObject(
                new JProperty("command", "FindInProjectExplorerSelectionChannel1"),
                new JProperty("objects", new JArray(id)))));
        }

        /// <summary>
        /// Configures the NONE -&gt; target transition rule that lands playback directly on
        /// the transition boundary (Jump to playlist item + custom-cue matching).
        ///
        /// The exact property/reference names for transition rules are Wwise-version
        /// specific and not all are exposed identically through WAAPI, so this is treated
        /// as best-effort: failure is logged and surfaced, but the harness is still left
        /// playable so the user can finish the rule by hand if needed.
        /// </summary>
        /// <summary>
        /// Assigns the copy as the harness container's "Generic Path" — what a user would otherwise
        /// have to drag in by hand for playback to work. A Music Switch Container plays an entry from
        /// its <c>Entries</c> list; with no switch/state groups assigned, a single MultiSwitchEntry
        /// whose <c>AudioNode</c> points at the copy is the generic (always-matched) path.
        /// </summary>
        private async Task AssignGenericPathAsync(AuditionSession session)
        {
            Status("Assigning the copy as the container's generic path...");
            try
            {
                // 'name' is a required create argument even though MultiSwitchEntry objects are
                // displayed unnamed — an empty string is what Wwise stores for them.
                var entry = await _client.Call(ak.wwise.core.@object.create, new JObject(
                    new JProperty("parent", session.SwitchContainerId),
                    new JProperty("type", "MultiSwitchEntry"),
                    new JProperty("list", "Entries"),
                    new JProperty("name", ""),
                    new JProperty("onNameConflict", "rename")));

                _logger.LogInformation("MultiSwitchEntry create result: {Result}", entry?.ToString());
                var entryId = RequireId(entry, "switch entry");

                await _client.Call(ak.wwise.core.@object.setReference, new JObject(
                    new JProperty("object", entryId),
                    new JProperty("reference", "AudioNode"),
                    new JProperty("value", session.CopyId)));

                Status("Generic path assigned.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not assign the generic path automatically.");
                Notify($"Generic path assignment failed: {DescribeError(ex)}");
                Notify("Playback needs the copy as a Generic Path — drag it into the harness's " +
                       "Music Switch tab by hand before pressing Play.");
            }
        }

        private async Task TryConfigureTransitionRuleAsync(AuditionSession session)
        {
            Status($"Configuring transition rule: None -> \"{session.Target.Name}\", Sync to Random Custom Cue (match \"{AuditionCueName}\")...");
            try
            {
                // Transitions aren't normal children of the container: the container references a
                // "Root" MusicTransition (TransitionRoot), and the actual rules are ChildrenList
                // children of that Root. A MusicTransition can't be created via WAAPI, but a fresh
                // container already ships with a default "(Any) to (Any)" rule — we just configure
                // that one. The MusicTransition model is identical across Wwise 2021–2025.
                var rootId = await FindTransitionRootAsync(session.SwitchContainerId);
                if (rootId == null)
                {
                    throw new InvalidOperationException(
                        "Could not find the container's Root MusicTransition.");
                }

                // The default "(Any) to (Any)" rule is read-only, so add a new rule as the last
                // (highest-priority) child of the Root and configure that.
                var transitionId = await CreateTransitionRuleAsync(rootId);

                // Source = None (Nothing); Destination = Object pointing at the copied structure.
                await SetTransitionProperty(transitionId, "SourceContextType", 1);
                await SetTransitionProperty(transitionId, "DestinationContextType", 2);
                // Destination Sync To = Random Custom Cue.
                await SetTransitionProperty(transitionId, "DestinationJumpPositionPreset", 3);
                // Custom Cue Filter = Match specific name = our audition cue.
                await SetTransitionProperty(transitionId, "JumpToCustomCueMatchMode", 1);
                await SetTransitionProperty(transitionId, "JumpToCustomCueMatchName", AuditionCueName);

                await _client.Call(ak.wwise.core.@object.setReference, new JObject(
                    new JProperty("object", transitionId),
                    new JProperty("reference", "DestinationContextObject"),
                    new JProperty("value", session.CopyId)));

                session.TransitionRuleConfigured = true;
                Status("Transition rule configured.");
            }
            catch (Exception ex)
            {
                session.TransitionRuleConfigured = false;
                _logger.LogWarning(ex, "Could not configure the transition rule automatically.");
                Notify($"Transition rule setup failed: {DescribeError(ex)}");
                Notify("The harness is still playable — set the None -> target transition " +
                       $"(Sync to Random Custom Cue, match \"{AuditionCueName}\") by hand, then press Play.");
            }
        }

        /// <summary>
        /// Surfaces the WAMP error detail payload that the ClientCore ErrorException carries in
        /// its internal <c>Json</c> field (read via reflection) — for invalid_arguments this
        /// usually names the exact offending argument.
        /// </summary>
        private static string DescribeError(Exception ex)
        {
            var json = ex.GetType()
                .GetProperty("Json", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public)?
                .GetValue(ex) as string;

            return string.IsNullOrWhiteSpace(json) ? ex.Message : $"{ex.Message} | {json.Trim()}";
        }

        /// <summary>
        /// Finds the "Root" MusicTransition owned by the given container. Transitions reference
        /// their container through an <c>owner</c> field rather than the parent/child hierarchy.
        /// </summary>
        private async Task<string?> FindTransitionRootAsync(string containerId)
        {
            var result = await QueryAsync(
                "$ from type MusicTransition where name = \"Root\"",
                new[] { "id", "name", "owner" });

            var root = (result?[ReturnKey] as JArray)?
                .FirstOrDefault(t => t["owner"]?["id"]?.ToString() == containerId);

            var rootId = root?["id"]?.ToString();
            _logger.LogInformation("Transition Root for container {Container}: {Root}", containerId, rootId ?? "<none>");
            return string.IsNullOrEmpty(rootId) ? null : rootId;
        }

        /// <summary>
        /// Adds a new rule MusicTransition as a child of the Root. Per the work-unit structure,
        /// rules live in the Root's default ChildrenList (no named list), appended last so the
        /// rule has the highest priority. Surfaces the full WAMP error detail if create is rejected.
        /// </summary>
        private async Task<string> CreateTransitionRuleAsync(string rootId)
        {
            var result = await _client.Call(ak.wwise.core.@object.create, new JObject(
                new JProperty("parent", rootId),
                new JProperty("type", "MusicTransition"),
                new JProperty("name", "Transition"),
                new JProperty("onNameConflict", "rename")));

            _logger.LogInformation("MusicTransition create result: {Result}", result?.ToString());
            return RequireId(result, "music transition");
        }

        private async Task SetTransitionProperty(string transitionId, string property, object value)
        {
            await _client.Call(ak.wwise.core.@object.setProperty, new JObject(
                new JProperty("object", transitionId),
                new JProperty("property", property),
                new JProperty("value", JToken.FromObject(value))));
        }

        /// <summary>
        /// Places one custom cue <see cref="AuditionCueOffsetFromEndMs"/> ms before the end of
        /// every Music Segment in the copied structure, so the transition can jump straight to the
        /// run-up into the segment's end. All edits happen on the copy inside the temp Work Unit,
        /// so production is untouched.
        /// </summary>
        private async Task CreateAuditionCuesAsync(AuditionSession session, CancellationToken cancellationToken)
        {
            Status($"Placing one audition cue {AuditionCueOffsetFromEndMs} ms before each segment's end...");

            var segments = await GetSegmentsAsync(session);
            if (segments.Count == 0)
            {
                _logger.LogInformation("No music segments found under the copy; skipping cue creation.");
                return;
            }

            int created = 0;
            foreach (var segment in segments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double lengthMs = await GetSegmentLengthMsAsync(segment.Id);
                _logger.LogInformation("Segment \"{Segment}\" measured length: {Length} ms.", segment.Name, lengthMs);
                if (lengthMs <= AuditionCueOffsetFromEndMs)
                {
                    _logger.LogWarning("Skipping segment {Segment}: length {Length} ms <= offset {Offset} ms.",
                        segment.Name, lengthMs, AuditionCueOffsetFromEndMs);
                    continue;
                }

                int cueTime = (int)Math.Round(lengthMs - AuditionCueOffsetFromEndMs);
                try
                {
                    await _client.Call(ak.wwise.core.@object.create, new JObject(
                        new JProperty("parent", segment.Id),
                        new JProperty("type", "MusicCue"),
                        new JProperty("list", "Cues"),
                        new JProperty("name", AuditionCueName),
                        new JProperty("@TimeMs", cueTime),
                        new JProperty("@CueType", CustomCueType),
                        new JProperty("onNameConflict", "rename")));
                    created++;
                }
                catch (Exception ex)
                {
                    Notify($"Could not create cue at {cueTime} ms on \"{segment.Name}\": {ex.Message}");
                    _logger.LogWarning(ex, "MusicCue create failed at {Time} ms on {Segment}.", cueTime, segment.Name);
                }
            }

            Status($"Added {created} audition cue(s) across {segments.Count} segment(s).");
        }

        /// <summary>Returns the Music Segments in the copied structure (the copy itself if it is one).</summary>
        private async Task<List<MusicObjectInfo>> GetSegmentsAsync(AuditionSession session)
        {
            if (string.Equals(session.Target.Type, "MusicSegment", StringComparison.OrdinalIgnoreCase))
            {
                return new List<MusicObjectInfo> { new() { Id = session.CopyId, Name = session.Target.Name, Type = "MusicSegment" } };
            }

            var result = await QueryAsync(
                $"$ \"{session.CopyId}\" select descendants where type = \"MusicSegment\"",
                new[] { "id", "name", "type", "path" });

            return result?[ReturnKey] is JArray segments
                ? segments.Select(ToMusicObject).ToList()
                : new List<MusicObjectInfo>();
        }

        /// <summary>
        /// Returns a segment's length in milliseconds. The primary source is the segment's own
        /// <c>@EndPosition</c> property (the Exit cue position) — it is reliable regardless of how
        /// many Music Tracks the segment holds, unlike <c>maxDurationSource</c>, which can come back
        /// empty for multi-track segments. Falls back to maxDurationSource, then the largest cue
        /// time. Returns 0 if nothing can be determined.
        /// </summary>
        private async Task<double> GetSegmentLengthMsAsync(string segmentId)
        {
            // Primary: the segment's End Position (ms), which marks the Exit cue / segment end.
            try
            {
                var endResult = await QueryAsync(
                    $"$ \"{segmentId}\"",
                    new[] { "id", "name", "@EndPosition" });

                var endMs = (endResult?[ReturnKey] as JArray)?.FirstOrDefault()
                    ?["@EndPosition"]?.Value<double>();

                if (endMs is > 0)
                {
                    _logger.LogInformation("Segment {Segment}: @EndPosition {Ms} ms.", segmentId, endMs);
                    return endMs.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "@EndPosition query failed for segment {Segment}.", segmentId);
            }

            // Fallback: trimmed duration (seconds) of the longest audio source under the segment.
            try
            {
                var durResult = await QueryAsync(
                    $"$ \"{segmentId}\"",
                    new[] { "id", "name", "maxDurationSource" });

                var seconds = (durResult?[ReturnKey] as JArray)?.FirstOrDefault()
                    ?["maxDurationSource"]?["trimmedDuration"]?.Value<double>();

                if (seconds is > 0)
                {
                    double ms = seconds.Value * 1000.0;
                    _logger.LogInformation("Segment {Segment}: maxDurationSource trimmedDuration {Seconds}s -> {Ms} ms.",
                        segmentId, seconds, ms);
                    return ms;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "maxDurationSource query failed for segment {Segment}.", segmentId);
            }

            // Fallback: the largest cue @TimeMs (e.g. the Exit cue), when it is populated.
            var queries = new[]
            {
                ("descendants", $"$ \"{segmentId}\" select descendants where type = \"MusicCue\""),
                ("children",    $"$ \"{segmentId}\" select children where type = \"MusicCue\""),
                ("ancestor",    $"$ from type MusicCue where ancestors.any(id = \"{segmentId}\")"),
            };

            foreach (var (label, waql) in queries)
            {
                try
                {
                    var result = await QueryAsync(waql, new[] { "id", "name", "@TimeMs", "@CueType" });

                    if (result?[ReturnKey] is JArray cues && cues.Count > 0)
                    {
                        _logger.LogInformation("Segment {Segment}: {Count} cue(s) via {Relation}: {Cues}",
                            segmentId, cues.Count, label, cues.ToString());
                        return cues.Max(c => c["@TimeMs"]?.Value<double>() ?? 0);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cue query ({Relation}) failed for segment {Segment}.", label, segmentId);
                }
            }

            _logger.LogWarning("No cues found under segment {Segment} (copy may not preserve cues).", segmentId);
            return 0;
        }

        private async Task<List<MusicObjectInfo>> GetCustomCuesAsync(string rootId)
        {
            try
            {
                var result = await QueryAsync(
                    $"$ \"{rootId}\" select descendants where type = \"MusicCue\" and @CueType = {CustomCueType}",
                    new[] { "id", "name", "type", "path" });

                if (result?[ReturnKey] is JArray cues)
                {
                    return cues.Select(ToMusicObject).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enumerate custom cues.");
            }

            return new List<MusicObjectInfo>();
        }

        /// <summary>Best-effort rollback: destroy the transport, then delete the temp Work Unit.</summary>
        private async Task SafeTeardownAsync(AuditionSession session)
        {
            if (session.TransportId is int transportId)
            {
                await SafeCall(() => _client.Call(ak.wwise.core.transport.destroy, new JObject(
                    new JProperty("transport", transportId))));
            }

            if (!string.IsNullOrEmpty(session.TempWorkUnitId))
            {
                await SafeCall(() => _client.Call(ak.wwise.core.@object.delete, new JObject(
                    new JProperty("object", session.TempWorkUnitId))));
            }
        }

        private async Task SafeCall(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cleanup step failed (continuing).");
            }
        }

        private Task<JObject> QueryAsync(string waql, string[] returnFields)
        {
            var args = new JObject(new JProperty(WaqlKey, waql));
            var options = new JObject(new JProperty(ReturnKey, new JArray(returnFields)));
            return _client.Call(ak.wwise.core.@object.get, args, options);
        }

        private static string RequireId(JObject? result, string what)
        {
            var id = result?["id"]?.ToString();
            if (string.IsNullOrEmpty(id))
                throw new InvalidOperationException($"WAAPI did not return an id for the {what}.");
            return id;
        }

        private static MusicObjectInfo ToMusicObject(JToken token) => new()
        {
            Id = token["id"]?.ToString() ?? string.Empty,
            Name = token["name"]?.ToString() ?? string.Empty,
            Type = token["type"]?.ToString() ?? string.Empty,
            Path = token["path"]?.ToString() ?? string.Empty,
        };

        private void Status(string message)
        {
            _logger.LogInformation("{Message}", message);
            StatusUpdated?.Invoke(this, message);
        }

        private void Notify(string message)
        {
            _logger.LogWarning("{Message}", message);
            NotificationRequested?.Invoke(this, message);
        }
    }
}
