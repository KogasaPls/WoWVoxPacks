using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Abilities;

public sealed class SharedMediaAbilitiesAddOnService(
    ILogger<SharedMediaAbilitiesAddOnService> logger,
    IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService<SharedMediaAbilitiesAddOn>
{
    private ILogger<SharedMediaAbilitiesAddOnService> Logger { get; } = logger;

    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("SharedMedia_Abilities");

    public Task<SharedMediaAbilitiesAddOn> BuildAddOnAsync(string outputDirectoryBase,
        TtsSettings ttsSettings,
        CancellationToken cancellationToken)
    {
        SharedMediaAbilitiesAddOn addOn = new(outputDirectoryBase, AddOnSettings, ttsSettings);
        return Task.FromResult(addOn);
    }
}
