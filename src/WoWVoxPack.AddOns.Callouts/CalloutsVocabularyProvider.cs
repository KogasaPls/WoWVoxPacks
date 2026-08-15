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
