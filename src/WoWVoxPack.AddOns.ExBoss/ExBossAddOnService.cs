using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.ExBoss;

public sealed class ExBossAddOnService(IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService
{
    private static readonly Lazy<List<SoundFile>> JsonSoundFiles = new(() =>
        AddOnBuilder.LoadSoundFileJson(Path.Combine(AppContext.BaseDirectory, "Labels.json")));

    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("ExBoss");

    public Task<AddOnDraft> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        AddOnDraft addOn = new AddOnBuilder(AddOnSettings, ttsSettings, "ExBoss")
            .WithTitle($"ExBoss WoWVoxPacks {ttsSettings.Voice}")
            .WithDisplayTitle($"ExBoss WoWVoxPacks ({ttsSettings.Voice})")
            .AddSoundFiles(JsonSoundFiles.Value, overwrite: true)
            .AddFile("Core.lua", LabelsFile.Render)
            .BuildDraft(outputDirectoryBase);

        return Task.FromResult(addOn);
    }
}
