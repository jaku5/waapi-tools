using Xunit;

namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Tests
{
    /// <summary>
    /// Covers the selection-validation logic in GetSelectedTargetAsync: which selections are
    /// accepted as audition targets and which are rejected (with a user notification).
    /// </summary>
    public class GetSelectedTargetTests : TransitionAuditionerServiceTestBase
    {
        [Theory]
        [InlineData("MusicSwitchContainer")]
        [InlineData("MusicPlaylistContainer")]
        [InlineData("MusicSegment")]
        public async Task GetSelectedTargetAsync_WhenAuditionableTypeSelected_ReturnsIt(string type)
        {
            SetupSelection(Selection(
                Obj("{ID}", "MyMusic", type, "\\Interactive Music Hierarchy\\Default Work Unit\\MyMusic")));

            var result = await Service.GetSelectedTargetAsync();

            Assert.NotNull(result);
            Assert.Equal("{ID}", result!.Id);
            Assert.Equal("MyMusic", result.Name);
            Assert.Equal(type, result.Type);
        }

        [Fact]
        public async Task GetSelectedTargetAsync_WhenNothingSelected_ReturnsNullAndNotifies()
        {
            SetupSelection(Selection());

            var result = await Service.GetSelectedTargetAsync();

            Assert.Null(result);
            Assert.Single(Notifications);
        }

        [Fact]
        public async Task GetSelectedTargetAsync_WhenSelectionIsNotInteractiveMusic_ReturnsNullAndNotifies()
        {
            SetupSelection(Selection(
                Obj("{ID}", "MySound", "Sound", "\\Actor-Mixer Hierarchy\\Default Work Unit\\MySound")));

            var result = await Service.GetSelectedTargetAsync();

            Assert.Null(result);
            Assert.Single(Notifications);
        }

        [Fact]
        public async Task GetSelectedTargetAsync_WhenSelectionIsOwnHarness_ReturnsNullAndNotifies()
        {
            // An auditionable type, but living inside the tool's own temporary harness — must be rejected
            // so the user can't audition the scaffolding (which would stack copies on copies).
            SetupSelection(Selection(
                Obj("{ID}", "TransitionAuditioner_TEMP_DELETE_ME", "MusicSwitchContainer",
                    "\\Interactive Music Hierarchy\\Default Work Unit\\TransitionAuditioner_TEMP_DELETE_ME")));

            var result = await Service.GetSelectedTargetAsync();

            Assert.Null(result);
            Assert.Single(Notifications);
        }

        [Fact]
        public async Task GetSelectedTargetAsync_WhenMultipleSelected_PicksFirstAuditionableOne()
        {
            // A non-music object is selected first; the first auditionable object in the selection wins.
            SetupSelection(Selection(
                Obj("{S}", "MySound", "Sound", "\\Actor-Mixer Hierarchy\\MySound"),
                Obj("{M}", "MyMusic", "MusicSegment", "\\Interactive Music Hierarchy\\MyMusic")));

            var result = await Service.GetSelectedTargetAsync();

            Assert.NotNull(result);
            Assert.Equal("{M}", result!.Id);
            Assert.Equal("MusicSegment", result.Type);
        }
    }
}
