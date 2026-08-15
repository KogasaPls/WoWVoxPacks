using System.Text.Json;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.Callouts;

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
