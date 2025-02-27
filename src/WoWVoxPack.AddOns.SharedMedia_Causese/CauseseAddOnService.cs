using FFMpegCore;
using FFMpegCore.Enums;

using Google.Cloud.TextToSpeech.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Causese;

public class CauseseAddOnService(
    ILogger<CauseseAddOnService> logger,
    GoogleTtsClient googleTtsClient,
    IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService
{
    private ILogger<CauseseAddOnService> Logger { get; } = logger;

    private GoogleTtsClient GoogleTtsClient { get; } = googleTtsClient;

    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("SharedMedia_Causese");

    public async Task BuildAddOnAsync(string outputDirectoryBase, CancellationToken cancellationToken = default)
    {
        CauseseAddOn addOn = new(AddOnSettings);
        var outputDirectory = Path.Combine(outputDirectoryBase, addOn.OutputDirectoryName);
        string soundsDirectory = Path.Combine(outputDirectory, "Sounds");

        Logger.LogInformation("Building {AddOnName} add-on in directory {OutputDirectory}", addOn.Title, outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(soundsDirectory);

        IEnumerable<Task<CauseseSoundFile>> soundFileTasks = addOn.SoundFiles
            .Where(file => !File.Exists(Path.Combine(soundsDirectory, file.FileName)))
            .Select(file => CreateSoundFile(file, soundsDirectory, cancellationToken))
            .ToList();

        _ = await Task.WhenAll(soundFileTasks);

        await addOn.WriteAllFilesAsync(outputDirectory, cancellationToken);
    }

    private async Task<CauseseSoundFile> CreateSoundFile(SoundFile soundFile, string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(soundFile.FileName);
        string fileNameWithWavExtension = $"{fileNameWithoutExtension}.wav";

        Logger.LogInformation("Creating sound file {FileName}", soundFile.FileName);
        ByteString audioContent = await SynthesizeText(soundFile, cancellationToken);


        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, fileNameWithWavExtension),
            audioContent.ToByteArray(), cancellationToken);

        Logger.LogDebug("Converting {FileName} to OGG", soundFile.FileName);
        await FFMpegArguments.FromFileInput(Path.Combine(outputDirectory, fileNameWithWavExtension))
            .OutputToFile(Path.Combine(outputDirectory, Path.ChangeExtension(fileNameWithWavExtension, "ogg")),
                addArguments: options =>
                {
                    options.WithAudioCodec("libvorbis");
                    options.WithAudioBitrate(AudioQuality.BelowNormal);
                    options.UsingMultithreading(false);
                })
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously();

        return new CauseseSoundFile(soundFile.FileName, audioContent);
    }

    private async Task<ByteString> SynthesizeText(SoundFile soundFile, CancellationToken cancellationToken)
    {
        if (soundFile.SSML is not null)
        {
            ByteString audioContent =
                await GoogleTtsClient.SynthesizeSsml(soundFile.SSML, AddOnSettings.TtsSettings, AudioEncoding.Linear16,
                    cancellationToken);
            return audioContent;
        }

        if (soundFile.Text is not null)
        {
            ByteString audioContent =
                await GoogleTtsClient.SynthesizeText(soundFile.Text, AddOnSettings.TtsSettings, AudioEncoding.Linear16,
                    cancellationToken);
            return audioContent;
        }

        throw new InvalidOperationException("Sound file must have either SSML or Text.");
    }
}
