using Newtonsoft.Json.Linq;
using Xunit;

namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Tests
{
    /// <summary>
    /// Covers the transport-state subscription handler: the stateChanged topic publishes for every
    /// transport, so the service must filter to its own and map state strings to a playing flag.
    /// </summary>
    public class TransportStateTests : TransitionAuditionerServiceTestBase
    {
        private async Task SetUpAsync() => await Service.SetUpAuditionAsync(Segment());

        private static JObject State(int transport, string state) =>
            new(new JProperty("transport", transport), new JProperty("state", state));

        [Fact]
        public async Task TransportState_WhenOwnTransportPlays_RaisesPlaybackStartedThenStopped()
        {
            SetupSuccessfulSegmentSetup(segmentLengthMs: 5000);
            await SetUpAsync(); // captures TransportHandler; the created transport id is 1

            Assert.NotNull(TransportHandler);

            TransportHandler!(State(1, "playing"));
            TransportHandler!(State(1, "stopped"));

            Assert.Equal(new[] { true, false }, PlaybackStates);
        }

        [Fact]
        public async Task TransportState_WhenOtherTransportPlays_IsIgnored()
        {
            SetupSuccessfulSegmentSetup(segmentLengthMs: 5000);
            await SetUpAsync();

            // A different transport's state change must not move our playback indicator.
            TransportHandler!(State(99, "playing"));

            Assert.Empty(PlaybackStates);
        }

        [Fact]
        public async Task TransportState_NonPlayingStatesReportNotPlaying()
        {
            SetupSuccessfulSegmentSetup(segmentLengthMs: 5000);
            await SetUpAsync();

            TransportHandler!(State(1, "paused"));

            Assert.Equal(new[] { false }, PlaybackStates);
        }
    }
}
