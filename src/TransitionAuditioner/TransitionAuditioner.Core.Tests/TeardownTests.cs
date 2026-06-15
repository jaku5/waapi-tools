using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Tests
{
    /// <summary>
    /// Covers TeardownAsync / the rollback path. The step ordering here is load-bearing: the service
    /// comments note that stopping playback and moving the selection/inspector off the temp structure
    /// before deleting it avoids version-specific Wwise crashes. These tests pin that intent.
    /// </summary>
    public class TeardownTests : TransitionAuditionerServiceTestBase
    {
        private async Task SetUpAsync() => await Service.SetUpAuditionAsync(Segment());

        [Fact]
        public async Task TeardownAsync_StopsPlaybackThenDestroysTransportThenDeletesHarness()
        {
            SetupSuccessfulSegmentSetup(segmentLengthMs: 5000);
            await SetUpAsync();
            Client.Invocations.Clear();

            await Service.TeardownAsync();

            var uris = CallUris();
            int stop = uris.IndexOf(ak.wwise.core.transport.executeAction);
            int destroy = uris.IndexOf(ak.wwise.core.transport.destroy);
            int delete = uris.IndexOf(ak.wwise.core.@object.delete);

            Assert.True(stop >= 0, "playback should be stopped during teardown");
            Assert.True(destroy >= 0, "transport should be destroyed during teardown");
            Assert.True(delete >= 0, "harness should be deleted during teardown");

            // Crash-avoidance ordering: stop -> destroy -> delete.
            Assert.True(stop < destroy, "playback must be stopped before the transport is destroyed");
            Assert.True(destroy < delete, "the transport must be destroyed before the harness is deleted");
        }

        [Fact]
        public async Task TeardownAsync_MovesSelectionOffTempStructureBeforeDeleting()
        {
            SetupSuccessfulSegmentSetup(segmentLengthMs: 5000);
            await SetUpAsync();
            Client.Invocations.Clear();

            await Service.TeardownAsync();

            var uris = CallUris();
            int command = uris.IndexOf(ak.wwise.ui.commands.execute); // FindInProjectExplorer / Inspect onto the original target
            int delete = uris.IndexOf(ak.wwise.core.@object.delete);

            Assert.True(command >= 0, "selection/inspector should be moved onto the original target");
            Assert.True(command < delete, "selection must move off the temp structure before it is deleted");
        }

        [Fact]
        public async Task TeardownAsync_ExcludesHarnessBeforeDeletingIt()
        {
            SetupSuccessfulSegmentSetup(segmentLengthMs: 5000);
            await SetUpAsync();
            Client.Invocations.Clear();

            await Service.TeardownAsync();

            // The harness is excluded (Inclusion=false) immediately before deletion to dodge the
            // crash-on-deleting-an-included-interactive-music-object bug.
            Client.Verify(c => c.Call(ak.wwise.core.@object.setProperty,
                It.Is<object>(o => o is JObject && (string?)((JObject)o)["property"] == "Inclusion"
                                                && ((JObject)o)["value"] != null && (bool)((JObject)o)["value"]! == false),
                It.IsAny<object>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task TeardownAsync_ClearsSession()
        {
            SetupSuccessfulSegmentSetup(segmentLengthMs: 5000);
            await SetUpAsync();

            await Service.TeardownAsync();

            Assert.Null(Service.Session);
            Assert.False(Service.IsSetUp);
        }

        [Fact]
        public async Task TeardownAsync_WhenNoSessionSetUp_DoesNotThrow()
        {
            // Safe to call with nothing set up (e.g. the user never ran a setup).
            await Service.TeardownAsync();

            Assert.Null(Service.Session);
        }
    }
}
