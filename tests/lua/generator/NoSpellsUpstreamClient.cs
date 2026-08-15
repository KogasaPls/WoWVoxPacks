using WoWVoxPack.AddOns.BigWigs_Voice;

internal sealed class NoSpellsUpstreamClient : IBigWigsVoiceUpstreamClient
{
    public Task<IEnumerable<BigWigsVoiceSoundFile>> GetSoundFilesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<BigWigsVoiceSoundFile>>([]);
    }
}
