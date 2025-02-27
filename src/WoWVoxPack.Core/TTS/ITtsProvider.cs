namespace WoWVoxPack.TTS;

public interface ITtsProvider
{
    Task<TtsResponse> GetAudioContentAsync(SoundFile soundFile, TtsSettings settings,
        CancellationToken cancellationToken = default);
}
