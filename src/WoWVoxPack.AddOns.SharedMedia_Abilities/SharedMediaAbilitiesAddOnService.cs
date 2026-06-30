using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Abilities;

public sealed class SharedMediaAbilitiesAddOnService(IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService
{
    private static readonly Lazy<List<SoundFile>> JsonSoundFiles = new(() =>
        AddOnBuilder.LoadSoundFileJsonWithSsmlFallback(
            Path.Combine(AppContext.BaseDirectory, "SharedMedia_Abilities_Sounds.json")));

    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("SharedMedia_Abilities");

    public Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        AddOn addOn = new AddOnBuilder(AddOnSettings, ttsSettings)
            .WithTitle($"SharedMedia Abilities WoWVoxPacks {ttsSettings.Voice}")
            .WithDisplayTitle($"SharedMedia |cffff7f3f+|r|cffffffffAbilities: WoWVoxPacks ({ttsSettings.Voice})|r")
            .AddSoundFiles(JsonSoundFiles.Value, overwrite: true)
            .AddFile("Core.lua", addon => new AbilitiesLuaFile(addon).TransformText())
            .Build(outputDirectoryBase);

        return Task.FromResult(addOn);
    }
}
