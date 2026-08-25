using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns;

public interface IAddOnService
{
    Task<AddOnDraft> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default);
}
