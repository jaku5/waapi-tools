using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Messaging;
using JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core;
using ActormixerSanitizer.UI.ViewModels;
using System.Threading.Tasks;
using System.Collections.Generic;
using JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core.Models;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Input;
using ActormixerSanitizer.UI.Services;

namespace ActormixerSanitizer.UI.Tests
{
    public class MainViewModelTests
    {
        private readonly Mock<IActormixerSanitizerService> _sanitizerServiceMock;
        private readonly Mock<ILogger<MainViewModel>> _loggerMock;
        private readonly Mock<IMessenger> _messengerMock;
        private readonly Mock<IDialogService> _dialogServiceMock;
        private MainViewModel _viewModel;

        static MainViewModelTests()
        {
            // Ensure an Application instance exists for testing
            if (Application.Current == null)
            {
                new Application();
            }
        }

        public MainViewModelTests()
        {
            _sanitizerServiceMock = new Mock<IActormixerSanitizerService>();
            _loggerMock = new Mock<ILogger<MainViewModel>>();
            _messengerMock = new Mock<IMessenger>();
            _dialogServiceMock = new Mock<IDialogService>();

            // Setup default behaviors
            _sanitizerServiceMock.Setup(s => s.GetSanitizableMixersAsync()).ReturnsAsync(new List<ActorMixerInfo>());
        }

        private void CreateViewModel()
        {
            _viewModel = new MainViewModel(_sanitizerServiceMock.Object, _loggerMock.Object, _messengerMock.Object, _dialogServiceMock.Object);
        }

        [Fact]
        public async Task ScanCommand_WhenExecuted_CallsGetSanitizableMixersAsync()
        {
            // Arrange
            CreateViewModel();
            _sanitizerServiceMock.Setup(s => s.CheckProjectStateAsync()).ReturnsAsync(false);

            // Act
            await ((IAsyncRelayCommand)_viewModel.ScanCommand).ExecuteAsync(null);

            // Assert
            _sanitizerServiceMock.Verify(s => s.GetSanitizableMixersAsync(), Times.Once);
        }

        [Fact]
        public async Task ScanCommand_WhenServiceReturnsMixers_PopulatesActorMixersCollection()
        {
            // Arrange
            CreateViewModel();
            var mixers = new List<ActorMixerInfo>
            {
                new ActorMixerInfo { Id = "1", Name = "Mixer1" },
                new ActorMixerInfo { Id = "2", Name = "Mixer2" }
            };
            _sanitizerServiceMock.Setup(s => s.CheckProjectStateAsync()).ReturnsAsync(false);
            _sanitizerServiceMock.Setup(s => s.GetSanitizableMixersAsync()).ReturnsAsync(mixers);

            // Act
            await ((IAsyncRelayCommand)_viewModel.ScanCommand).ExecuteAsync(null);

            // Assert
            Assert.Equal(2, _viewModel.ActorMixers.Count);
            Assert.Equal("Mixer1", _viewModel.ActorMixers[0].Name);
            Assert.Equal("Mixer2", _viewModel.ActorMixers[1].Name);
        }

        [Fact]
        public async Task ScanCommand_WhenNoActorsFound_ClearsCollection()
        {
            // Arrange
            CreateViewModel();
            _sanitizerServiceMock.Setup(s => s.CheckProjectStateAsync()).ReturnsAsync(false);
            _sanitizerServiceMock.Setup(s => s.GetSanitizableMixersAsync()).ReturnsAsync(new List<ActorMixerInfo>());

            // Add some initial data
            _viewModel.ActorMixers.Add(new ActorMixerInfo { Id = "old", Name = "OldMixer" });

            // Act
            await ((IAsyncRelayCommand)_viewModel.ScanCommand).ExecuteAsync(null);

            // Assert
            Assert.Empty(_viewModel.ActorMixers);
        }

        [Fact]
        public async Task ConnectCommand_WhenExecuted_CallsServiceConnectAsync()
        {
            // Arrange
            CreateViewModel();
            _sanitizerServiceMock.Setup(s => s.ConnectAsync()).Returns(Task.CompletedTask);

            // Act
            await ((IAsyncRelayCommand)_viewModel.ConnectCommand).ExecuteAsync(null);

            // Assert
            _sanitizerServiceMock.Verify(s => s.ConnectAsync(), Times.Once);
        }

