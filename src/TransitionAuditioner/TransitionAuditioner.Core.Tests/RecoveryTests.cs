using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Tests
{
    /// <summary>
    /// Covers the connect-time recovery sweep (<c>RecoverLeakedAuditionsAsync</c>), which cleans up
    /// harnesses left behind when a previous session exited abnormally (a crash or the debugger's
    /// Stop button), where graceful teardown never ran. The crash-avoidance ordering matters here too:
    /// stop playback and move the selection/inspector off the temp structure before deleting it.
    /// </summary>
    public class RecoveryTests : TransitionAuditionerServiceTestBase
    {
        private const string HarnessName = "TransitionAuditioner_TEMP_DELETE_ME";

        /// <summary>Wires connect-path calls: project info + the leaked-harness scan. The scan ("$ from
        /// type MusicSwitchContainer where name : ...") is the only object.get whose WAQL targets that
        /// type, so it is routed by that; everything else (getInfo, getProjectInfo, getSelectedObjects)
        /// gets a benign empty result.</summary>
        private void WireConnect(params JObject[] leakedHarnesses)
        {
            Client.Setup(c => c.Connect(It.IsAny<string>(), It.IsAny<int>())).Returns(Task.CompletedTask);

            // The leaked-harness scan goes through QueryAsync -> the (string, JObject, JObject, int) overload.
            Client.Setup(c => c.Call(ak.wwise.core.@object.get, It.IsAny<JObject>(), It.IsAny<JObject>(), It.IsAny<int>()))
                  .Returns<string, JObject, JObject, int>((_, args, _, _) =>
                  {
                      var waql = args?["waql"]?.ToString() ?? string.Empty;
                      return Task.FromResult(waql.Contains("MusicSwitchContainer") ? Ret(leakedHarnesses) : Empty);
                  });

            // Everything else (incl. getProjectInfo/getInfo/getSelectedObjects/transport/commands/undo/delete).
            Client.Setup(c => c.Call(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()))
                  .ReturnsAsync(new JObject());
            Client.Setup(c => c.Call(ak.wwise.ui.getSelectedObjects, It.IsAny<JObject>(), It.IsAny<JObject>(), It.IsAny<int>()))
                  .ReturnsAsync(Selection());
        }

        private static JObject Harness(string id, string parentId, string name = HarnessName) =>
            new(
                new JProperty("id", id),
                new JProperty("name", name),
                new JProperty("parent", new JObject(new JProperty("id", parentId))));

        [Fact]
        public async Task ConnectAsync_WithNoLeakedHarness_DoesNotStopOrDelete()
        {
            WireConnect();

            await Service.ConnectAsync();

            var uris = CallUris();
            Assert.DoesNotContain(ak.wwise.core.transport.executeAction, uris);
            Assert.DoesNotContain(ak.wwise.core.@object.delete, uris);
        }

        [Fact]
        public async Task ConnectAsync_WithLeakedHarness_StopsThenMovesSelectionThenExcludesThenDeletes()
        {
            WireConnect(Harness("{HARNESS}", "{PARENT}"));

            await Service.ConnectAsync();

            var uris = CallUris();
            int stop = uris.IndexOf(ak.wwise.core.transport.executeAction);
            int command = uris.IndexOf(ak.wwise.ui.commands.execute); // FindInProjectExplorer / Inspect onto the parent
            int delete = uris.IndexOf(ak.wwise.core.@object.delete);

            Assert.True(stop >= 0, "lingering playback should be stopped during recovery");
            Assert.True(command >= 0, "selection/inspector should be moved off the temp structure");
            Assert.True(delete >= 0, "the leaked harness should be deleted");

            // Crash-avoidance ordering: stop -> move selection -> delete.
            Assert.True(stop < delete, "playback must be stopped before the harness is deleted");
            Assert.True(command < delete, "selection must move off the temp structure before it is deleted");

            // Excluded immediately before deletion to dodge the crash-on-deleting-an-included-object bug.
            Client.Verify(c => c.Call(ak.wwise.core.@object.setProperty,
                It.Is<object>(o => o is JObject && (string?)((JObject)o)["property"] == "Inclusion"
                                                && ((JObject)o)["value"] != null && (bool)((JObject)o)["value"]! == false),
                It.IsAny<object>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task ConnectAsync_WithMultipleLeakedHarnesses_DeletesEach()
        {
            WireConnect(Harness("{H1}", "{P1}"), Harness("{H2}", "{P2}", HarnessName + "(1)"));

            await Service.ConnectAsync();

            Client.Verify(c => c.Call(ak.wwise.core.@object.delete,
                It.Is<object>(o => o is JObject && (string?)((JObject)o)["object"] == "{H1}"),
                It.IsAny<object>(), It.IsAny<int>()), Times.Once);
            Client.Verify(c => c.Call(ak.wwise.core.@object.delete,
                It.Is<object>(o => o is JObject && (string?)((JObject)o)["object"] == "{H2}"),
                It.IsAny<object>(), It.IsAny<int>()), Times.Once);
        }
    }
}
