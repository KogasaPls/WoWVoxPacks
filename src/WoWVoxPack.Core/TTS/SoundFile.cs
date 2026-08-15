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
        FileName = Path.ChangeExtension(fileName, ".ogg").ToLowerInvariant();
        Text = text;
        Ssml = ssml;
        DisplayName = displayName ?? Path.ChangeExtension(fileName, null);
        FormattedDisplayName = formattedDisplayName ?? DisplayName;
    }

    [Required]
    [JsonPropertyOrder(-5)]
    public string FileName { get; set; }

    /// <summary>
    /// Set only when the display name is not what identifies the sound. BigWigs_Voice sets it to
    /// the spell ID: several spells share a name, and the ID is what names the file and what the
    /// game looks up.
    /// </summary>
    [JsonPropertyName("Key")]
    [JsonPropertyOrder(-6)]
    public string? ExplicitKey { get; set; }

    /// <summary>
    /// What makes two entries the same sound across builds: the dictionary key an addon is
    /// assembled under, and the manifest key a rendered file is remembered under. Defaults to
    /// the display name, which is also the LibSharedMedia key of the SharedMedia addons.
    /// </summary>
    [JsonIgnore]
    public string Key => ExplicitKey ?? DisplayName;

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
                Word = word + (index == words.Length - 1 ? string.Empty : " "),
                WordIpa = word.Split('=').ElementAtOrDefault(1)
            })
            .Select(x => x.WordIpa is null
                ? new XText(x.Word) as XNode
                : new XElement("phoneme", new XAttribute("alphabet", "IPA"),
                    new XAttribute("ph", x.WordIpa), x.Word));

        return new XDocument(new XElement("speak", content)).ToString().TrimEnd();
    }
}
