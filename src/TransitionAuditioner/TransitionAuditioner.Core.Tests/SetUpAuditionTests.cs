using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Tests
{
    /// <summary>
    /// Covers SetUpAuditionAsync: the harness build wrapped in an undo group, cue-placement
    /// arithmetic and skipping, rollback on failure, and not stacking scaffolding.
    /// </summary>
    public class SetUpAuditionTests : TransitionAuditionerServiceTestBase
    {
        [Fact]
        public async Task SetUpAuditionAsync_HappyPath_BuildsHarnessInsideUndoGroupAndSetsSession()
        {
            SetupSuccessfulSegmentSetup(segmentLengthMs: 5000);

            var session = await Service.SetUpAuditionAsync(Segment());

            // Build is wrapped in an undo group: begin once, end once.
            Client.Verify(c => c.Call(ak.wwise.core.undo.beginGroup, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.Once);
            Client.Verify(c => c.Call(ak.wwise.core.undo.endGroup, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.Once);

            // The harness Music Switch Container is created from the production structure, which is copied (never moved).
            Client.Verify(c => c.Call(ak.wwise.core.@object.create,
                It.Is<object>(o => o is JObject && (string?)((JObject)o)["type"] == "MusicSwitchContainer"
                                                && (string?)((JObject)o)["name"] == "TransitionAuditioner_TEMP_DELETE_ME"),
                It.IsAny<object>(), It.IsAny<int>()), Times.Once);
            Client.Verify(c => c.Call(ak.wwise.core.@object.copy, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.Once);
            Client.Verify(c => c.Call(ak.wwise.core.@object.move, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.Never);

            // A transport is created and the session is published.
            Client.Verify(c => c.Call(ak.wwise.core.transport.create, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.Once);
            Assert.NotNull(session);
            Assert.Same(session, Service.Session);
            Assert.True(Service.IsSetUp);
        }

        [Fact]
        public async Task SetUpAuditionAsync_PlacesCueOffsetMsBeforeSegmentEnd()
        {
            SetupSuccessfulSegmentSetup(segmentLengthMs: 5000);
            Service.AuditionCueOffsetFromEndMs = 1000;

            await Service.SetUpAuditionAsync(Segment());

            // Cue time = measured length - offset = 5000 - 1000.
            Client.Verify(c => c.Call(ak.wwise.core.@object.create,
                It.Is<object>(o => o is JObject && (string?)((JObject)o)["type"] == "MusicCue"
                                                && (int?)((JObject)o)["@TimeMs"] == 4000),
                It.IsAny<object>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task SetUpAuditionAsync_WhenSegmentShorterThanOffset_SkipsCueAndRecordsSkip()
        {
            SetupSuccessfulSegmentSetup(segmentLengthMs: 500);
            Service.AuditionCueOffsetFromEndMs = 1000;

            var session = await Service.SetUpAuditionAsync(Segment(name: "Tiny"));

            // No audition cue is created when the segment is not longer than the offset.
            Client.Verify(c => c.Call(ak.wwise.core.@object.create,
                It.Is<object>(o => o is JObject && (string?)((JObject)o)["type"] == "MusicCue"),
                It.IsAny<object>(), It.IsAny<int>()), Times.Never);

            var skip = Assert.Single(session.SkippedSegments);
            Assert.Equal("Tiny", skip.Name);
            Assert.Equal(500, skip.MeasuredLengthMs);
            Assert.Contains(Notifications, n => n.Contains("Tiny"));
        }

        [Fact]
        public async Task SetUpAuditionAsync_WhenBuildStepFails_RollsBackAndRethrows()
        {
            SetupSuccessfulSegmentSetup(segmentLengthMs: 5000);

            // Make the structure copy fail, after the harness container has already been created.
            Client.Setup(c => c.Call(ak.wwise.core.@object.copy, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()))
                  .ThrowsAsync(new InvalidOperationException("WAAPI copy failed"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => Service.SetUpAuditionAsync(Segment()));

            // Rollback deletes the half-built harness, and the undo group is still closed (finally).
            Client.Verify(c => c.Call(ak.wwise.core.@object.delete, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.AtLeastOnce);
            Client.Verify(c => c.Call(ak.wwise.core.undo.endGroup, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.AtLeastOnce);
            Assert.Null(Service.Session);
            Assert.False(Service.IsSetUp);
        }

        [Fact]
        public async Task SetUpAuditionAsync_WhenSessionAlreadyExists_TearsDownPreviousFirst()
        {
            SetupSuccessfulSegmentSetup(segmentLengthMs: 5000);

            await Service.SetUpAuditionAsync(Segment(id: "{FIRST}"));
            Client.Invocations.Clear();

            await Service.SetUpAuditionAsync(Segment(id: "{SECOND}"));

            // The second setup must tear down the first harness before building again, so a delete
            // happens during the second call. Never stack scaffolding.
            Client.Verify(c => c.Call(ak.wwise.core.@object.delete, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.AtLeastOnce);
            Assert.NotNull(Service.Session);
            Assert.Equal("{SECOND}", Service.Session!.Target.Id);
        }
    }
}
