using System.Collections.Generic;

namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Models
{
    /// <summary>
    /// Tracks everything the tool created for one audition so teardown can undo it
    /// cleanly. The temp Work Unit is the single object whose deletion removes the
    /// whole scaffolding; the other fields are kept for status reporting and so the
    /// transport can be destroyed first.
    /// </summary>
    public class AuditionSession
    {
        /// <summary>The original object the user selected (never mutated).</summary>
        public MusicObjectInfo Target { get; set; } = new MusicObjectInfo();

        /// <summary>
        /// Id of the Music Switch Container harness, built in the target's Work Unit. Deleting it
        /// removes the whole scaffolding (copy, cues, transition rule) on teardown.
        /// </summary>
        public string SwitchContainerId { get; set; } = string.Empty;

        /// <summary>Id of the copied structure that lives inside the temp Work Unit.</summary>
        public string CopyId { get; set; } = string.Empty;

        /// <summary>Transport object id, if one was created for auditioning.</summary>
        public int? TransportId { get; set; }

        /// <summary>True once the transition rule was configured without error.</summary>
        public bool TransitionRuleConfigured { get; set; }

            /// <summary>Id of the None-&gt;target MusicTransition rule created on the harness.</summary>
        public string TransitionId { get; set; } = string.Empty;

        /// <summary>
        /// Id of the Music Playlist Editor view the tool opened (empty if none was opened, or one
        /// was already open). Closed on teardown so the tool doesn't leave a view it created behind.
        /// </summary>
        public string CreatedPlaylistViewId { get; set; } = string.Empty;

        /// <summary>Custom cues discovered under the target, for reporting/diagnostics.</summary>
        public List<MusicObjectInfo> CustomCues { get; set; } = new List<MusicObjectInfo>();
    }
}
