using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Countdown;

public sealed class BigWigsCountdownAddOnService(
    ILogger<BigWigsCountdownAddOnService> logger,
    IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService<BigWigsCountdownAddon>
{
    private ILogger<BigWigsCountdownAddOnService> Logger { get; } = logger;

    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("BigWigs_Countdown");

    public Task<BigWigsCountdownAddon> BuildAddOnAsync(string outputDirectoryBase,
        TtsSettings ttsSettings,
        CancellationToken cancellationToken)
    {
        BigWigsCountdownAddon addOn = new(outputDirectoryBase, AddOnSettings, ttsSettings);

        return Task.FromResult(addOn);
    }
}
