using System.Text.Json;

using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Abilities;

public sealed class SharedMediaAbilitiesAddOnService(IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService
{
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("SharedMedia_Abilities");

    public Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        string file = Path.Combine(AppContext.BaseDirectory, "SharedMedia_Abilities_Sounds.json");

        AddOn addOn = new AddOnBuilder(AddOnSettings, ttsSettings)
            .WithTitle($"SharedMedia Abilities WoWVoxPacks {ttsSettings.Voice}")
            .WithDisplayTitle($"SharedMedia |cffff7f3f+|r|cffffffffAbilities: WoWVoxPacks ({ttsSettings.Voice})|r")
            .AddSoundFiles(LoadSoundsFromJson(file), overwrite: true)
            .AddFile("Core.lua", addon => new AbilitiesLuaFile(addon).TransformText())
            .Build(outputDirectoryBase);

        return Task.FromResult(addOn);
    }

    private static IEnumerable<SoundFile> LoadSoundsFromJson(string filePath)
    {
        List<SoundFile> soundFiles =
            JsonSerializer.Deserialize<List<SoundFile>>(File.ReadAllText(filePath),
                SoundFileJsonContext.Default.ListSoundFile) ?? [];

        return soundFiles.Select(sf =>
            sf.Ssml is null && sf.Text?.Contains('=') == true
                ? new SoundFile(sf.FileName, ssml: SoundFile.GetSsml(sf.Text), displayName: sf.DisplayName,
                    formattedDisplayName: sf.FormattedDisplayName)
                : sf);
    }
}
