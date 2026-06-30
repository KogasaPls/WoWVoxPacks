using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns;

public interface IAddOnService
{
    Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default);
}
