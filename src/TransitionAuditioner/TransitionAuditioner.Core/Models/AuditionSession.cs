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

        /// <summary>Id (GUID) of the throwaway Work Unit in the Interactive Music Hierarchy.</summary>
        public string TempWorkUnitId { get; set; } = string.Empty;

        /// <summary>Id of the Music Switch Container scaffolded around the copy.</summary>
        public string SwitchContainerId { get; set; } = string.Empty;

        /// <summary>Id of the copied structure that lives inside the temp Work Unit.</summary>
        public string CopyId { get; set; } = string.Empty;

        /// <summary>Transport object id, if one was created for auditioning.</summary>
        public int? TransportId { get; set; }

        /// <summary>True once the transition rule was configured without error.</summary>
        public bool TransitionRuleConfigured { get; set; }

        /// <summary>Custom cues discovered under the target, for reporting/diagnostics.</summary>
        public List<MusicObjectInfo> CustomCues { get; set; } = new List<MusicObjectInfo>();
    }
}
