using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WoWVoxPack.TTS;

public class SoundFile
{
    [JsonConstructor]
    public SoundFile(string fileName, string? text = null, string? ssml = null, string? displayName = null,
        string? formattedDisplayName = null)
    {
        FileName = Path.ChangeExtension(fileName, ".ogg");
        Text = text;
        Ssml = ssml;
        DisplayName = displayName ?? Path.ChangeExtension(fileName, null);
        FormattedDisplayName = formattedDisplayName ?? DisplayName;
    }

    [Required]
    [JsonPropertyOrder(-5)]
    public string FileName { get; set; }


    [JsonPropertyName("DisplayName")]
    [JsonPropertyOrder(-4)]
    public string DisplayName { get; set; }

    [JsonPropertyName("FormattedDisplayName")]
    [JsonPropertyOrder(-3)]
    public string FormattedDisplayName { get; set; }

    [JsonPropertyName("Text")]
    [JsonPropertyOrder(-2)]
    public string? Text { get; set; }

    [JsonPropertyName("SSML")]
    [JsonPropertyOrder(-1)]
    public string? Ssml { get; set; }


    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public string? CopyFromPath { get; set; }
}
