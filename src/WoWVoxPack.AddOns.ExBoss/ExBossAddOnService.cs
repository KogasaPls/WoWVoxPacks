using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.ExBoss;

public sealed class ExBossAddOnService(IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService
{
    private static readonly Lazy<List<SoundFile>> JsonSoundFiles = new(() =>
        AddOnBuilder.LoadSoundFileJson(Path.Combine(AppContext.BaseDirectory, "Labels.json")));

    public string AddOnId => "ExBoss";

    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("ExBoss");

    public Task<AddOnDraft> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        GameFlavor flavor, CancellationToken cancellationToken = default)
    {
        AddOnDraft addOn = new AddOnBuilder(AddOnSettings, ttsSettings, AddOnId)
            .ForFlavor(flavor)
            .WithTitle($"ExBoss WoWVoxPacks {ttsSettings.Voice}")
            .WithDisplayTitle($"ExBoss WoWVoxPacks ({ttsSettings.Voice})")
            .AddSoundFiles(JsonSoundFiles.Value, overwrite: true)
            .AddFile("Core.lua", LabelsFile.Render)
            .BuildDraft(outputDirectoryBase);

        return Task.FromResult(addOn);
    }
}
