using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoWVoxPack.AddOns.SharedMedia_Causese;

public class CauseseAddOnService(
    ILogger<CauseseAddOnService> logger,
    IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService<CauseseAddOn>
{
    private ILogger<CauseseAddOnService> Logger { get; } = logger;

    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("SharedMedia_Causese");

    public Task<CauseseAddOn> BuildAddOnAsync(string outputDirectoryBase, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CauseseAddOn(outputDirectoryBase, AddOnSettings));
    }
}
