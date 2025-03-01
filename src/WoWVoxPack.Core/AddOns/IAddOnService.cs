using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns;

public interface IAddOnService<T> : IAddOnService where T : AddOn
{
    async Task<AddOn> IAddOnService.BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken)
    {
        return await BuildAddOnAsync(outputDirectoryBase, ttsSettings, cancellationToken).ConfigureAwait(false);
    }

    new Task<T> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default);
}

public interface IAddOnService
{
    Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default);
}