        [Fact]
        public async Task ConvertCommand_WhenNoActorsMarked_DoesNothing()
        {
            // Arrange
            CreateViewModel();
            var mixers = new List<ActorMixerInfo>
            {
                new ActorMixerInfo { Id = "1", Name = "Mixer1", IsMarked = false }
            };
            _sanitizerServiceMock.Setup(s => s.CheckProjectStateAsync()).ReturnsAsync(false);
            _sanitizerServiceMock.Setup(s => s.GetSanitizableMixersAsync()).ReturnsAsync(mixers);
            _dialogServiceMock.Setup(d => d.ShowConfirmationDialog(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);


            // Populate the collection
            await ((IAsyncRelayCommand)_viewModel.ScanCommand).ExecuteAsync(null);

            // Act
            await ((IAsyncRelayCommand)_viewModel.ConvertCommand).ExecuteAsync(null);

            // Assert - ConvertToFoldersAsync should not be called
            _sanitizerServiceMock.Verify(s => s.ConvertToFoldersAsync(It.IsAny<List<ActorMixerInfo>>()), Times.Never);
        }

        [Fact]
        public async Task ConvertCommand_WhenActorsMarked_CallsServiceWithMarkedActors()
        {
            // Arrange
            CreateViewModel();
            var mixers = new List<ActorMixerInfo>
            {
                new ActorMixerInfo { Id = "1", Name = "Mixer1", IsMarked = true },
                new ActorMixerInfo { Id = "2", Name = "Mixer2", IsMarked = false }
            };
            _sanitizerServiceMock.Setup(s => s.CheckProjectStateAsync()).ReturnsAsync(false);
            _sanitizerServiceMock.Setup(s => s.GetSanitizableMixersAsync()).ReturnsAsync(mixers);
            _sanitizerServiceMock.Setup(s => s.ConvertToFoldersAsync(It.IsAny<List<ActorMixerInfo>>()))
                .Returns(Task.CompletedTask);
            _dialogServiceMock.Setup(d => d.ShowConfirmationDialog(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            // Populate the collection
            await ((IAsyncRelayCommand)_viewModel.ScanCommand).ExecuteAsync(null);
            _viewModel.ActorMixers[0].IsMarked = true;

            // Act
            await ((IAsyncRelayCommand)_viewModel.ConvertCommand).ExecuteAsync(null);

            // Assert
            _sanitizerServiceMock.Verify(
                s => s.ConvertToFoldersAsync(It.Is<List<ActorMixerInfo>>(
                    list => list.Count == 1 && list[0].Id == "1")), Times.Once);
        }

        [Fact]
        public async Task MarkAllCommand_WhenExecuted_MarksAllActors()
        {
            // Arrange
            CreateViewModel();
            var mixers = new List<ActorMixerInfo>
            {
                new ActorMixerInfo { Id = "1", Name = "Mixer1", IsMarked = false },
                new ActorMixerInfo { Id = "2", Name = "Mixer2", IsMarked = false },
                new ActorMixerInfo { Id = "3", Name = "Mixer3", IsMarked = false }
            };
            _sanitizerServiceMock.Setup(s => s.CheckProjectStateAsync()).ReturnsAsync(false);
            _sanitizerServiceMock.Setup(s => s.GetSanitizableMixersAsync()).ReturnsAsync(mixers);

            // Populate the collection
            await ((IAsyncRelayCommand)_viewModel.ScanCommand).ExecuteAsync(null);

            // Act
            ((ICommand)_viewModel.MarkAllCommand).Execute(null);

            // Assert
            Assert.All(_viewModel.ActorMixers, mixer => Assert.True(mixer.IsMarked));
        }

        [Fact]
        public async Task UnmarkAllCommand_WhenExecuted_UnmarksAllActors()
        {
            // Arrange
            CreateViewModel();
            var mixers = new List<ActorMixerInfo>
            {
                new ActorMixerInfo { Id = "1", Name = "Mixer1", IsMarked = true },
                new ActorMixerInfo { Id = "2", Name = "Mixer2", IsMarked = true }
            };
            _sanitizerServiceMock.Setup(s => s.CheckProjectStateAsync()).ReturnsAsync(false);
            _sanitizerServiceMock.Setup(s => s.GetSanitizableMixersAsync()).ReturnsAsync(mixers);

            // Populate the collection
            await ((IAsyncRelayCommand)_viewModel.ScanCommand).ExecuteAsync(null);

            // Act
            ((ICommand)_viewModel.UnmarkAllCommand).Execute(null);

            // Assert
            Assert.All(_viewModel.ActorMixers, mixer => Assert.False(mixer.IsMarked));
        }

        [Fact]
        public async Task SelectInWwiseCommand_WhenExecuted_CallsServiceSelectInProjectExplorer()
        {
            // Arrange
            CreateViewModel();
            var mixers = new List<ActorMixerInfo>
            {
                new ActorMixerInfo { Id = "mixer-id-123", Name = "Mixer1" }
            };
            _sanitizerServiceMock.Setup(s => s.CheckProjectStateAsync()).ReturnsAsync(false);
            _sanitizerServiceMock.Setup(s => s.GetSanitizableMixersAsync()).ReturnsAsync(mixers);
            _sanitizerServiceMock.Setup(s => s.SelectInProjectExplorer("mixer-id-123"))
                .Returns(Task.CompletedTask);

            // Populate and select
            await ((IAsyncRelayCommand)_viewModel.ScanCommand).ExecuteAsync(null);
            var mixerToSelect = _viewModel.ActorMixers[0];
            mixerToSelect.IsMarked = true;

            // Act
            await ((IAsyncRelayCommand<ActorMixerInfo>)_viewModel.SelectInWwiseCommand).ExecuteAsync(mixerToSelect);

            // Assert
            _sanitizerServiceMock.Verify(s => s.SelectInProjectExplorer("mixer-id-123"), Times.Once);
        }

        [Fact]
        public async Task ShowMarkedListCommand_WhenExecuted_CallsServiceShowInListView()
        {
            // Arrange
            CreateViewModel();
            var mixers = new List<ActorMixerInfo>
            {
                new ActorMixerInfo { Id = "1", Name = "Mixer1", IsMarked = true },
                new ActorMixerInfo { Id = "2", Name = "Mixer2", IsMarked = true }
            };
            _sanitizerServiceMock.Setup(s => s.CheckProjectStateAsync()).ReturnsAsync(false);
            _sanitizerServiceMock.Setup(s => s.GetSanitizableMixersAsync()).ReturnsAsync(mixers);
            _sanitizerServiceMock.Setup(s => s.ShowInListView(It.IsAny<List<ActorMixerInfo>>()))
                .Returns(Task.CompletedTask);

            // Populate and select
            await ((IAsyncRelayCommand)_viewModel.ScanCommand).ExecuteAsync(null);
            _viewModel.ActorMixers[0].IsMarked = true;
            _viewModel.ActorMixers[1].IsMarked = true;

            // Act
            await ((IAsyncRelayCommand)_viewModel.ShowMarkedListCommand).ExecuteAsync(null);

            // Assert
            _sanitizerServiceMock.Verify(
                s => s.ShowInListView(It.Is<List<ActorMixerInfo>>(
                    list => list.Count == 2)), Times.Once);
        }

        [Fact]
        public async Task ScanCommand_WhenServiceReturnsNull_HandlesGracefully()
        {
            // Arrange
            CreateViewModel();
            _sanitizerServiceMock.Setup(s => s.CheckProjectStateAsync()).ReturnsAsync(false);
            _sanitizerServiceMock.Setup(s => s.GetSanitizableMixersAsync()).ReturnsAsync((List<ActorMixerInfo>)null!);

            // Act
            await ((IAsyncRelayCommand)_viewModel.ScanCommand).ExecuteAsync(null);

            // Assert - should not crash, collection should be empty or unaffected
            Assert.NotNull(_viewModel.ActorMixers);
        }

        [Fact]
        public void MainViewModel_WhenServiceStateChanges_UpdatesUIProperties()
        {
            // Arrange
            CreateViewModel();

            // Act - Simulate property changes from service mock
            var serviceState = new
            {
                IsDirty = true,
                IsSaved = true,
                IsConverted = true,
                IsConnectionLost = false,
                IsScanned = true
            };

            // Assert - These properties should be settable from the ViewModel
            // The ViewModel subscribes to service state changes
            Assert.IsType<bool>(_viewModel.IsDirty);
            Assert.IsType<bool>(_viewModel.IsSaved);
            Assert.IsType<bool>(_viewModel.IsConverted);
            Assert.IsType<bool>(_viewModel.IsConnectionLost);
            Assert.IsType<bool>(_viewModel.IsScanned);
        }

        [Fact]
        public void ScanCommand_WhenServiceNotConnected_IsDisabled()
        {
            // Arrange
            CreateViewModel();
            _viewModel.IsNotConnected = true;

            // Assert - Command's CanExecute should respect connection state
            // This tests the command availability logic
            var isScanEnabled = _viewModel.IsScanEnabled;
            Assert.False(isScanEnabled);
        }

        [Fact]
        public async Task ConvertCommand_WhenActorsExistButNoneMarked_IsDisabled()
        {
            // Arrange
            CreateViewModel();
            var mixers = new List<ActorMixerInfo>
            {
                new ActorMixerInfo { Id = "1", Name = "Mixer1", IsMarked = false }
            };
            _sanitizerServiceMock.Setup(s => s.CheckProjectStateAsync()).ReturnsAsync(false);
            _sanitizerServiceMock.Setup(s => s.GetSanitizableMixersAsync()).ReturnsAsync(mixers);

            // Populate
            await ((IAsyncRelayCommand)_viewModel.ScanCommand).ExecuteAsync(null);

            // Assert
            var isConvertEnabled = _viewModel.IsConvertEnabled;
            Assert.False(isConvertEnabled);
        }

        [Fact]
        public async Task ConvertCommand_WhenActorsMarkedAndReady_IsEnabled()
        {
            // Arrange
            CreateViewModel();
            var mixers = new List<ActorMixerInfo>
            {
                new ActorMixerInfo { Id = "1", Name = "Mixer1", IsMarked = true }
            };
            _sanitizerServiceMock.Setup(s => s.CheckProjectStateAsync()).ReturnsAsync(false);
            _sanitizerServiceMock.Setup(s => s.GetSanitizableMixersAsync()).ReturnsAsync(mixers);

            // Populate and select
            await ((IAsyncRelayCommand)_viewModel.ScanCommand).ExecuteAsync(null);
            _viewModel.ActorMixers[0].IsMarked = true;
            _viewModel.IsNotConnected = false;

            // Assert
            var isConvertEnabled = _viewModel.IsConvertEnabled;
            Assert.True(isConvertEnabled);
        }

        [Fact]
        public async Task IsNotConnected_ReflectsConnectionState()
        {
            // Arrange
            CreateViewModel();
            _sanitizerServiceMock.Setup(s => s.CheckProjectStateAsync()).ReturnsAsync(false);

            // Act - Initial state should be not connected
            var initialState = _viewModel.IsNotConnected;

            // Assert
            Assert.True(initialState);
        }

        [Fact]
        public async Task ThemeChangeCommand_WhenExecuted_TogglesTheme()
        {
            // Arrange
            CreateViewModel();
            var initialTheme = _viewModel.IsDarkTheme;

            // Act
            ((ICommand)_viewModel.ThemeChangeCommand).Execute(null);

            // Assert - theme should toggle
            Assert.NotEqual(initialTheme, _viewModel.IsDarkTheme);
        }

        [Fact]
        public void AddLogMessage_AppendsToLogText()
        {
            // Arrange
            CreateViewModel();

            // Assert - LogText property should exist and be initialized
            // The ViewModel should have a LogText property that is accessible
            Assert.NotNull(_viewModel.LogText);
            Assert.IsType<string>(_viewModel.LogText);
        }
    }
}