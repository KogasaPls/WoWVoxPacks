using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.ExBoss;

public sealed class ExBossAddOnService(
    ILogger<ExBossAddOnService> logger,
    IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService<ExBossAddOn>
{
    private ILogger<ExBossAddOnService> Logger { get; } = logger;

    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("ExBoss");

    public Task<ExBossAddOn> BuildAddOnAsync(string outputDirectoryBase,
        TtsSettings ttsSettings,
        CancellationToken cancellationToken)
    {
        ExBossAddOn addOn = new(outputDirectoryBase, AddOnSettings, ttsSettings);
        return Task.FromResult(addOn);
    }
}
