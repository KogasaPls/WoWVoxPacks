using Microsoft.Extensions.Options;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Abilities;

public sealed class SharedMediaAbilitiesAddOnService(IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService<SharedMediaAbilitiesAddOn>
{
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("SharedMedia_Abilities");

    public Task<SharedMediaAbilitiesAddOn> BuildAddOnAsync(string outputDirectoryBase,
        TtsSettings ttsSettings,
        CancellationToken cancellationToken)
    {
        SharedMediaAbilitiesAddOn addOn = new(outputDirectoryBase, AddOnSettings, ttsSettings);
        return Task.FromResult(addOn);
    }
}
