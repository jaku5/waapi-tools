namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core
{
    /// <summary>
    /// How a segment's length is measured when placing the audition cue.
    /// </summary>
    public enum SegmentLengthSource
    {
        /// <summary>
        /// The Exit cue position (where the source exits / the transition fires). The default,
        /// and the most correct boundary for auditioning a transition. Falls back to Segment end
        /// if the Exit cue cannot be read.
        /// </summary>
        ExitCue,

        /// <summary>The segment's <c>@EndPosition</c> — the end of content, including any post-exit tail.</summary>
        SegmentEnd,

        /// <summary>The trimmed duration of the segment's longest audio source.</summary>
        AudioLength,
    }
}
