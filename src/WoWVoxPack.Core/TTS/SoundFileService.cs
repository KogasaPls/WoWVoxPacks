using FFMpegCore;
using FFMpegCore.Enums;

using Microsoft.Extensions.Logging;

namespace WoWVoxPack.TTS;

public class SoundFileService(ITtsProvider ttsProvider, ILogger<SoundFileService> logger) : ISoundFileService
{
    private ITtsProvider TtsProvider { get; } = ttsProvider;
    private ILogger<SoundFileService> Logger { get; } = logger;

    public async Task CreateSoundFileAsync(SoundFile soundFile, string outputDirectory, TtsSettings settings,
        CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Creating sound file {FileName} in {OutputDirectory}", soundFile.FileName, outputDirectory);

        if (!string.IsNullOrEmpty(soundFile.CopyFromPath))
        {
            File.Copy(soundFile.CopyFromPath, Path.Combine(outputDirectory, soundFile.FileName), true);
            return;
        }

        string filePathWithOggExtension =
            Path.Combine(outputDirectory, Path.ChangeExtension(soundFile.FileName, ".ogg"));
        TtsResponse ttsResponse = await TtsProvider.GetAudioContentAsync(soundFile, settings, cancellationToken);

        string correctExtension = ttsResponse.Format.GetFileExtension();
        string filePathWithCorrectExtension = Path.ChangeExtension(filePathWithOggExtension, correctExtension);
        await File.WriteAllBytesAsync(filePathWithCorrectExtension, ttsResponse.AudioContent, cancellationToken);

        string originalExtension = Path.GetExtension(soundFile.FileName);
        if (!originalExtension.Equals(correctExtension, StringComparison.OrdinalIgnoreCase) &&
            !correctExtension.Equals(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            await FFMpegArguments.FromFileInput(filePathWithCorrectExtension)
                .OutputToFile(filePathWithOggExtension, true,
                    options =>
                    {
                        options.WithAudioCodec("libvorbis");
                        options.WithAudioBitrate(AudioQuality.BelowNormal);
                    })
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously();
        }
    }
}
