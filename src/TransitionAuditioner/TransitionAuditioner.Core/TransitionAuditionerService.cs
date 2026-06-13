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

        // Clearly-temporary name: the harness lives in the user's Work Unit until teardown deletes it,
        // so if it ever leaks (e.g. a crash mid-audition) it is obvious and easy to remove by hand.
        private const string HarnessContainerName = "TransitionAuditioner_TEMP_DELETE_ME";

        // @CueType value for a Custom cue (Entry = 0, Exit = 1, Custom = 2).
        private const int CustomCueType = 2;

        // How far before the segment's end to place the audition cue, so you can jump
        // straight to the run-up into the transition instead of playing the whole segment.
        // Hardcoded for the MVP; intended to become a UI setting later.
        public int AuditionCueOffsetFromEndMs { get; set; } = 1000;

        // How a segment's length is measured when placing the audition cue.
        public SegmentLengthSource LengthSource { get; set; } = SegmentLengthSource.ExitCue;

        // View id of a Music Playlist Editor this tool opened (2025+ only, where it can be closed
        // again). Empty when none is tracked. Closed during teardown.
        private string _createdPlaylistViewId = string.Empty;

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

        /// <summary>Raised with a short description of the current Wwise selection as it changes.</summary>
        public event EventHandler<SelectionInfo>? SelectionChanged;

        /// <summary>Raised when this tool's transport starts (true) or stops (false) playing.</summary>
        public event EventHandler<bool>? PlaybackStateChanged;

        private int? _selectionSubscriptionId;
        private int? _transportSubscriptionId;

        public bool IsConnected { get; private set; }
        public bool IsSetUp => Session != null;
        public string? ProjectName { get; private set; }
        public string? WwiseVersion { get; private set; }
        public AuditionSession? Session { get; private set; }

        // Wwise major version (year). ak.wwise.ui.layout.closeView only exists from 2025 onward,
        // and ak.wwise.ui.layout.getOrCreateView only from 2024 onward.
        private int _wwiseYear;

        /// <summary>True when this Wwise version can open a view via WAAPI
        /// (ak.wwise.ui.layout.getOrCreateView, 2024+). On 2023 we can only inspect the copied
        /// playlist so a Playlist Editor the user opened themselves shows the audition material.</summary>
        public bool CanCreatePlaylistEditorView => _wwiseYear >= 2024;

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
                _wwiseYear = info?["version"]?["year"]?.Value<int>() ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to fetch project info: {Message}", ex.Message);
            }

            // Track the Wwise selection live for the on-screen indicator, and seed it once.
            try
            {
                var options = new JObject(new JProperty(ReturnKey, new JArray("id", "name", "type", "path")));
                _selectionSubscriptionId = await _client.Subscribe(
                    ak.wwise.ui.selectionChanged, options, OnWwiseSelectionChanged);

                var current = await _client.Call(ak.wwise.ui.getSelectedObjects, null, options);
                RaiseSelection(current);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to subscribe to selection changes: {Message}", ex.Message);
            }
        }

        private void OnWwiseSelectionChanged(JObject json) => RaiseSelection(json);

        private void RaiseSelection(JObject? json)
        {
            var objects = json?["objects"] as JArray;
            string text;
            string id = string.Empty;
            bool isAuditionable = false;
            if (objects == null || objects.Count == 0)
            {
                text = "(nothing selected)";
            }
            else
            {
                var first = objects[0];
                id = first["id"]?.ToString() ?? string.Empty;
                text = $"{first["name"]} ({first["type"]})";
                if (objects.Count > 1)
                    text += $"  +{objects.Count - 1} more";

                // Pullable only if it's an interactive-music type and not the tool's own harness —
                // mirrors the validation in GetSelectedTargetAsync.
                isAuditionable =
                    AuditionableTypes.Contains(first["type"]?.ToString() ?? string.Empty) &&
                    first["path"]?.ToString()?.Contains(HarnessContainerName) != true;
            }

            SelectionChanged?.Invoke(this, new SelectionInfo { Id = id, Text = text, IsAuditionable = isAuditionable });
        }

        public void Disconnect()
        {
            IsConnected = false;

            // Politely unsubscribe from selection changes before the socket closes. Run it off the
            // UI thread (the WAMP client captures the sync context, so blocking here directly would
            // deadlock); the socket close is the backstop if it doesn't finish in time.
            if (_selectionSubscriptionId is int subId)
            {
                _selectionSubscriptionId = null;
                try { Task.Run(() => _client.Unsubscribe(subId)).Wait(2000); }
                catch { /* best effort — server cleans up on socket close anyway */ }
            }

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

            // Reject the tool's own temporary harness (or anything inside it): its unique name
            // appears in the path of the harness and every copied descendant.
            if (match["path"]?.ToString()?.Contains(HarnessContainerName) == true)
            {
                Notify("That's the audition tool's own temporary harness — select a production object instead.");
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

            // Build inside the target's own Work Unit (not a throwaway one) so the user's undo
            // history is preserved — Work Unit create/delete would flush it. The build is wrapped
            // in an undo group (one tidy entry); teardown deletes the harness. The production
            // structure is copied, never moved, so it is never mutated.
            await _client.Call(ak.wwise.core.undo.beginGroup);

            try
            {
                var workUnitId = await GetTargetWorkUnitIdAsync(target.Id);

                Status("Building the audition harness...");
                var switchContainer = await _client.Call(ak.wwise.core.@object.create, new JObject(
                    new JProperty("parent", workUnitId),
                    new JProperty("type", "MusicSwitchContainer"),
                    new JProperty("name", HarnessContainerName),
                    new JProperty("onNameConflict", "rename")));
                session.SwitchContainerId = RequireId(switchContainer, "harness container");
                cancellationToken.ThrowIfCancellationRequested();

                Status($"Copying \"{target.Name}\" into the harness...");
                var copy = await _client.Call(ak.wwise.core.@object.copy, new JObject(
                    new JProperty("object", target.Id),
                    new JProperty("parent", session.SwitchContainerId),
                    new JProperty("onNameConflict", "rename")));
                session.CopyId = RequireId(copy, "structure copy");
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
                    await SubscribeToTransportStateAsync(session.TransportId.Value);
                }

                Session = session;
                Status("Ready. Click Play to audition the transition.");
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
                // The build contains only undoable object operations now (no Work Unit create),
                // so the group ends cleanly and collapses the build into one undo entry.
                await SafeCall(() => _client.Call(ak.wwise.core.undo.endGroup, new JObject(
                    new JProperty("displayName", "Set up transition audition"))));
            }
        }

        /// <summary>Resolves the Work Unit that contains the target, to host the harness at its root.</summary>
        private async Task<string> GetTargetWorkUnitIdAsync(string targetId)
        {
            var result = await QueryAsync($"$ \"{targetId}\"", new[] { "id", "name", "workunit" });
            var workUnitId = (result?[ReturnKey] as JArray)?.FirstOrDefault()?["workunit"]?["id"]?.ToString();
            if (string.IsNullOrEmpty(workUnitId))
                throw new InvalidOperationException("Could not resolve the target's Work Unit.");
            return workUnitId;
        }

        public async Task TeardownAsync()
        {
            // Close any Music Playlist Editor we opened, even if no audition was ever set up
            // (the user may have opened it manually before/without a setup).
            await CloseCreatedPlaylistEditorAsync();

            if (Session == null)
                return;

            var session = Session;
            Session = null;
            await SafeTeardownAsync(session);
            Status("Cleaned up. The project was not modified or saved.");
        }

        public async Task ShowInProjectExplorerAsync()
        {
            if (Session?.SwitchContainerId is not { Length: > 0 } harnessId)
                return;

            // Select + inspect the harness so its editor (with the Transitions tab holding our
            // None->target rule) is shown. Editors follow the inspected object, not the tree
            // selection, so both are pointed at the harness.
            await SafeCall(() => _client.Call(ak.wwise.ui.commands.execute, new JObject(
                new JProperty("command", "FindInProjectExplorerSelectionChannel1"),
                new JProperty("objects", new JArray(harnessId)))));
            await SafeCall(() => Inspect(harnessId));
        }

        private Task Inspect(string objectId) => _client.Call(ak.wwise.ui.commands.execute, new JObject(
            new JProperty("command", "Inspect"),
            new JProperty("objects", new JArray(objectId))));

        /// <summary>
        /// Opens the Music Playlist Editor immediately. Only versions with closeView (2025+) can
        /// clean the view up, so only there do we track the view id (and only when we actually
        /// created it, i.e. one was not already open) for closing during teardown; older versions
        /// just open it and leave it.
        /// </summary>
        public async Task OpenPlaylistEditorAsync()
        {
            // Wwise 2023 lacks ak.wwise.ui.layout.getOrCreateView, so we can't open the view for the
            // user. Fall back to just inspecting the copied playlist: if the user already has a
            // Playlist Editor open, it follows the inspection and shows the audition material.
            if (!CanCreatePlaylistEditorView)
            {
                Status("Inspecting the copied playlist (open a Music Playlist Editor in Wwise to see it)...");
                await InspectCopiedPlaylistAsync();
                return;
            }

            bool canClose = _wwiseYear >= 2025;
            bool existedBefore = canClose && await PlaylistEditorExistsAsync();

            try
            {
                Status("Opening Music Playlist Editor...");
                var result = await _client.Call("ak.wwise.ui.layout.getOrCreateView", new JObject(
                    new JProperty("name", "MusicPlaylistEditor")));

                var viewId = result?["id"]?.ToString();
                if (canClose && !existedBefore && string.IsNullOrEmpty(_createdPlaylistViewId)
                    && !string.IsNullOrEmpty(viewId))
                {
                    _createdPlaylistViewId = viewId;
                }

                await InspectCopiedPlaylistAsync();
            }
            catch (Exception ex)
            {
                Notify($"Failed to open the Music Playlist Editor: {DescribeError(ex)}");
            }
        }

        /// <summary>For a Music Playlist Container audition, points the editor at the copied playlist so
        /// it shows the audition material, then restores the harness as the inspected object (the
        /// transitions editor follows it). The Playlist Editor keeps showing the playlist even after we
        /// inspect the non-playlist harness.</summary>
        private async Task InspectCopiedPlaylistAsync()
        {
            if (Session is { } session
                && string.Equals(session.Target.Type, "MusicPlaylistContainer", StringComparison.OrdinalIgnoreCase)
                && session.CopyId is { Length: > 0 } copyId)
            {
                await SafeCall(() => Inspect(copyId));
                if (session.SwitchContainerId is { Length: > 0 } harnessId)
                    await SafeCall(() => Inspect(harnessId));
            }
        }

        /// <summary>Closes a Music Playlist Editor this tool opened (2025+). No-op otherwise, so
        /// older versions never see an error.</summary>
        private async Task CloseCreatedPlaylistEditorAsync()
        {
            if (string.IsNullOrEmpty(_createdPlaylistViewId))
                return;

            var viewId = _createdPlaylistViewId;
            _createdPlaylistViewId = string.Empty;
            try
            {
                Status($"Closing Music Playlist Editor {viewId}...");
                await _client.Call("ak.wwise.ui.layout.closeView", new JObject(
                    new JProperty("viewID", viewId)));
            }
            catch (Exception ex)
            {
                // Older Wwise versions lack ak.wwise.ui.layout.closeView; leave the view open.
                Notify($"Could not close the Music Playlist Editor (your Wwise version may not " +
                       $"support it): {DescribeError(ex)}");
            }
        }

        private async Task<bool> PlaylistEditorExistsAsync()
        {
            try
            {
                var result = await _client.Call("ak.wwise.ui.layout.getViewInstances");
                return result != null && result.ToString().Contains("MusicPlaylistEditor");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query view instances.");
                return false;
            }
        }

        /// <summary>
        /// Subscribes to transport state changes so the UI can reflect live playback. The topic is
        /// global (it fires for every transport), so the handler filters to our own transport id.
        /// Best-effort: a failed subscription just means no live playback accent, not a broken setup.
        /// </summary>
        private async Task SubscribeToTransportStateAsync(int transportId)
        {
            try
            {
                // This topic requires a 'transport' option naming the watched transport (it is not a
                // 'return' field list — passing return fields, or omitting transport, makes the
                // subscribe call fail). Scoped to our transport, every publish is already ours.
                _transportSubscriptionId = await _client.Subscribe(
                    ak.wwise.core.transport.stateChanged,
                    new JObject(new JProperty("transport", transportId)),
                    json => OnTransportStateChanged(json, transportId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to subscribe to transport state changes.");
            }
        }

        private void OnTransportStateChanged(JObject json, int ownTransportId)
        {
            // The topic publishes for all transports; ignore anything that isn't ours.
            if (json["transport"]?.Value<int>() is int id && id != ownTransportId)
                return;

            // State is one of "playing" / "stopped" / "paused"; only "playing" is active playback.
            bool playing = string.Equals(json["state"]?.ToString(), "playing", StringComparison.OrdinalIgnoreCase);
            PlaybackStateChanged?.Invoke(this, playing);
        }

        /// <summary>Drops the transport-state subscription (best-effort) and reports playback stopped.</summary>
        private async Task UnsubscribeFromTransportStateAsync()
        {
            if (_transportSubscriptionId is not int subId)
                return;

            _transportSubscriptionId = null;
            await SafeCall(() => _client.Unsubscribe(subId));
            PlaybackStateChanged?.Invoke(this, false);
        }

        public async Task PlayAsync()
        {
            if (Session?.TransportId is int transportId)
            {
                await _client.Call(ak.wwise.core.transport.executeAction, new JObject(
                    new JProperty("transport", transportId),
                    new JProperty("action", "play")));
            }
        }

        public async Task StopAsync()
        {
            if (Session?.TransportId is int transportId)
            {
                await _client.Call(ak.wwise.core.transport.executeAction, new JObject(
                    new JProperty("transport", transportId),
                    new JProperty("action", "stop")));
            }
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
                session.TransitionId = transitionId;

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
                    Notify($"Skipped \"{segment.Name}\": measured length {lengthMs} ms is not greater than the " +
                           $"{AuditionCueOffsetFromEndMs} ms offset (try a smaller offset or a different length basis).");
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
        /// Returns a segment's length in milliseconds using the configured <see cref="LengthSource"/>.
        /// For <see cref="SegmentLengthSource.ExitCue"/>, falls back to the segment end if the Exit
        /// cue cannot be read. Returns 0 if it cannot be determined.
        /// </summary>
        private async Task<double> GetSegmentLengthMsAsync(string segmentId)
        {
            switch (LengthSource)
            {
                case SegmentLengthSource.AudioLength:
                    return await GetAudioLengthMsAsync(segmentId);

                case SegmentLengthSource.SegmentEnd:
                    return await GetEndPositionMsAsync(segmentId);

                default: // ExitCue
                    double exitMs = await GetExitCueMsAsync(segmentId);
                    if (exitMs > 0)
                        return exitMs;

                    double endMs = await GetEndPositionMsAsync(segmentId);
                    if (endMs > 0)
                        Notify($"Exit cue not readable for a segment; used Segment end ({endMs} ms) instead.");
                    return endMs;
            }
        }

        /// <summary>Segment length from its Exit cue (a MusicCue with <c>@CueType</c> 1).</summary>
        private async Task<double> GetExitCueMsAsync(string segmentId)
        {
            try
            {
                // The segment's "Cues" return field lists the cue objects directly — unlike
                // children/descendants traversal, which does not reach the Cues object-list.
                var segResult = await QueryAsync($"$ \"{segmentId}\"", new[] { "id", "Cues" });
                if ((segResult?[ReturnKey] as JArray)?.FirstOrDefault()?["Cues"] is not JArray cues || cues.Count == 0)
                {
                    _logger.LogWarning("Segment {Segment} exposed no Cues.", segmentId);
                    return 0;
                }

                // Resolve each cue by its own id (direct object access) and pick the Exit cue.
                foreach (var cue in cues)
                {
                    var cueId = cue["id"]?.ToString();
                    if (string.IsNullOrEmpty(cueId))
                        continue;

                    var cueResult = await QueryAsync($"$ \"{cueId}\"", new[] { "id", "name", "@TimeMs", "@CueType" });
                    var obj = (cueResult?[ReturnKey] as JArray)?.FirstOrDefault();
                    if (obj?["@CueType"]?.Value<int>() == 1)
                    {
                        var timeMs = obj["@TimeMs"]?.Value<double>() ?? 0;
                        _logger.LogInformation("Segment {Segment}: Exit cue {Cue} -> {Ms} ms.", segmentId, cueId, timeMs);
                        return timeMs;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exit cue lookup failed for segment {Segment}.", segmentId);
            }

            _logger.LogWarning("Exit cue not found for segment {Segment}.", segmentId);
            return 0;
        }

        /// <summary>Segment length from <c>@EndPosition</c> (the Exit cue position).</summary>
        private async Task<double> GetEndPositionMsAsync(string segmentId)
        {
            try
            {
                var result = await QueryAsync($"$ \"{segmentId}\"", new[] { "id", "name", "@EndPosition" });
                var endMs = (result?[ReturnKey] as JArray)?.FirstOrDefault()?["@EndPosition"]?.Value<double>();
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
            return 0;
        }

        /// <summary>Segment length from the trimmed duration of its longest audio source.</summary>
        private async Task<double> GetAudioLengthMsAsync(string segmentId)
        {
            try
            {
                var result = await QueryAsync($"$ \"{segmentId}\"", new[] { "id", "name", "maxDurationSource" });
                var seconds = (result?[ReturnKey] as JArray)?.FirstOrDefault()
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

        /// <summary>
        /// Best-effort rollback: stop playback, destroy the transport, exclude the temp structure,
        /// then delete the temp Work Unit. Stopping first avoids a Wwise crash when the structure is
        /// deleted mid-playback; excluding first sidesteps a version-specific crash on deleting an
        /// included interactive-music object. Both are harmless (the temp Work Unit is never saved).
        /// </summary>
        private async Task SafeTeardownAsync(AuditionSession session)
        {
            // Drop the state subscription first (and report playback stopped) so no stray events
            // arrive while the transport is being torn down.
            await UnsubscribeFromTransportStateAsync();

            // Stop all transports — covers playback the user may have started on any transport,
            // not just the one we created.
            await SafeCall(() => _client.Call(ak.wwise.core.transport.executeAction, new JObject(
                new JProperty("action", "stop"))));

            if (session.TransportId is int transportId)
            {
                await SafeCall(() => _client.Call(ak.wwise.core.transport.destroy, new JObject(
                    new JProperty("transport", transportId))));
            }

            // Move the Wwise selection AND the inspector off the temp structure before deleting it:
            // deleting the Work Unit while one of its Music Segments is selected or inspected crashes
            // Wwise. Both the tree selection (FindInProjectExplorer) and the inspected object
            // (Inspect — which the editors follow) must point at the original target, which lives
            // outside the temp Work Unit.
            if (!string.IsNullOrEmpty(session.Target.Id))
            {
                var targetArray = new JArray(session.Target.Id);

                await SafeCall(() => _client.Call(ak.wwise.ui.commands.execute, new JObject(
                    new JProperty("command", "FindInProjectExplorerSelectionChannel1"),
                    new JProperty("objects", targetArray))));

                await SafeCall(() => _client.Call(ak.wwise.ui.commands.execute, new JObject(
                    new JProperty("command", "Inspect"),
                    new JProperty("objects", new JArray(session.Target.Id)))));
            }

            // Exclude the harness before deleting, to dodge the crash-on-deleting-an-included
            // interactive-music-object bug. Inclusion only affects SoundBank generation, so this
            // has no side effects on a throwaway object that is never saved. The exclude + delete
            // are grouped so they collapse into a single undo entry.
            if (!string.IsNullOrEmpty(session.SwitchContainerId))
            {
                await SafeCall(() => _client.Call(ak.wwise.core.undo.beginGroup));
                await SafeCall(() => SetInclusionAsync(session.SwitchContainerId, false));

                // Delete the harness — its whole subtree (copy, cues, transition rule) goes with it.
                await SafeCall(() => _client.Call(ak.wwise.core.@object.delete, new JObject(
                    new JProperty("object", session.SwitchContainerId))));
                await SafeCall(() => _client.Call(ak.wwise.core.undo.endGroup, new JObject(
                    new JProperty("displayName", "Remove transition audition"))));
            }
        }

        private Task SetInclusionAsync(string objectId, bool included)
        {
            return _client.Call(ak.wwise.core.@object.setProperty, new JObject(
                new JProperty("object", objectId),
                new JProperty("property", "Inclusion"),
                new JProperty("value", included)));
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
