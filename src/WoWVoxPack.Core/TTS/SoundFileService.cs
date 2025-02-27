using FFMpegCore;
using FFMpegCore.Enums;

namespace WoWVoxPack.TTS;

public class SoundFileService(ITtsProvider ttsProvider) : ISoundFileService
{
    private ITtsProvider TtsProvider { get; } = ttsProvider;

    public async Task CreateSoundFileIfNotExistsAsync(SoundFile soundFile, string outputDirectory, TtsSettings settings,
        CancellationToken cancellationToken = default)
    {
        var filePathWithOggExtension = Path.Combine(outputDirectory, Path.ChangeExtension(soundFile.FileName, ".ogg"));
        if (File.Exists(filePathWithOggExtension))
        {
            return;
        }

        var ttsResponse = await TtsProvider.GetAudioContentAsync(soundFile, settings, cancellationToken);

        var correctExtension = ttsResponse.Format.GetFileExtension();
        var filePathWithCorrectExtension = Path.ChangeExtension(filePathWithOggExtension, correctExtension);
        await File.WriteAllBytesAsync(filePathWithCorrectExtension, ttsResponse.AudioContent, cancellationToken);

        var originalExtension = Path.GetExtension(soundFile.FileName);
        if (!originalExtension.Equals(correctExtension, StringComparison.OrdinalIgnoreCase) &&
            !correctExtension.Equals(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            await FFMpegArguments.FromFileInput(filePathWithCorrectExtension)
                .OutputToFile(filePathWithOggExtension, true,
                    options =>
                    {
                        options.WithAudioCodec("libvorbis");
                        options.WithAudioBitrate(AudioQuality.BelowNormal);
                        options.UsingMultithreading(false);
                    })
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously();
        }
    }
}
