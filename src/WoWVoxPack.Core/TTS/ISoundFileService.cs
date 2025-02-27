namespace WoWVoxPack.TTS;

public interface ISoundFileService
{
    Task CreateSoundFileIfNotExistsAsync(SoundFile soundFile, string outputDirectory, TtsSettings settings,
        CancellationToken cancellationToken = default);
}
