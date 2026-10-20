using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.AddOns.BigWigs_Voice;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class BigWigsVoiceAddOnServiceTests
{
    private static AddOnSettings Settings => new()
    {
        Title = "BigWigs_Voice_WoWVoxPacks",
        Version = "12.0.7",
        Author = "Tester",
        Notes = "A test addon."
    };

    private static TtsSettings TtsSettings => new() { Voice = VoiceName.Neural2_C };

    [Fact]
    public void Constructor_KeepsUpstreamIpaPendingUntilResolution()
    {
        BigWigsVoiceSoundFile sound =
            new("111", "Wing Buffet=bʌf.ɪt", "upstream:spells.txt:9");

        Assert.Equal("Wing Buffet", sound.Text);
        Assert.Null(sound.Pronunciations);
        PronunciationHint hint = Assert.Single(sound.ImportedPronunciations!);
        Assert.Equal(new Pronunciation("Buffet", "bʌf.ɪt"), hint.Pronunciation);
        Assert.Equal("upstream:spells.txt:9", hint.Origin);
    }

    [Fact]
    public async Task BuildAddOnAsync_KeepsBothSpells_WhenTheyShareAName()
    {
        BigWigsVoiceAddOnService service = new(new StubOptions(Settings),
            new FakeUpstreamClient([new BigWigsVoiceSoundFile("111", "Shadow Bolt"),
                new BigWigsVoiceSoundFile("222", "Shadow Bolt")]), BigWigsVoicePendingSpells.Empty);

        AddOnDraft draft = await service.BuildAddOnAsync("/tmp/output", TtsSettings);
        AddOn addOn = draft.Finalize(draft.SoundFiles);

        SoundFile[] shadowBolts = addOn.SoundFiles.Where(f => f.DisplayName == "Shadow Bolt").ToArray();
        Assert.Equal(["111.ogg", "222.ogg"], shadowBolts.Select(f => f.FileName).Order());
    }

    [Fact]
    public async Task BuildAddOnAsync_NamesTheFolderAfterTheVoice()
    {
        BigWigsVoiceAddOnService service =
            new(new StubOptions(Settings), new FakeUpstreamClient([]), BigWigsVoicePendingSpells.Empty);

        AddOnDraft draft = await service.BuildAddOnAsync("/tmp/output", TtsSettings);
        AddOn addOn = draft.Finalize(draft.SoundFiles);

        // BigWigs finds a voice pack by its TOC metadata, never by folder name.
        Assert.Equal("BigWigs_Voice_WoWVoxPacks_Neural2_C", addOn.AddOnDirectoryName);
        Assert.Contains("BigWigs_Voice_WoWVoxPacks_Neural2_C", addOn.GetFileContent("Core.lua"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAddOnAsync_PrefersTheCuratedEntry_WhenUpstreamNamesTheSameSpellDifferently()
    {
        List<SoundFile> curated =
            AddOnBuilder.LoadSoundFileJson(Path.Combine(AppContext.BaseDirectory, "BigWigsVoice_Sounds.json"));
        BigWigsVoiceSoundFile[] upstream = curated
            .Select(f => new BigWigsVoiceSoundFile(Path.GetFileNameWithoutExtension(f.FileName), "Upstream Name"))
            .ToArray();

        BigWigsVoiceAddOnService service =
            new(new StubOptions(Settings), new FakeUpstreamClient(upstream), BigWigsVoicePendingSpells.Empty);

        AddOnDraft draft = await service.BuildAddOnAsync("/tmp/output", TtsSettings);
        AddOn addOn = draft.Finalize(draft.SoundFiles);

        Assert.Equal(curated.Count, addOn.SoundFiles.Count());
        foreach (SoundFile expected in curated)
        {
            SoundFile actual = Assert.Single(addOn.SoundFiles, f => f.FileName == expected.FileName);
            Assert.Equal(expected.DisplayName, actual.DisplayName);
        }
    }

    [Fact]
    public async Task BuildAddOnAsync_PreservesUpstreamHintWhenCuratedEntryReplacesTheId()
    {
        BigWigsVoiceSoundFile upstream =
            new("338353", "Goresplatter=upstream-ipa", "upstream:spells.txt:12");
        BigWigsVoiceAddOnService service =
            new(new StubOptions(Settings), new FakeUpstreamClient([upstream]), BigWigsVoicePendingSpells.Empty);

        AddOnDraft draft = await service.BuildAddOnAsync("/tmp/output", TtsSettings);

        SoundFile curated = Assert.Single(draft.SoundFiles, sound => sound.Key == "338353");
        PronunciationHint hint = Assert.Single(curated.ImportedPronunciations!);
        Assert.Equal(new Pronunciation("Goresplatter", "upstream-ipa"), hint.Pronunciation);
        Assert.Equal("upstream:spells.txt:12", hint.Origin);
    }

    [Fact]
    public async Task Resolve_GivesDuplicateAxegrinderAndGoresplatterIdsTheSamePronunciation()
    {
        BigWigsVoiceSoundFile[] upstream =
        [
            new("1283832", "Axegrinder"),
            new("1301111", "Axegrinder"),
            new("338353", "Goresplatter"),
            new("442530", "Goresplatter")
        ];
        BigWigsVoiceAddOnService service =
            new(new StubOptions(Settings), new FakeUpstreamClient(upstream), BigWigsVoicePendingSpells.Empty);
        AddOnDraft draft = await service.BuildAddOnAsync("/tmp/output", TtsSettings);
        PronunciationResolver resolver = new(
            PronunciationCatalog.Load(FindRepoFile("pronunciations.json")),
            NullLogger<PronunciationResolver>.Instance);

        SoundFile[] sounds = resolver.Resolve([draft]).Single().SoundFiles.ToArray();

        Assert.All(sounds.Where(sound => sound.DisplayName == "Axegrinder"),
            sound => Assert.Equal("Axe grinder", sound.Text));
        Assert.All(sounds.Where(sound => sound.DisplayName == "Goresplatter"),
            sound => Assert.Contains(new Pronunciation("Goresplatter", "ˈɡɔrˌsplætər"),
                sound.Pronunciations!));
    }

    [Fact]
    public async Task BuildAddOnAsync_AddsPendingSpellsUpstreamDoesNotList()
    {
        BigWigsVoicePendingSpells pending =
            BigWigsVoicePendingSpells.Parse("pending.txt", "; Boss: Kith'ix\n1301511\tVoidswarm\n");
        BigWigsVoiceAddOnService service = new(new StubOptions(Settings),
            new FakeUpstreamClient([new BigWigsVoiceSoundFile("111", "Shadow Bolt")]), pending);

        AddOnDraft draft = await service.BuildAddOnAsync("/tmp/output", TtsSettings);

        Assert.Contains(draft.SoundFiles, sound => sound.FileName == "111.ogg");
        SoundFile voidswarm = Assert.Single(draft.SoundFiles, sound => sound.FileName == "1301511.ogg");
        Assert.Equal("Voidswarm", voidswarm.Text);
    }

    [Fact]
    public async Task BuildAddOnAsync_PrefersUpstream_WhenItListsAPendingSpell()
    {
        BigWigsVoicePendingSpells pending =
            BigWigsVoicePendingSpells.Parse("pending.txt", "1301511\tVoidswarm\n");
        BigWigsVoiceAddOnService service = new(new StubOptions(Settings),
            new FakeUpstreamClient([new BigWigsVoiceSoundFile("1301511", "Void Swarm")]), pending);

        AddOnDraft draft = await service.BuildAddOnAsync("/tmp/output", TtsSettings);

        SoundFile voidswarm = Assert.Single(draft.SoundFiles, sound => sound.FileName == "1301511.ogg");
        Assert.Equal("Void Swarm", voidswarm.Text);
    }

    [Fact]
    public void ShippedPendingSpells_ParseEveryEntry()
    {
        // The parser skips any line it cannot split on one tab, so a space typed in place of
        // the tab would drop that spell without a word.
        string path = FindRepoFile(BigWigsVoicePendingSpells.FileName);
        string[] entries = File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith(';'))
            .ToArray();

        BigWigsVoicePendingSpells pending = BigWigsVoicePendingSpells.Load(path);

        Assert.Equal(entries.Length, pending.SoundFiles.Count);
        Assert.All(pending.SoundFiles, sound => Assert.Matches(@"^\d+$", sound.SpellId));
        Assert.Equal(pending.SoundFiles.Count, pending.SoundFiles.Select(sound => sound.SpellId).Distinct().Count());
    }

    private static string FindRepoFile(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, fileName)))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, fileName);
    }

    private sealed class FakeUpstreamClient(IEnumerable<BigWigsVoiceSoundFile> soundFiles)
        : IBigWigsVoiceUpstreamClient
    {
        public Task<IEnumerable<BigWigsVoiceSoundFile>> GetSoundFilesAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(soundFiles);
    }

    private sealed class StubOptions(AddOnSettings settings) : IOptionsSnapshot<AddOnSettings>
    {
        public AddOnSettings Value => settings;

        public AddOnSettings Get(string? name) => settings;
    }
}
