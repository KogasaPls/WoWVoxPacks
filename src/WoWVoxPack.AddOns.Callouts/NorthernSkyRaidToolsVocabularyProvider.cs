using System.Text.Json;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.Callouts;

/// <summary>Loads the Northern Sky Raid Tools vocabulary, folding in Callouts when given.</summary>
public sealed class NorthernSkyRaidToolsVocabularyProvider
{
    private readonly Lazy<IReadOnlyList<CalloutRegistration>> _registrations;

    public NorthernSkyRaidToolsVocabularyProvider(IReadOnlyList<string> vocabularyPaths,
        string overridesPath, CalloutsVocabularyProvider? callouts = null)
    {
        _registrations = new Lazy<IReadOnlyList<CalloutRegistration>>(() =>
            NorthernSkyRaidToolsVocabulary.Load(
                vocabularyPaths,
                CalloutPronunciation.LoadOverrides(overridesPath),
                callouts?.Registrations));
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
