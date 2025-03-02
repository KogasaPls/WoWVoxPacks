namespace WoWVoxPack.TTS;

public interface ISoundFileService
{
    Task CreateSoundFileAsync(SoundFile soundFile, string outputDirectory, TtsSettings settings,
        CancellationToken cancellationToken = default);
}
