using System.Globalization;

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

        if (soundFile.Composition is not null)
        {
            await ComposeAsync(soundFile, soundFile.Composition, outputDirectory, settings, cancellationToken);
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

        // Appended, not ChangeExtension: the stem is a hidden file, so its leading dot is the only
        // one in the name and ChangeExtension reads the whole thing as the extension. It replaced
        // the name and left every render in the fleet sharing one temp path.
        string correctExtension = ttsResponse.Format.GetFileExtension();
        string pendingSource = stem + correctExtension;
        string pendingOgg = stem + ".ogg";

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

    /// <summary>
    /// Concatenates fixed-length slots: silence up to the first onset, then each source with its
    /// leading silence removed and padded or cut to the gap before the next onset. Slots rather
    /// than a mix, because amix followed by apad dropped the leading silence on ffmpeg 9.
    /// </summary>
    private static async Task ComposeAsync(SoundFile soundFile, SoundComposition composition, string outputDirectory,
        TtsSettings settings, CancellationToken cancellationToken)
    {
        SoundCompositionPart[] parts = composition.Parts.OrderBy(part => part.At).ToArray();
        if (parts.Length == 0 || parts[0].At < 0 || parts[^1].At >= composition.Duration ||
            parts.Zip(parts.Skip(1)).Any(pair => pair.Second.At <= pair.First.At))
        {
            throw new InvalidOperationException(
                $"{soundFile.FileName} composes {composition}, whose onsets must ascend and fall within its duration.");
        }

        string[] sources = parts.Select(part => Path.Combine(outputDirectory, part.FileName)).ToArray();
        string? missing = sources.FirstOrDefault(source => !File.Exists(source));
        if (missing is not null)
        {
            throw new FileNotFoundException(
                $"{soundFile.FileName} is composed from {Path.GetFileName(missing)}, which is not in {outputDirectory}.",
                missing);
        }

        string sampleRate = settings.SampleRateHertz.ToString(CultureInfo.InvariantCulture);
        List<string> chains = [];
        List<string> labels = [];
        FFMpegArguments? arguments = null;
        if (parts[0].At > 0)
        {
            arguments = FFMpegArguments.FromFileInput($"anullsrc=r={sampleRate}:cl=mono", false,
                options => options.ForceFormat("lavfi"));
            chains.Add($"[0:a]atrim=end={Seconds(parts[0].At)}[lead]");
            labels.Add("[lead]");
        }

        for (int index = 0; index < parts.Length; index++)
        {
            arguments = arguments is null
                ? FFMpegArguments.FromFileInput(sources[index])
                : arguments.AddFileInput(sources[index]);
            int input = labels.Count;
            string slot = Seconds(
                (index + 1 < parts.Length ? parts[index + 1].At : composition.Duration) - parts[index].At);
            chains.Add(
                $"[{input}:a]silenceremove=start_periods=1:start_threshold=-50dB," +
                $"aformat=sample_rates={sampleRate}:channel_layouts=mono," +
                $"atrim=end={slot},apad=whole_dur={slot}[part{index}]");
            labels.Add($"[part{index}]");
        }

        chains.Add($"{string.Concat(labels)}concat=n={labels.Count}:v=0:a=1[out]");
        string filter = string.Join(";", chains);

        string target = Path.Combine(outputDirectory, soundFile.FileName);
        string pendingOgg = Path.Combine(outputDirectory,
            $".wvp-{Path.GetFileNameWithoutExtension(soundFile.FileName)}-{Guid.NewGuid():N}.ogg");
        try
        {
            await arguments!
                .OutputToFile(pendingOgg, true,
                    options =>
                    {
                        options.WithCustomArgument($"-filter_complex \"{filter}\" -map \"[out]\"");
                        options.WithAudioCodec("libvorbis");
                        options.WithAudioBitrate(AudioQuality.BelowNormal);
                    })
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously();

            File.Move(pendingOgg, target, true);
        }
        finally
        {
            Delete(pendingOgg);
        }
    }

    private static string Seconds(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
