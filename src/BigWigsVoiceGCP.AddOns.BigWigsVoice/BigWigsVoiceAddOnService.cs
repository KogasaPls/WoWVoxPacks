using System.Xml.Linq;

using BigWigsVoiceGCP.TTS;

using FFMpegCore;
using FFMpegCore.Enums;

using Google.Cloud.TextToSpeech.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BigWigsVoiceGCP.AddOns.BigWigsVoice;

public sealed class BigWigsVoiceAddOnService(
    ILogger<BigWigsVoiceAddOnService> logger,
    GoogleTtsClient googleTtsClient,
    IOptionsSnapshot<AddOnSettings> addOnOptions,
    IOptions<TtsSettings> ttsOptions,
    IBigWigsVoiceUpstreamClient upstreamClient)
    : IAddOnService
{
    private ILogger<BigWigsVoiceAddOnService> Logger { get; } = logger;

    private GoogleTtsClient GoogleTtsClient { get; } = googleTtsClient;

    private TtsSettings TtsSettings { get; } = ttsOptions.Value;
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("BigWigs_Voice");
    private IBigWigsVoiceUpstreamClient UpstreamClient { get; } = upstreamClient;


    public async Task BuildAddOnAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        IEnumerable<SpellListFile> spellListFiles = await UpstreamClient.GetSpellListFiles();

        AddOnSettings.Title = $"BigWigs Voice GCP {TtsSettings.Voice}";
        outputDirectory = Path.Combine(outputDirectory, AddOnSettings.Title);

        BigWigsVoiceAddon addOn = new(AddOnSettings);

        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(Path.Combine(outputDirectory, "Sounds"));

        IEnumerable<Task<BigWigsVoiceSoundFile>> soundFileTasks = spellListFiles
            .SelectMany(spellListFile => GetSoundFiles(spellListFile, outputDirectory))
            .ToList();

        _ = await Task.WhenAll(soundFileTasks);

        await addOn.WriteAllFilesAsync(outputDirectory, cancellationToken);
    }

    private IEnumerable<Task<BigWigsVoiceSoundFile>> GetSoundFiles(SpellListFile spellListFile, string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        string content = spellListFile.Content;

        foreach (string line in content.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith(';'))
            {
                continue;
            }

            string[] parts = line.Split('\t');
            if (parts.Length != 2)
            {
                Logger.LogWarning("Invalid line in spell list file: {Line}", line);
                continue;
            }

            string spellId = parts[0];
            string spellName = parts[1];

            string fileName = $"{spellId}.wav";

            string soundsDirectory = Path.Combine(outputDirectory, "Sounds");
            if (File.Exists(Path.Combine(soundsDirectory, fileName)))
            {
                Logger.LogDebug("Skipping spell {SpellId} ({SpellName}) because it already exists", spellId, spellName);
                continue;
            }

            yield return CreateSoundFile(spellId, spellName, soundsDirectory, cancellationToken);
        }
    }

    private async Task<BigWigsVoiceSoundFile> CreateSoundFile(string spellId, string spellName, string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Synthesizing text for spell {SpellId} ({SpellName})", spellId, spellName);

        ByteString audioContent = await SynthesizeTextForSpell(spellName, cancellationToken);
        BigWigsVoiceSoundFile file = new(spellId, spellName, audioContent);

        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, file.FileName), audioContent.ToByteArray(), cancellationToken);

        Logger.LogDebug("Converting {FileName} to OGG", file.FileName);
        await FFMpegArguments.FromFileInput(Path.Combine(outputDirectory, file.FileName))
            .OutputToFile(Path.Combine(outputDirectory, Path.ChangeExtension(file.FileName, "ogg")),
                addArguments: options =>
                {
                    options.WithAudioCodec("libvorbis");
                    options.WithAudioBitrate(AudioQuality.BelowNormal);
                    options.UsingMultithreading(false);
                })
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously();

        return file;
    }

    private Task<ByteString> SynthesizeTextForSpell(string spellName, CancellationToken cancellationToken = default)
    {
        string ssml = GetSsml(spellName);
        return GoogleTtsClient.SynthesizeSsml(ssml, TtsSettings, audioEncoding: AudioEncoding.Linear16,
            cancellationToken);
    }

    private static string GetSsml(string text)
    {
        XNamespace ns = XNamespace.Get("http://www.w3.org/2001/10/synthesis");

        return new XDocument(
            new XElement(
                ns + "speak",
                new XAttribute("version", "1.0"),
                new XAttribute(XNamespace.Xml + "lang", "en"),
                text.Split([' '], StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Split('='))
                    .Select(x => new { Word = x.ElementAt(0) + " ", WordIpa = x.ElementAtOrDefault(1) })
                    .Select(
                        x => x.WordIpa == null
                            ? new XText(x.Word) as XNode
                            : new XElement(ns + "phoneme", new XAttribute("alphabet", "IPA"),
                                new XAttribute("ph", x.WordIpa), x.Word)))).ToString();
    }
}
