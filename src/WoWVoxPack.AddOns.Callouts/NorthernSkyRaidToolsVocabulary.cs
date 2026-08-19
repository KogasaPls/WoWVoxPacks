using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.Callouts;

/// <summary>Builds Northern Sky Raid Tools registrations from its literal source-key file.</summary>
public static class NorthernSkyRaidToolsVocabulary
{
    /// <summary>
    /// Reads every vocabulary in order. NSRT's generated file covers what its Media/Sounds
    /// ships; the alerts speak strings it has no file for, and those come from a second,
    /// hand-maintained one.
    /// </summary>
    public static IReadOnlyList<CalloutRegistration> Load(
        IEnumerable<string> vocabularyPaths,
        IReadOnlyDictionary<string, PronunciationOverride> overrides) =>
        CreateRegistrations(
            vocabularyPaths.SelectMany(path => Parse(File.ReadLines(path))), overrides);

    /// <summary>
    /// Keeps each source key exactly as written. Whitespace is inspected only to recognize a
    /// blank or comment-only line; it is never removed from a media key.
    /// </summary>
    public static IReadOnlyList<string> Parse(IEnumerable<string> lines) =>
        lines
            .Where(line => !string.IsNullOrWhiteSpace(line)
                           && !line.TrimStart().StartsWith('#'))
            .ToList();

    private static IReadOnlyList<CalloutRegistration> CreateRegistrations(
        IEnumerable<string> mediaKeys,
        IReadOnlyDictionary<string, PronunciationOverride> overrides)
    {
        // Case-only source-key variants are alternate spellings for the same NSRT sound and
        // should render one recording. Keep whitespace and punctuation significant: those are
        // literal media keys, not normalized names, and must not accidentally share audio.
        Dictionary<string, SoundFile> soundFilesByMediaKey = new(StringComparer.OrdinalIgnoreCase);
        List<CalloutRegistration> registrations = [];

        foreach (string mediaKey in mediaKeys)
        {
            SoundFile generated = DescribeSoundFile(mediaKey, overrides);
            if (!soundFilesByMediaKey.TryGetValue(mediaKey, out SoundFile? soundFile))
            {
                soundFile = generated;
                soundFilesByMediaKey.Add(mediaKey, soundFile);
            }

            registrations.Add(new CalloutRegistration(soundFile, [mediaKey]));
        }

        return registrations;
    }

    private static SoundFile DescribeSoundFile(
        string mediaKey,
        IReadOnlyDictionary<string, PronunciationOverride> overrides)
    {
        overrides.TryGetValue(mediaKey, out PronunciationOverride? @override);

        string displayName = @override?.Text is { } overrideText
            ? overrideText.Split('=')[0]
            : CalloutPronunciation.ToDisplayName(mediaKey);
        string? text = @override?.Ssml is null ? @override?.Text ?? displayName : null;
        string? ssml = @override?.Ssml;

        if (ssml is null && text?.Contains('=') == true)
        {
            ssml = SoundFile.GetSsml(text);
            text = null;
        }

        return new SoundFile(
            @override?.FileName ?? CalloutPronunciation.ToFileName(displayName),
            text: text,
            ssml: ssml,
            displayName: displayName);
    }
}
