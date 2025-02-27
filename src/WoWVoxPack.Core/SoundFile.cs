using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WoWVoxPack;

public class SoundFile
{
    [JsonConstructor]
    public SoundFile(string fileName, string? text = null, string? ssml = null, string? displayName = null,
        string? formattedDisplayName = null)
    {
        FileName = fileName;
        Text = text;
        Ssml = ssml;
        DisplayName = displayName ?? Path.ChangeExtension(fileName, null);
        FormattedDisplayName = formattedDisplayName ?? DisplayName;
    }

    public string? Text { get; set; }

    [JsonPropertyName("SSML")]
    public string? Ssml { get; set; }

    public string DisplayName { get; set; }

    public string FormattedDisplayName { get; set; }

    [Required]
    public string FileName { get; set; }

    public bool IsSsml => !string.IsNullOrWhiteSpace(Ssml);
}
