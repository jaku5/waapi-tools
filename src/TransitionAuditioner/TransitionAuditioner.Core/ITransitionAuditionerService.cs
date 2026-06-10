using System;
using System.Threading;
using System.Threading.Tasks;
using JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Models;

namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core
{
    public interface ITransitionAuditionerService
    {
        event EventHandler<string> StatusUpdated;
        event EventHandler<string> NotificationRequested;
        event EventHandler Disconnected;

        bool IsConnected { get; }
        bool IsSetUp { get; }
        string? ProjectName { get; }
        string? WwiseVersion { get; }

        /// <summary>The currently set-up audition, or null if none is active.</summary>
        AuditionSession? Session { get; }

        Task ConnectAsync();
        void Disconnect();

        /// <summary>
        /// Reads the current Wwise selection and returns the first object that is a
        /// valid interactive-music audition target, or null if the selection is not usable.
        /// </summary>
        Task<MusicObjectInfo?> GetSelectedTargetAsync();

        /// <summary>
        /// Builds the non-destructive audition scaffolding around a copy of <paramref name="target"/>:
        /// temp Work Unit, copied structure, parent Music Switch Container, transition rule,
        /// and a transport ready for the user to press Play. Throws on failure after rolling back.
        /// </summary>
        Task<AuditionSession> SetUpAuditionAsync(MusicObjectInfo target, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the temp Work Unit (and destroys the transport) created by
        /// <see cref="SetUpAuditionAsync"/>. Safe to call multiple times; never throws.
        /// The project is intentionally not saved.
        /// </summary>
        Task TeardownAsync();

        /// <summary>Brings the copied switch container into view in the Project Explorer.</summary>
        Task ShowInProjectExplorerAsync();
    }
}
