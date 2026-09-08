using System.Text.Json;
using System.Text.RegularExpressions;

using WoWVoxPack.TTS;

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

    /// <summary>The complete recording description derived from one upstream media key.</summary>
    public static SoundFile DescribeSoundFile(
        string mediaKey,
        IReadOnlyDictionary<string, PronunciationOverride> overrides)
    {
        overrides.TryGetValue(mediaKey, out PronunciationOverride? @override);
        string displayName = ToDisplayName(mediaKey);
        if (@override?.Composition is { } composition)
        {
            return new SoundFile(
                @override.FileName ?? ToFileName(displayName),
                displayName: displayName,
                composition: new SoundComposition(
                    composition.Duration,
                    composition.Parts
                        .Select(part => new SoundCompositionPart(FileNameFor(part.Key, overrides), part.At))
                        .ToArray()))
            {
                PronunciationName = mediaKey
            };
        }

        string? text = @override?.Ssml is null ? @override?.Text ?? displayName : null;

        return new SoundFile(
            @override?.FileName ?? ToFileName(displayName),
            text: text,
            ssml: @override?.Ssml,
            displayName: displayName)
        {
            PronunciationName = mediaKey
        };
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

    /// <summary>
    /// The file a part plays, spelled the way <see cref="SoundFile"/> spells it. A part that is
    /// itself composed is refused: the build composes everything in one pass after rendering.
    /// </summary>
    private static string FileNameFor(string mediaKey, IReadOnlyDictionary<string, PronunciationOverride> overrides)
    {
        overrides.TryGetValue(mediaKey, out PronunciationOverride? @override);
        if (@override?.Composition is not null)
        {
            throw new InvalidOperationException(
                $"'{mediaKey}' is composed, so it cannot be a part of another composition.");
        }

        return Path.ChangeExtension(@override?.FileName ?? ToFileName(ToDisplayName(mediaKey)), ".ogg")
            .ToLowerInvariant();
    }

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex PascalCaseBoundary();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
