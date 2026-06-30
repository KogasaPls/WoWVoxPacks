using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public sealed class BigWigsVoiceAddOnService(
    IOptionsSnapshot<AddOnSettings> addOnOptions,
    IBigWigsVoiceUpstreamClient upstreamClient)
    : IAddOnService
{
    private static readonly Lazy<List<SoundFile>> JsonSoundFiles = new(() =>
        AddOnBuilder.LoadSoundFileJson(Path.Combine(AppContext.BaseDirectory, "BigWigsVoice_Sounds.json")));

    private BigWigsVoiceSoundFile[]? _soundFiles;

    private IBigWigsVoiceUpstreamClient UpstreamClient { get; } = upstreamClient;
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("BigWigs_Voice");

    public async Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<BigWigsVoiceSoundFile> soundFiles = await GetSoundFilesAsync(cancellationToken);

        return new AddOnBuilder(AddOnSettings, ttsSettings)
            .WithDisplayTitle($"BigWigs |cffff7f3f+|r|cffffffffVoice: WoWVoxPacks ({ttsSettings.Voice})|r")
            .AddFile("Core.lua", addon => new CoreLuaFile(addon).TransformText())
            .AddSoundFiles(JsonSoundFiles.Value, overwrite: true)
            .AddSoundFiles(soundFiles)
            .Build(outputDirectoryBase);
    }

    private async ValueTask<IEnumerable<BigWigsVoiceSoundFile>>
        GetSoundFilesAsync(CancellationToken cancellationToken)
    {
        return _soundFiles ??= (await UpstreamClient.GetSoundFilesAsync(cancellationToken)).ToArray();
    }
}
