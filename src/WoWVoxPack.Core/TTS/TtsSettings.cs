using System.ComponentModel.DataAnnotations;

namespace WoWVoxPack.TTS;

public class TtsSettings
{
    [Required]
    public VoiceName? Voice { get; set; }

    public string LanguageCode { get; set; } = "en-US";
    public float SpeakingRate { get; set; } = 1;
    public float Pitch { get; set; } = 0;
    public int SampleRateHertz { get; set; } = 44100;
}
