using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Xml.Linq;

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

    public static string GetSsml(string text)
    {
        string[] words = text.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<XNode> content = words.Select((word, index) => new
            {
                Word = word + (index == words.Length - 1 ? "" : " "),
                WordIpa = word.Split('=').ElementAtOrDefault(1)
            })
            .Select(x => x.WordIpa is null
                ? new XText(x.Word) as XNode
                : new XElement("phoneme", new XAttribute("alphabet", "IPA"),
                    new XAttribute("ph", x.WordIpa), x.Word));

        return new XDocument(new XElement("speak", content)).ToString().TrimEnd();
    }
}
