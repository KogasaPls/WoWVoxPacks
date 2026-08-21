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
    public async Task BuildAddOnAsync_KeepsBothSpells_WhenTheyShareAName()
    {
        BigWigsVoiceAddOnService service = new(new StubOptions(Settings),
            new FakeUpstreamClient([new BigWigsVoiceSoundFile("111", "Shadow Bolt"),
                new BigWigsVoiceSoundFile("222", "Shadow Bolt")]));

        AddOn addOn = await service.BuildAddOnAsync("/tmp/output", TtsSettings);

        SoundFile[] shadowBolts = addOn.SoundFiles.Where(f => f.DisplayName == "Shadow Bolt").ToArray();
        Assert.Equal(["111.ogg", "222.ogg"], shadowBolts.Select(f => f.FileName).Order());
    }

    [Fact]
    public async Task BuildAddOnAsync_NamesTheFolderAfterTheVoice()
    {
        BigWigsVoiceAddOnService service =
            new(new StubOptions(Settings), new FakeUpstreamClient([]));

        AddOn addOn = await service.BuildAddOnAsync("/tmp/output", TtsSettings);

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
            new(new StubOptions(Settings), new FakeUpstreamClient(upstream));

        AddOn addOn = await service.BuildAddOnAsync("/tmp/output", TtsSettings);

        Assert.Equal(curated.Count, addOn.SoundFiles.Count());
        foreach (SoundFile expected in curated)
        {
            SoundFile actual = Assert.Single(addOn.SoundFiles, f => f.FileName == expected.FileName);
            Assert.Equal(expected.DisplayName, actual.DisplayName);
        }
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
