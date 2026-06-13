namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Models
{
    /// <summary>
    /// A snapshot of the current Wwise selection: a human-readable description plus the
    /// id of the first selected object (empty when nothing is selected).
    /// </summary>
    public class SelectionInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// True when the first selected object can be pulled as a target: it is an interactive-music
        /// type and is not the tool's own temporary harness. Used to enable the Pull Selection button.
        /// </summary>
        public bool IsAuditionable { get; set; }
    }
}
