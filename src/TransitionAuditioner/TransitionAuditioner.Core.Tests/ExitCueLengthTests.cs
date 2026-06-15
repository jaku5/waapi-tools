using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Tests
{
    /// <summary>
    /// Covers the default ExitCue length basis and its fallback to the segment end. ExitCue is the
    /// most correct boundary for auditioning a transition, but it is not always readable, so the
    /// fallback (and its user notification) matters.
    /// </summary>
    public class ExitCueLengthTests : TransitionAuditionerServiceTestBase
    {
        [Fact]
        public async Task SetUpAuditionAsync_WhenExitCueReadable_PlacesCueRelativeToExitCue()
        {
            // The Exit cue (@CueType 1) sits at 4000 ms; the Entry cue (@CueType 0) must be ignored.
            SetupExitCueSegmentSetup(
                cues: new[] { ("{ENTRY}", 0, 0d), ("{EXIT}", 1, 4000d) },
                endPositionMs: 9999); // would give a different answer if the fallback were taken
            Service.AuditionCueOffsetFromEndMs = 1000;

            await Service.SetUpAuditionAsync(Segment());

            // Cue time = exit-cue position - offset = 4000 - 1000.
            Client.Verify(c => c.Call(ak.wwise.core.@object.create,
                It.Is<object>(o => o is JObject && (string?)((JObject)o)["type"] == "MusicCue"
                                                && (int?)((JObject)o)["@TimeMs"] == 3000),
                It.IsAny<object>(), It.IsAny<int>()), Times.Once);

            // The fallback notification must NOT appear when the Exit cue was used.
            Assert.DoesNotContain(Notifications, n => n.Contains("Segment end"));
        }

        [Fact]
        public async Task SetUpAuditionAsync_WhenNoExitCue_FallsBackToSegmentEndAndNotifies()
        {
            // Only an Entry (0) and a Custom (2) cue exist — no Exit cue — so the service falls back
            // to @EndPosition (6000 ms).
            SetupExitCueSegmentSetup(
                cues: new[] { ("{ENTRY}", 0, 0d), ("{CUSTOM}", 2, 2500d) },
                endPositionMs: 6000);
            Service.AuditionCueOffsetFromEndMs = 1000;

            await Service.SetUpAuditionAsync(Segment());

            // Cue time = segment end - offset = 6000 - 1000.
            Client.Verify(c => c.Call(ak.wwise.core.@object.create,
                It.Is<object>(o => o is JObject && (string?)((JObject)o)["type"] == "MusicCue"
                                                && (int?)((JObject)o)["@TimeMs"] == 5000),
                It.IsAny<object>(), It.IsAny<int>()), Times.Once);

            Assert.Contains(Notifications, n => n.Contains("Segment end"));
        }

        [Fact]
        public async Task SetUpAuditionAsync_WhenSegmentHasNoCues_FallsBackToSegmentEnd()
        {
            // No cues at all on the segment; the Exit-cue read yields nothing, so the fallback runs.
            SetupExitCueSegmentSetup(
                cues: System.Array.Empty<(string, int, double)>(),
                endPositionMs: 3000);
            Service.AuditionCueOffsetFromEndMs = 1000;

            await Service.SetUpAuditionAsync(Segment());

            // Cue time = segment end - offset = 3000 - 1000.
            Client.Verify(c => c.Call(ak.wwise.core.@object.create,
                It.Is<object>(o => o is JObject && (string?)((JObject)o)["type"] == "MusicCue"
                                                && (int?)((JObject)o)["@TimeMs"] == 2000),
                It.IsAny<object>(), It.IsAny<int>()), Times.Once);
        }
    }
}
