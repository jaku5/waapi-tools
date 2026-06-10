namespace JPAudio.WaapiTools.Tool.TransitionAuditioner.Core.Models
{
    /// <summary>
    /// A resolved interactive-music object (the target the user selected, or a
    /// playlist item / custom cue discovered underneath it).
    /// </summary>
    public class MusicObjectInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }
}
