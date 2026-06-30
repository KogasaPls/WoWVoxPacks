using Microsoft.Extensions.Options;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Countdown;

public sealed class BigWigsCountdownAddOnService(IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService<BigWigsCountdownAddOn>
{
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("BigWigs_Countdown");

    public Task<BigWigsCountdownAddOn> BuildAddOnAsync(string outputDirectoryBase,
        TtsSettings ttsSettings,
        CancellationToken cancellationToken)
    {
        BigWigsCountdownAddOn addOn = new(outputDirectoryBase, AddOnSettings, ttsSettings);

        return Task.FromResult(addOn);
    }
}
