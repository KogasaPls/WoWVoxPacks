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

        // Everything is written beside the final name and moved onto it once it is whole. A build
        // that dies mid-encode would otherwise leave a truncated ogg that exists, and a file that
        // exists is a file no later build re-renders.
        // Beside the target rather than in the system temp directory, so the move is a rename and
        // not a copy. The name is the one package.sh excludes, in case a hard kill leaves one.
        string stem = Path.Combine(outputDirectory,
            $".wvp-{Path.GetFileNameWithoutExtension(soundFile.FileName)}-{Guid.NewGuid():N}");
        string correctExtension = ttsResponse.Format.GetFileExtension();
        string pendingSource = Path.ChangeExtension(stem, correctExtension);
        string pendingOgg = Path.ChangeExtension(stem, ".ogg");

        try
        {
            await File.WriteAllBytesAsync(pendingSource, ttsResponse.AudioContent, cancellationToken);

            string originalExtension = Path.GetExtension(soundFile.FileName);
            if (!originalExtension.Equals(correctExtension, StringComparison.OrdinalIgnoreCase) &&
                !correctExtension.Equals(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                await FFMpegArguments.FromFileInput(pendingSource)
                    .OutputToFile(pendingOgg, true,
                        options =>
                        {
                            options.WithAudioCodec("libvorbis");
                            options.WithAudioBitrate(AudioQuality.BelowNormal);
                        })
                    .CancellableThrough(cancellationToken)
                    .ProcessAsynchronously();
            }

            File.Move(pendingOgg, filePathWithOggExtension, true);
        }
        finally
        {
            // Google answers in LINEAR16, so every rendered sound has a wav to clean up. They are
            // gitignored and excluded from the archives, which is why 2.7 GB of them had piled up
            // in a working tree unnoticed.
            Delete(pendingSource);
            Delete(pendingOgg);
        }
    }

    private static void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
