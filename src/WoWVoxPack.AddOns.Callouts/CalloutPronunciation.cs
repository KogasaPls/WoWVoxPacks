using System.Text.Json;
using System.Text.RegularExpressions;

namespace WoWVoxPack.AddOns.Callouts;

/// <summary>
/// Turns an upstream sound name into spoken text and a file name. Some names are PascalCase file
/// stems (<c>MindControl</c>) or bare digits, and neither reads well synthesised verbatim.
/// </summary>
public static partial class CalloutPronunciation
{
    private static readonly string[] NumberWords =
        ["Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten"];

    public static string ToDisplayName(string soundName)
    {
        if (int.TryParse(soundName, out int number) && number >= 0 && number < NumberWords.Length)
        {
            return NumberWords[number];
        }

        return PascalCaseBoundary().Replace(soundName, " ");
    }

    public static string ToFileName(string displayName)
    {
        string slug = NonAlphanumeric().Replace(displayName.ToLowerInvariant(), "_").Trim('_');
        return $"{slug}.ogg";
    }

    public static IReadOnlyDictionary<string, PronunciationOverride> LoadOverrides(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, PronunciationOverride>(StringComparer.OrdinalIgnoreCase);
        }

        Dictionary<string, PronunciationOverride> parsed =
            JsonSerializer.Deserialize<Dictionary<string, PronunciationOverride>>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        return new Dictionary<string, PronunciationOverride>(parsed, StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex PascalCaseBoundary();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
