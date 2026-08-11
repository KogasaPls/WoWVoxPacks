using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.Callouts;

/// <summary>Builds a self-contained Northern Sky Raid Tools media pack for one voice.</summary>
public sealed class NorthernSkyRaidToolsAddOnService(
    IOptionsSnapshot<AddOnSettings> addOnOptions,
    NorthernSkyRaidToolsVocabularyProvider vocabulary)
    : IAddOnService
{
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("NorthernSkyRaidTools");

    public Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        string voice = ttsSettings.Voice?.ToString() ?? string.Empty;
        AddOn addOn = new AddOnBuilder(AddOnSettings, ttsSettings)
            .WithTitle($"WoWVoxPacks_NorthernSkyRaidTools_{voice}")
            .WithDisplayTitle($"WoWVoxPacks |cffff7f3fNorthern Sky Raid Tools|r|cffffffff ({voice})|r")
            .AddSoundFiles(vocabulary.SoundFiles)
            .AddFile("Core.lua", generatedAddOn =>
                NorthernSkyRaidToolsLuaFile.Render(generatedAddOn, vocabulary.Registrations))
            .Build(outputDirectoryBase);

        return Task.FromResult(addOn);
    }
}
