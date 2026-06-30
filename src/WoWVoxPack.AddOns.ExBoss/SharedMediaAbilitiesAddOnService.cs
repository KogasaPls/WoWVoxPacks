using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.ExBoss;

public sealed class ExBossAddOnService(IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService
{
    private static readonly Lazy<List<SoundFile>> JsonSoundFiles = new(() =>
        AddOnBuilder.LoadSoundFileJsonWithSsmlFallback(Path.Combine(AppContext.BaseDirectory, "Labels.json")));

    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("ExBoss");

    public Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        AddOn addOn = new AddOnBuilder(AddOnSettings, ttsSettings)
            .WithTitle($"ExBoss WoWVoxPacks {ttsSettings.Voice}")
            .WithDisplayTitle($"ExBoss WoWVoxPacks ({ttsSettings.Voice})")
            .AddSoundFiles(JsonSoundFiles.Value, overwrite: true)
            .AddFile("Core.lua", addon => new LabelsFile(addon).TransformText())
            .Build(outputDirectoryBase);

        return Task.FromResult(addOn);
    }
}
