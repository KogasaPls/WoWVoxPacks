using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public sealed class BigWigsVoiceAddOnService(
    ILogger<BigWigsVoiceAddOnService> logger,
    IOptionsSnapshot<AddOnSettings> addOnOptions,
    IBigWigsVoiceUpstreamClient upstreamClient)
    : IAddOnService<BigWigsVoiceAddOn>
{
    private static readonly Lazy<List<SoundFile>> CountdownSoundFiles = new(GetCountdownSoundFiles);

    private BigWigsVoiceSoundFile[]? _soundFiles;

    private ILogger<BigWigsVoiceAddOnService> Logger { get; } = logger;

    private IBigWigsVoiceUpstreamClient UpstreamClient { get; } = upstreamClient;
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("BigWigs_Voice");

    public async Task<BigWigsVoiceAddOn> BuildAddOnAsync(string outputDirectoryBase,
        TtsSettings ttsSettings,
        CancellationToken cancellationToken)
    {
        IEnumerable<BigWigsVoiceSoundFile> soundFiles = await GetSoundFilesAsync(cancellationToken);

        BigWigsVoiceAddOn addOn = new(outputDirectoryBase, AddOnSettings, ttsSettings,
            soundFiles.Concat(CountdownSoundFiles.Value));

        return addOn;
    }

    private async ValueTask<IEnumerable<BigWigsVoiceSoundFile>>
        GetSoundFilesAsync(CancellationToken cancellationToken)
    {
        return _soundFiles ??= (await UpstreamClient.GetSoundFilesAsync(cancellationToken)).ToArray();
    }

    private static List<SoundFile> GetCountdownSoundFiles()
    {
        return
        [
            new SoundFile("countdown_1", "1"),
            new SoundFile("countdown_2", "2"),
            new SoundFile("countdown_3", "3"),
            new SoundFile("countdown_4", "4"),
            new SoundFile("countdown_5", "5"),
            new SoundFile("countdown_6", "6"),
            new SoundFile("countdown_7", "7"),
            new SoundFile("countdown_8", "8"),
            new SoundFile("countdown_9", "9"),
            new SoundFile("countdown_10", "10")
        ];
    }
}
