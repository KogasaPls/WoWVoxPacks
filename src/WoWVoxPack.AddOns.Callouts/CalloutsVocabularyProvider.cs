using System.Text.Json;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.Callouts;

/// <summary>
/// Loads the curated, tracked-vocabulary, and retired Callouts entries. Every tracked file goes
/// through the same derivation as the Northern Sky Raid Tools vocabulary, so a name both packs
/// carry renders one identical recording and folds in without contest.
/// </summary>
public sealed class CalloutsVocabularyProvider
{
    private readonly Lazy<IReadOnlyList<CalloutRegistration>> _registrations;

    public CalloutsVocabularyProvider(
        string curatedJsonPath,
        string overridesPath,
        IReadOnlyList<string> vocabularyPaths,
        string retiredJsonPath)
    {
        _registrations = new Lazy<IReadOnlyList<CalloutRegistration>>(() =>
            CalloutVocabulary.Merge(
                AddOnBuilder.LoadSoundFileJsonWithIpaHints(curatedJsonPath),
                vocabularyPaths.SelectMany(CalloutNameVocabulary.Load),
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
