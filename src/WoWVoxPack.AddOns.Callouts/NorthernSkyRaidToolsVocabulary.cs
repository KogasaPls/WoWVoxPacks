using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.Callouts;

/// <summary>Builds Northern Sky Raid Tools registrations from its literal source-key file.</summary>
public static class NorthernSkyRaidToolsVocabulary
{
    /// <summary>
    /// The first two are generated: what NSRT's Media/Sounds ships, then the alert strings it
    /// has no file for. Strings its code composes at runtime are hand-enumerated in the third.
    /// </summary>
    public static readonly IReadOnlyList<string> VocabularyFileNames =
        ["nsrt-vocabulary.txt", "nsrt-alert-vocabulary.txt", "nsrt-extra-vocabulary.txt"];

    public static IReadOnlyList<CalloutRegistration> Load(
        IEnumerable<string> vocabularyPaths,
        IReadOnlyDictionary<string, PronunciationOverride> overrides,
        IEnumerable<CalloutRegistration>? calloutRegistrations = null)
    {
        IReadOnlyList<CalloutRegistration> registrations = CreateRegistrations(
            vocabularyPaths.SelectMany(path => Parse(File.ReadLines(path))), overrides);

        return calloutRegistrations is null
            ? registrations
            : [.. registrations, .. FoldInCallouts(registrations, calloutRegistrations)];
    }

    /// <summary>
    /// Keeps each source key exactly as written. Whitespace is inspected only to recognize a
    /// blank or comment-only line; it is never removed from a media key.
    /// </summary>
    public static IReadOnlyList<string> Parse(IEnumerable<string> lines) =>
        lines
            .Where(line => !string.IsNullOrWhiteSpace(line)
                           && !line.TrimStart().StartsWith('#'))
            .ToList();

    /// <summary>
    /// Key dedup is case-insensitive because NSRT's sound lookup is (its cache lowercases every
    /// key). Reuse-only registrations fold in with their flag intact: whether their recording is
    /// still present in this pack's sound directory is decided at build time, exactly as the
    /// Callouts pack decides it.
    /// </summary>
    private static IEnumerable<CalloutRegistration> FoldInCallouts(
        IReadOnlyList<CalloutRegistration> registrations,
        IEnumerable<CalloutRegistration> calloutRegistrations)
    {
        HashSet<string> registered = new(
            registrations.SelectMany(registration => registration.MediaKeys),
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, SoundFile> nativeByFileName = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, SoundFile> nativeByKey = new(StringComparer.OrdinalIgnoreCase);
        foreach (CalloutRegistration registration in registrations)
        {
            nativeByFileName.TryAdd(registration.SoundFile.FileName, registration.SoundFile);
            nativeByKey.TryAdd(registration.SoundFile.Key, registration.SoundFile);
        }

        foreach (CalloutRegistration callout in calloutRegistrations)
        {
            List<string> mediaKeys = callout.MediaKeys
                .Union([callout.SoundFile.DisplayName], StringComparer.OrdinalIgnoreCase)
                .Where(registered.Add)
                .ToList();

            if (mediaKeys.Count == 0)
            {
                continue;
            }

            if (nativeByFileName.TryGetValue(callout.SoundFile.FileName, out SoundFile? native)
                && (native.Text != callout.SoundFile.Text || native.Ssml != callout.SoundFile.Ssml))
            {
                throw new InvalidOperationException(
                    $"'{callout.SoundFile.Key}' from Callouts and '{native.Key}' from the Northern "
                    + $"Sky Raid Tools vocabulary both render {callout.SoundFile.FileName} with "
                    + "different content; add a pronunciation override or file name to separate them.");
            }

            // AddOnBuilder dedupes sound files by Key, so a folded Key an NSRT entry already owns
            // would ship the native recording while this registration's own file is never
            // rendered, leaving its Lua keys pointing at a path that does not exist.
            if (nativeByKey.TryGetValue(callout.SoundFile.Key, out SoundFile? sameKey)
                && !string.Equals(sameKey.FileName, callout.SoundFile.FileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"'{callout.SoundFile.Key}' from Callouts shares its key with a Northern Sky "
                    + $"Raid Tools entry but renders {callout.SoundFile.FileName} instead of "
                    + $"{sameKey.FileName}; align the file names or rename one side.");
            }

            yield return callout with { MediaKeys = mediaKeys };
        }
    }

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

        string displayName = CalloutPronunciation.ToDisplayName(mediaKey);
        string? text = @override?.Ssml is null ? @override?.Text ?? displayName : null;
        string? ssml = @override?.Ssml;
        IReadOnlyList<Pronunciation> pronunciations = @override?.Pronunciations ?? [];

        if (ssml is null && text?.Contains('=') == true)
        {
            (text, IReadOnlyList<Pronunciation> lifted) = SoundFile.ParseIpaHints(text);
            pronunciations = pronunciations.Count > 0 ? pronunciations : lifted;
        }

        return new SoundFile(
            @override?.FileName ?? CalloutPronunciation.ToFileName(displayName),
            text: text,
            ssml: ssml,
            displayName: displayName,
            pronunciations: pronunciations);
    }
}
