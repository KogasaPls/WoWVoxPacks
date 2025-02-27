using FFMpegCore;
using FFMpegCore.Enums;

using Google.Cloud.TextToSpeech.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public sealed class BigWigsVoiceAddOnService(
    ILogger<BigWigsVoiceAddOnService> logger,
    IOptionsSnapshot<AddOnSettings> addOnOptions,
    IBigWigsVoiceUpstreamClient upstreamClient)
    : IAddOnService<BigWigsVoiceAddon>
{
    private ILogger<BigWigsVoiceAddOnService> Logger { get; } = logger;

    private IBigWigsVoiceUpstreamClient UpstreamClient { get; } = upstreamClient;
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("BigWigs_Voice");


    public async Task<BigWigsVoiceAddon> BuildAddOnAsync(string outputDirectoryBase,
        CancellationToken cancellationToken)
    {
        var soundFiles = await UpstreamClient.GetSoundFilesAsync(cancellationToken);

        BigWigsVoiceAddon addOn = new(outputDirectoryBase, AddOnSettings, soundFiles);

        return addOn;
    }
}
