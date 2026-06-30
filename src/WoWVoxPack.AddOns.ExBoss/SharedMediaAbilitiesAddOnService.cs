using System.Text.Json;

using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.ExBoss;

public sealed class ExBossAddOnService(IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService
{
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("ExBoss");

    public Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        string file = Path.Combine(AppContext.BaseDirectory, "Labels.json");

        AddOn addOn = new AddOnBuilder(AddOnSettings, ttsSettings)
            .WithTitle($"ExBoss WoWVoxPacks {ttsSettings.Voice}")
            .WithDisplayTitle($"ExBoss WoWVoxPacks ({ttsSettings.Voice})")
            .AddSoundFiles(LoadSoundsFromJson(file), overwrite: true)
            .AddFile("Core.lua", addon => new LabelsFile(addon).TransformText())
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
