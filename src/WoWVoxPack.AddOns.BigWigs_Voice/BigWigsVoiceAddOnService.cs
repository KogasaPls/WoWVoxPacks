using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public sealed class BigWigsVoiceAddOnService(
    IOptionsSnapshot<AddOnSettings> addOnOptions,
    IBigWigsVoiceUpstreamClient upstreamClient)
    : IAddOnService
{
    private static readonly Lazy<List<SoundFile>> JsonSoundFiles = new(LoadCuratedSoundFiles);

    private BigWigsVoiceSoundFile[]? _soundFiles;

    private IBigWigsVoiceUpstreamClient UpstreamClient { get; } = upstreamClient;
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("BigWigs_Voice");

    public async Task<AddOnDraft> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        BigWigsVoiceSoundFile[] soundFiles = (await GetSoundFilesAsync(cancellationToken)).ToArray();
        IReadOnlyDictionary<string, BigWigsVoiceSoundFile> upstreamById = soundFiles
            .GroupBy(sound => sound.SpellId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        IEnumerable<SoundFile> curated = JsonSoundFiles.Value.Select(sound =>
            CloneCurated(sound, upstreamById.GetValueOrDefault(sound.Key)));

        return new AddOnBuilder(AddOnSettings, ttsSettings, "BigWigs_Voice")
            .WithTitle($"BigWigs Voice WoWVoxPacks {ttsSettings.Voice}")
            .WithDisplayTitle($"BigWigs |cffff7f3f+|r|cffffffffVoice: WoWVoxPacks ({ttsSettings.Voice})|r")
            .AddFile("Core.lua", CoreLuaFile.Render)
            .AddSoundFiles(soundFiles)
            .AddSoundFiles(curated, overwrite: true)
            .BuildDraft(outputDirectoryBase);
    }

    /// <summary>
    /// The curated entries name their file after the spell they are overriding, so the ID in the
    /// file name is their key. Keying them by name instead would miss the upstream entry whenever
    /// the two disagree about the name, and both would then render the same file.
    /// </summary>
    private static List<SoundFile> LoadCuratedSoundFiles()
    {
        List<SoundFile> soundFiles =
            AddOnBuilder.LoadSoundFileJson(Path.Combine(AppContext.BaseDirectory, "BigWigsVoice_Sounds.json"));

        foreach (SoundFile soundFile in soundFiles)
        {
            soundFile.ExplicitKey = Path.GetFileNameWithoutExtension(soundFile.FileName);
        }

        return soundFiles;
    }

    private static SoundFile CloneCurated(SoundFile source, BigWigsVoiceSoundFile? upstream)
    {
        return new SoundFile(
            source.FileName,
            source.Text,
            source.Ssml,
            source.DisplayName,
            source.FormattedDisplayName,
            source.Pronunciations)
        {
            ExplicitKey = source.ExplicitKey,
            CopyFromPath = source.CopyFromPath,
            PronunciationName = upstream?.PronunciationName ?? source.PronunciationName,
            ImportedPronunciations = upstream?.ImportedPronunciations
        };
    }

    private async ValueTask<IEnumerable<BigWigsVoiceSoundFile>>
        GetSoundFilesAsync(CancellationToken cancellationToken)
    {
        return _soundFiles ??= (await UpstreamClient.GetSoundFilesAsync(cancellationToken)).ToArray();
    }
}
