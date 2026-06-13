namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Models
{
    /// <summary>
    /// A Music Segment that received no audition cue because its measured length was not
    /// greater than the chosen offset. Collected during setup so the UI can summarise all
    /// skips in one notification rather than one dialog per segment.
    /// </summary>
    public class SegmentSkip
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>The segment's measured length, in milliseconds, under the chosen length basis.</summary>
        public double MeasuredLengthMs { get; set; }
    }
}
