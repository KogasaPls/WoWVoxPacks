using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BigWigsVoiceGCP.AddOns.SharedMedia_Causese;

public class SoundFile
{

    public SoundFile()
    {
    }

    [JsonConstructor]
    public SoundFile(string? text, string? ssml, string displayName, string formattedDisplayName, string fileName)
    {
        Text = text;
        SSML = ssml;
        DisplayName = displayName;
        FormattedDisplayName = formattedDisplayName;
        FileName = fileName;
    }

    public string? Text { get; set; }

    public string? SSML
    {
        get;
        set;
    }

    public string DisplayName { get; set; }

    public string FormattedDisplayName { get; set; }
    [Required] public string FileName { get; set; }
}
