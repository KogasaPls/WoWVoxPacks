using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.Callouts;

/// <summary>
/// Merges curated <c>Callouts_Sounds.json</c> entries, a tracked source vocabulary, and
/// <c>RetiredCallouts.json</c> into one list, so sound rendering and Lua generation cannot drift
/// apart. Names explicitly moved to the retired list keep resolving after an upstream removes
/// or renames them.
/// </summary>
public static class CalloutVocabulary
{
    /// <summary>
    /// The tracked name files: the generated player-reminder spell list, and the
    /// hand-maintained generic mechanics and instructions.
    /// </summary>
    public static readonly IReadOnlyList<string> VocabularyFileNames =
        ["lorrgs-vocabulary.txt", "callout-vocabulary.txt"];

    public static IReadOnlyList<CalloutRegistration> Merge(
        IEnumerable<SoundFile> curated,
        IEnumerable<string> upstreamSoundNames,
        IReadOnlyDictionary<string, PronunciationOverride> overrides,
        IEnumerable<string>? retiredSoundNames = null)
    {
        Dictionary<string, CalloutRegistration> merged = new(StringComparer.OrdinalIgnoreCase);

        foreach (SoundFile soundFile in curated)
        {
            merged[soundFile.DisplayName] = new CalloutRegistration(soundFile, []);
        }

        foreach (string soundName in upstreamSoundNames)
        {
            AddFromName(merged, soundName, overrides, honorExclude: true, reuseExistingAudioOnly: false);
        }

        // Once a key has shipped, an upstream dropping the source name must not stop it resolving:
        // BigWigs and WeakAuras keep the key string in saved profiles,
        // and once it stops being registered Fetch(..., true) returns nil and they play
        // nothing, with no message. Reprocessing the retired name through the exact same
        // pipeline reproduces the same FileName/Text/SSML it had while live. The registration
        // is marked reuse-only so the media service includes it when that recording is still on
        // disk and skips it rather than synthesising it when the recording is genuinely gone.
        // honorExclude is false: an Exclude override must not undo a key that already shipped.
        List<string> retired = (retiredSoundNames ?? []).ToList();

        foreach (string soundName in retired)
        {
            AddFromName(merged, soundName, overrides, honorExclude: false, reuseExistingAudioOnly: true);
        }

        return [.. merged.Values, .. Revive(merged.Values, retired, overrides)];
    }

    /// <summary>
    /// <para>
    /// <c>merged</c> is keyed case-insensitively so a curated entry and its upstream counterpart
    /// collapse. LibSharedMedia keys are plain Lua strings and therefore case-sensitive, so a
    /// case-only upstream rename (<c>Soak</c> to <c>soak</c>) would otherwise drop the shipped
    /// key: the retired name folds into the live slot, the entry count never moves, and nothing
    /// fails. That is exactly the case retirement exists to cover.
    /// </para>
    /// <para>
    /// So the shipped display names are also tracked ordinally, and a retired name missing from
    /// that set gets its own registration. It reuses the live entry's <c>FileName</c>, so no
    /// audio is re-synthesised, and carries a distinct <c>ExplicitKey</c> because the addon
    /// builder and the manifest both key sounds case-insensitively too. Its media keys are empty
    /// on purpose: the live entry already absorbed the retired source name.
    /// </para>
    /// </summary>
    private static IEnumerable<CalloutRegistration> Revive(
        IEnumerable<CalloutRegistration> merged,
        IEnumerable<string> retiredSoundNames,
        IReadOnlyDictionary<string, PronunciationOverride> overrides)
    {
        HashSet<string> shipped =
            new(merged.Select(r => r.SoundFile.DisplayName), StringComparer.Ordinal);

        foreach (string soundName in retiredSoundNames)
        {
            (string displayName, SoundFile soundFile, _) = Describe(soundName, overrides);

            if (!shipped.Add(displayName))
            {
                continue;
            }

            soundFile.ExplicitKey = $"retired:{displayName}";
            yield return new CalloutRegistration(soundFile, [], ReuseExistingAudioOnly: true);
        }
    }

    private static void AddFromName(
        Dictionary<string, CalloutRegistration> merged,
        string soundName,
        IReadOnlyDictionary<string, PronunciationOverride> overrides,
        bool honorExclude,
        bool reuseExistingAudioOnly)
    {
        if (honorExclude
            && overrides.TryGetValue(soundName, out PronunciationOverride? @override)
            && @override.Exclude)
        {
            return;
        }

        (string displayName, SoundFile soundFile, IReadOnlyList<string> mediaKeys) =
            Describe(soundName, overrides);

        if (merged.TryGetValue(displayName, out CalloutRegistration? existing))
        {
            merged[displayName] = existing with
            {
                MediaKeys = existing.MediaKeys.Union(mediaKeys, StringComparer.Ordinal).ToList()
            };
            return;
        }

        merged[displayName] =
            new CalloutRegistration(soundFile, mediaKeys, reuseExistingAudioOnly);
    }

    /// <summary>
    /// The whole of what an upstream sound name becomes. Pure, so a name reprocessed after
    /// retirement reproduces byte-for-byte what it produced while live.
    /// </summary>
    private static (string DisplayName, SoundFile SoundFile, IReadOnlyList<string> MediaKeys) Describe(
        string soundName,
        IReadOnlyDictionary<string, PronunciationOverride> overrides)
    {
        overrides.TryGetValue(soundName, out PronunciationOverride? @override);

        // The media key, never the override's text: a retired alias and its replacement share
        // one recording and must still register under their own LibSharedMedia names.
        string displayName = CalloutPronunciation.ToDisplayName(soundName);

        string? text = @override?.Ssml is null ? @override?.Text ?? displayName : null;

        string? ssml = @override?.Ssml;
        IReadOnlyList<Pronunciation> pronunciations = @override?.Pronunciations ?? [];
        if (ssml is null && text?.Contains('=') == true)
        {
            (text, IReadOnlyList<Pronunciation> lifted) = SoundFile.ParseIpaHints(text);
            pronunciations = pronunciations.Count > 0 ? pronunciations : lifted;
        }

        SoundFile soundFile = new(
            @override?.FileName ?? CalloutPronunciation.ToFileName(displayName),
            text: text,
            ssml: ssml,
            displayName: displayName,
            pronunciations: pronunciations);

        return (displayName, soundFile, [soundName]);
    }
}
