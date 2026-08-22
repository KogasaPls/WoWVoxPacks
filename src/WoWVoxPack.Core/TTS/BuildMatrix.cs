using System.ComponentModel.DataAnnotations;

namespace WoWVoxPack.TTS;

public class BuildMatrix
{
    [Required]
    public List<TtsSettings> TtsSettings { get; set; } = null!;

    /// <summary>
    /// Report what the run would render and remove, then stop without calling the paid API or
    /// writing anything. Set with <c>--Matrix:DryRun true</c>.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Let a build re-render most of a pack. Off by default so a change to a field the manifest
    /// compares cannot quietly bill for the whole vocabulary. Set with
    /// <c>--Matrix:AllowFullRerender true</c>.
    /// </summary>
    public bool AllowFullRerender { get; set; }
}
