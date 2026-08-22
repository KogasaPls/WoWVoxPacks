using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WoWVoxPack.TTS;

public class SoundFile
{
    [JsonConstructor]
    public SoundFile(string fileName, string? text = null, string? ssml = null, string? displayName = null,
        string? formattedDisplayName = null, IReadOnlyList<Pronunciation>? pronunciations = null)
    {
        FileName = Path.ChangeExtension(fileName, ".ogg").ToLowerInvariant();
        Text = text;
        Ssml = ssml;
        Pronunciations = pronunciations is { Count: > 0 } ? pronunciations : null;
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

    [JsonPropertyName("Pronunciations")]
    [JsonPropertyOrder(0)]
    public IReadOnlyList<Pronunciation>? Pronunciations { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public string? CopyFromPath { get; set; }

    /// <summary>
    /// Drops the "=IPA" hint upstream spell lists and the repo's own manifests attach to a word,
    /// leaving the plain spelling.
    /// </summary>
    public static string StripIpaHints(string text)
    {
        return !text.Contains('=')
            ? text
            : string.Join(' ', text.Split(' ').Select(word => word.Split('=')[0]));
    }

    /// <summary>
    /// Splits the repo's "Word=IPA" convention into what is said and how to say it. The hint has
    /// to leave the text: Google applies a custom pronunciation by matching the phrase in the
    /// input, and a phrase inside a phoneme tag is documented not to match.
    /// </summary>
    public static (string Text, IReadOnlyList<Pronunciation> Pronunciations) ParseIpaHints(string markedUpText)
    {
        List<Pronunciation> pronunciations = markedUpText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Split('='))
            .Where(parts => parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
            .Select(parts => new Pronunciation(parts[0], parts[1]))
            .DistinctBy(p => p.Phrase)
            .ToList();

        return (StripIpaHints(markedUpText), pronunciations);
    }
}
