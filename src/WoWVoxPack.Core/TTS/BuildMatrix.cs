using System.ComponentModel.DataAnnotations;

namespace WoWVoxPack.TTS;

public class BuildMatrix
{
    [Required]
    public List<TtsSettings> TtsSettings { get; set; } = null!;
}
