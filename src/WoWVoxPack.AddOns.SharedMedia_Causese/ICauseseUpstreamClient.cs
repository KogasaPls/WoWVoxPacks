using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Causese;

public interface ICauseseUpstreamClient
{
    Task<IEnumerable<SoundFile>> GetSoundFilesAsync(
        CancellationToken cancellationToken = default);
}
