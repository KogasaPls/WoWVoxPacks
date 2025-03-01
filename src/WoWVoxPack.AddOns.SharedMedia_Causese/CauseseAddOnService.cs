using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Causese;

public class CauseseAddOnService(
    ILogger<CauseseAddOnService> logger,
    IOptionsSnapshot<AddOnSettings> addOnOptions,
    ICauseseUpstreamClient upstreamClient)
    : IAddOnService<CauseseAddOn>
{
    private ILogger<CauseseAddOnService> Logger { get; } = logger;

    private SoundFile[]? _soundFiles;

    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("SharedMedia_Causese");

    private ICauseseUpstreamClient UpstreamClient { get; } = upstreamClient;

    public async Task<CauseseAddOn> BuildAddOnAsync(string outputDirectoryBase,
        TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        var soundFiles = await GetSoundFilesAsync(cancellationToken);

        return new CauseseAddOn(outputDirectoryBase, AddOnSettings, ttsSettings, soundFiles);
    }

    private async ValueTask<IEnumerable<SoundFile>> GetSoundFilesAsync(
        CancellationToken cancellationToken = default) =>
        _soundFiles ??= (await UpstreamClient.GetSoundFilesAsync(cancellationToken)).ToArray();
}
