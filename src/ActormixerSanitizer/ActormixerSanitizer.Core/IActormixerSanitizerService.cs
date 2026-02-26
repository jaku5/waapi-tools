using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core.Models;

namespace JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core
{
    public interface IActormixerSanitizerService
    {
        event EventHandler<string> StatusUpdated;
        event EventHandler<string> NotificationRequested;
        event EventHandler Disconnected;
        event EventHandler ProjectStateChanged;

        bool IsDirty { get; }
        bool IsSaved { get; }
        bool IsConverted { get; }
        bool IsConnectionLost { get; }
        bool IsScanned { get; }
        bool IsConverting { get; }
        bool IsScanning { get; }
        string ProjectName { get; }
        string WwiseVersion { get; }
        string ActorMixerName { get; }
        string ActorMixerNamePlural { get; }

        Task SubscribeToChangesAsync();
        Task UnsubscribeFromChangesAsync();
        void Disconnect();
        Task ConnectAsync();
        Task<List<ActorMixerInfo>> GetSanitizableMixersAsync(Action<int, int>? progressCallback = null, System.Threading.CancellationToken cancellationToken = default);
        Task<bool> CheckProjectStateAsync();
        Task ConvertToFoldersAsync(List<ActorMixerInfo> actors, Action<int, int>? progressCallback = null, System.Threading.CancellationToken cancellationToken = default);
        Task SelectInProjectExplorer(string actorId);
        Task ShowInListView(List<ActorMixerInfo> actors);
    }
}
