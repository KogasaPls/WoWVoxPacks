using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.Callouts;

/// <summary>Builds the media folder: all audio, registered with LibSharedMedia.</summary>
public sealed class CalloutsMediaAddOnService(
    IOptionsSnapshot<AddOnSettings> addOnOptions,
    CalloutsVocabularyProvider vocabulary)
    : IAddOnService
{
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("Callouts");

    public Task<AddOnDraft> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        AddOnBuilder builder = new AddOnBuilder(AddOnSettings, ttsSettings, "Callouts")
            .WithTitle($"WoWVoxPacks Callouts {ttsSettings.Voice}")
            .WithDisplayTitle($"WoWVoxPacks |cffff7f3fCallouts|r|cffffffff ({ttsSettings.Voice})|r");

        // A reuse-only retired key is meaningful only for a recording already present in this
        // exact voice pack.
        string soundDirectory = builder.SoundDirectory(outputDirectoryBase);
        AddOnDraft addOn = builder
            .AddSoundFiles(vocabulary.SoundFilesFor(soundDirectory), overwrite: true)
            .AddFile("Core.lua", CalloutsLuaFile.Render)
            .BuildDraft(outputDirectoryBase);

        return Task.FromResult(addOn);
    }
}
