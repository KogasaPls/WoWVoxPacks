using System.Text.Json;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.Callouts;

/// <summary>Loads the curated, player-reminder, and retired Callouts vocabulary.</summary>
public sealed class CalloutsVocabularyProvider
{
    private readonly Lazy<IReadOnlyList<CalloutRegistration>> _registrations;

    public CalloutsVocabularyProvider(
        string curatedJsonPath,
        string overridesPath,
        string playerReminderVocabularyPath,
        string retiredJsonPath)
    {
        _registrations = new Lazy<IReadOnlyList<CalloutRegistration>>(() =>
            CalloutVocabulary.Merge(
                AddOnBuilder.LoadSoundFileJsonWithSsmlFallback(curatedJsonPath),
                CalloutNameVocabulary.Load(playerReminderVocabularyPath),
                CalloutPronunciation.LoadOverrides(overridesPath),
                LoadRetiredNames(retiredJsonPath)));
    }

    public IReadOnlyList<CalloutRegistration> Registrations => _registrations.Value;

    public IEnumerable<SoundFile> SoundFilesFor(string soundDirectory) =>
        Registrations
            .Where(registration =>
                !registration.ReuseExistingAudioOnly
                || File.Exists(Path.Combine(soundDirectory, registration.SoundFile.FileName)))
            .Select(registration => registration.SoundFile);

    private static IReadOnlyList<string> LoadRetiredNames(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? [];
    }
}

/// <summary>Loads only Northern Sky Raid Tools' literal media-key vocabulary.</summary>
public sealed class NorthernSkyRaidToolsVocabularyProvider
{
    private readonly Lazy<IReadOnlyList<CalloutRegistration>> _registrations;

    public NorthernSkyRaidToolsVocabularyProvider(string vocabularyPath, string overridesPath)
    {
        _registrations = new Lazy<IReadOnlyList<CalloutRegistration>>(() =>
            NorthernSkyRaidToolsVocabulary.Load(
                vocabularyPath,
                CalloutPronunciation.LoadOverrides(overridesPath)));
    }

    public IReadOnlyList<CalloutRegistration> Registrations => _registrations.Value;

    /// <summary>
    /// One output file is rendered per file name even when multiple literal media keys use it.
    /// </summary>
    public IEnumerable<SoundFile> SoundFiles =>
        Registrations
            .Select(registration => registration.SoundFile)
            .DistinctBy(soundFile => soundFile.FileName, StringComparer.OrdinalIgnoreCase);
}
