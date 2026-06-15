using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using JPAudio.WaapiTools.ClientJson;
using JPAudio.WaapiTools.Tool.TransitionAuditioner.Core;
using JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Models;

namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Tests
{
    /// <summary>
    /// Shared scaffolding for the service tests. Mocks <see cref="IJsonClient"/> so the WAAPI
    /// surface can be driven with canned JSON, mirroring the approach used by the
    /// PropertyContainerAuditor.Core tests.
    ///
    /// Overload note: the service reaches <c>ak.wwise.core.object.get</c> exclusively through its
    /// private QueryAsync helper, which calls the <c>(string, JObject, JObject, int)</c> overload.
    /// Every other WAAPI call resolves to the <c>(string, object, object, int)</c> overload. The two
    /// are mocked separately so each can return the right shape.
    /// </summary>
    public abstract class TransitionAuditionerServiceTestBase
    {
        protected readonly Mock<IJsonClient> Client = new(MockBehavior.Loose);
        protected readonly Mock<ILogger<TransitionAuditionerService>> Logger = new();
        protected readonly TransitionAuditionerService Service;

        /// <summary>The transport-state publish handler the service registers during setup,
        /// captured so tests can fire state changes at it directly.</summary>
        protected JsonClient.PublishHandler? TransportHandler;

        protected readonly List<string> Notifications = new();
        protected readonly List<bool> PlaybackStates = new();

        protected TransitionAuditionerServiceTestBase()
        {
            Service = new TransitionAuditionerService(Client.Object, Logger.Object);
            Service.NotificationRequested += (_, msg) => Notifications.Add(msg);
            Service.PlaybackStateChanged += (_, playing) => PlaybackStates.Add(playing);
        }

        protected static JObject Ret(params JObject[] items) =>
            new(new JProperty("return", new JArray(items.Cast<object>().ToArray())));

        protected static JObject Empty => new(new JProperty("return", new JArray()));

        /// <summary>
        /// Wires a minimal, successful WAAPI backend for <see cref="TransitionAuditionerService.SetUpAuditionAsync"/>
        /// run against a single MusicSegment target whose measured (SegmentEnd) length is
        /// <paramref name="segmentLengthMs"/>. The transition-rule and custom-cue lookups return empty;
        /// those steps are best-effort and a successful setup does not depend on them.
        /// </summary>
        protected void SetupSuccessfulSegmentSetup(double segmentLengthMs)
        {
            // Measure length from @EndPosition so cue placement needs only one query, not the
            // multi-cue Exit-cue traversal.
            Service.LengthSource = SegmentLengthSource.SegmentEnd;

            Client.Setup(c => c.Call(ak.wwise.core.@object.get, It.IsAny<JObject>(), It.IsAny<JObject>(), It.IsAny<int>()))
                  .Returns<string, JObject, JObject, int>((_, _, options, _) => Task.FromResult(RouteGet(options, segmentLengthMs)));

            WireNonQueryCalls();
        }

        /// <summary>
        /// Wires a successful setup that measures length through the default <see cref="SegmentLengthSource.ExitCue"/>
        /// basis. The single copied segment exposes the given cues (by id -> @CueType/@TimeMs); the Exit cue
        /// is the one with <c>@CueType</c> 1. When no readable Exit cue is present the service falls back to
        /// <paramref name="endPositionMs"/> (@EndPosition).
        /// </summary>
        protected void SetupExitCueSegmentSetup(IReadOnlyList<(string Id, int CueType, double TimeMs)> cues, double endPositionMs)
        {
            Service.LengthSource = SegmentLengthSource.ExitCue;

            Client.Setup(c => c.Call(ak.wwise.core.@object.get, It.IsAny<JObject>(), It.IsAny<JObject>(), It.IsAny<int>()))
                  .Returns<string, JObject, JObject, int>((_, args, options, _) =>
                      Task.FromResult(RouteExitCueGet(args, options, cues, endPositionMs)));

            WireNonQueryCalls();
        }

        /// <summary>Shared wiring for every non-<c>object.get</c> call: create/copy/setProperty/setReference/
        /// undo/commands tolerate an {id} result; transport.create yields a transport id; the transport-state
        /// subscription handler is captured.</summary>
        private void WireNonQueryCalls()
        {
            Client.Setup(c => c.Call(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()))
                  .ReturnsAsync(new JObject(new JProperty("id", "{NEW}")));

            // Declared after the generic setup so it wins for transport.create (Moq: last match wins).
            Client.Setup(c => c.Call(ak.wwise.core.transport.create, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()))
                  .ReturnsAsync(new JObject(new JProperty("transport", 1)));

            Client.Setup(c => c.Subscribe(ak.wwise.core.transport.stateChanged, It.IsAny<JObject>(), It.IsAny<JsonClient.PublishHandler>(), It.IsAny<int>()))
                  .Callback<string, JObject, JsonClient.PublishHandler, int>((_, _, handler, _) => TransportHandler = handler)
                  .ReturnsAsync(42);
        }

        /// <summary>Routes <c>object.get</c> queries by the return fields they request (and, where
        /// that is ambiguous, the WAQL). Only the fields a successful segment setup needs are served.</summary>
        private static JObject RouteGet(JObject? options, double segmentLengthMs)
        {
            var fields = (options?["return"] as JArray)?.Select(f => f.ToString()).ToList() ?? new List<string>();

            // GetTargetWorkUnitIdAsync: "$ {id}" returning [id, name, workunit].
            if (fields.Contains("workunit"))
                return Ret(new JObject(
                    new JProperty("id", "{TARGET}"),
                    new JProperty("workunit", new JObject(new JProperty("id", "{WU}")))));

            // GetEndPositionMsAsync (SegmentEnd length basis).
            if (fields.Contains("@EndPosition"))
                return Ret(new JObject(new JProperty("@EndPosition", segmentLengthMs)));

            // Custom-cue enumeration and transition-root lookup are best-effort; empty is fine.
            return Empty;
        }

        /// <summary>Like <see cref="RouteGet"/>, but serves the Exit-cue traversal: a segment's Cues list,
        /// per-cue resolution by id, and the @EndPosition fallback.</summary>
        private static JObject RouteExitCueGet(JObject? args, JObject? options,
            IReadOnlyList<(string Id, int CueType, double TimeMs)> cues, double endPositionMs)
        {
            var fields = (options?["return"] as JArray)?.Select(f => f.ToString()).ToList() ?? new List<string>();

            if (fields.Contains("workunit"))
                return Ret(new JObject(
                    new JProperty("id", "{TARGET}"),
                    new JProperty("workunit", new JObject(new JProperty("id", "{WU}")))));

            // The segment's "Cues" object-list: one entry per configured cue, identified by id.
            if (fields.Contains("Cues"))
                return Ret(new JObject(
                    new JProperty("id", "{SEG}"),
                    new JProperty("Cues", new JArray(cues.Select(c => (object)new JObject(new JProperty("id", c.Id))).ToArray()))));

            // Per-cue resolution: "$ {cueId}" returning [id, name, @TimeMs, @CueType].
            if (fields.Contains("@CueType"))
            {
                var cueId = ExtractDollarId(args?[WaqlKey]?.ToString());
                var cue = cues.FirstOrDefault(c => c.Id == cueId);
                if (cue.Id != null)
                    return Ret(new JObject(
                        new JProperty("id", cue.Id),
                        new JProperty("@TimeMs", cue.TimeMs),
                        new JProperty("@CueType", cue.CueType)));
                return Empty;
            }

            // GetEndPositionMsAsync fallback.
            if (fields.Contains("@EndPosition"))
                return Ret(new JObject(new JProperty("@EndPosition", endPositionMs)));

            return Empty;
        }

        private const string WaqlKey = "waql";

        /// <summary>Pulls the id out of a <c>$ "{id}"</c> WAQL query.</summary>
        private static string? ExtractDollarId(string? waql)
        {
            if (string.IsNullOrEmpty(waql)) return null;
            int first = waql.IndexOf('"');
            int last = waql.LastIndexOf('"');
            return (first >= 0 && last > first) ? waql.Substring(first + 1, last - first - 1) : null;
        }

        /// <summary>Configures <c>ak.wwise.ui.getSelectedObjects</c> to return the given selection.
        /// The call site passes a null args and a JObject options, which binds the JObject overload.</summary>
        protected void SetupSelection(JObject selectionResult)
        {
            Client.Setup(c => c.Call(ak.wwise.ui.getSelectedObjects, It.IsAny<JObject>(), It.IsAny<JObject>(), It.IsAny<int>()))
                  .ReturnsAsync(selectionResult);
        }

        protected static JObject Selection(params JObject[] objects) =>
            new(new JProperty("objects", new JArray(objects.Cast<object>().ToArray())));

        protected static JObject Obj(string id, string name, string type, string path) =>
            new(
                new JProperty("id", id),
                new JProperty("name", name),
                new JProperty("type", type),
                new JProperty("path", path));

        protected static MusicObjectInfo Segment(string id = "{SEG}", string name = "MySegment") =>
            new() { Id = id, Name = name, Type = "MusicSegment", Path = "\\Interactive Music Hierarchy\\" + name };

        /// <summary>The uris of every <c>Call</c> invocation, in order — used to assert relative
        /// ordering of teardown steps. (Unsubscribe etc. are excluded; their first arg is not a string.)</summary>
        protected List<string> CallUris() => Client.Invocations
            .Where(i => i.Arguments.Count > 0 && i.Arguments[0] is string)
            .Select(i => (string)i.Arguments[0])
            .ToList();
    }
}
