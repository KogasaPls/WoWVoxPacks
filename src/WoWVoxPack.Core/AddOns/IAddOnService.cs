using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns;

public interface IAddOnService
{
    string AddOnId { get; }

    Task<AddOnDraft> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings, GameFlavor flavor,
        CancellationToken cancellationToken = default);
}
