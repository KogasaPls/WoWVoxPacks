namespace WoWVoxPack.AddOns.BigWigs_Voice;

public interface IBigWigsVoiceUpstreamClient
{
    Task<IEnumerable<BigWigsVoiceSoundFile>> GetSoundFilesAsync(
        CancellationToken cancellationToken = default);
}
