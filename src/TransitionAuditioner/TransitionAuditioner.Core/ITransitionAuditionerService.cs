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

        /// <summary>Raised with the current Wwise selection (description and first object id) as it changes.</summary>
        event EventHandler<SelectionInfo> SelectionChanged;

        bool IsConnected { get; }
        bool IsSetUp { get; }
        string? ProjectName { get; }
        string? WwiseVersion { get; }

        /// <summary>How far before each segment's end to place the audition cue, in milliseconds.</summary>
        int AuditionCueOffsetFromEndMs { get; set; }

        /// <summary>How a segment's length is measured when placing the audition cue.</summary>
        SegmentLengthSource LengthSource { get; set; }

        /// <summary>Whether the Music Playlist Editor is opened on reveal (playlist-container targets).</summary>
        bool OpenPlaylistEditor { get; set; }

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

        /// <summary>Selects the copied structure in the Project Explorer (and the Playlist Editor).</summary>
        Task ShowInProjectExplorerAsync();

        /// <summary>Plays the harness through its WAAPI transport, firing the audition transition.</summary>
        Task PlayAsync();

        /// <summary>Stops playback on the harness transport.</summary>
        Task StopAsync();
    }
}
