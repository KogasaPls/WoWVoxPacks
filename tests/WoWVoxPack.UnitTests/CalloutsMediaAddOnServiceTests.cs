using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.AddOns.Callouts;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public sealed class CalloutsMediaAddOnServiceTests : IDisposable
{
    private readonly string _temporaryDirectory = Directory.CreateTempSubdirectory().FullName;

    private static AddOnSettings Settings => new()
    {
        Title = "WoWVoxPacks_Callouts",
        Version = "12.0.7",
        Author = "Tester",
        Notes = "Test."
    };

    private static TtsSettings TtsSettings => new() { Voice = VoiceName.Neural2_C };

    [Fact]
    public async Task BuildAddOnAsync_KeepsMissingLiveAudioButSkipsMissingRetiredAudio()
    {
        CalloutsMediaAddOnService service = CreateService(["LiveCallout"], ["OldCallout"]);

        AddOn addOn = await service.BuildAddOnAsync(_temporaryDirectory, TtsSettings);

        Assert.Contains(addOn.SoundFiles, sound => sound.DisplayName == "Live Callout");
        Assert.DoesNotContain(addOn.SoundFiles, sound => sound.DisplayName == "Old Callout");
        Assert.DoesNotContain("Old Callout", addOn.GetFileContent("Core.lua"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAddOnAsync_RegistersRetiredAudioThatAlreadyExists()
    {
        string soundDirectory = Path.Combine(
            _temporaryDirectory,
            "WoWVoxPacks_Callouts_Neural2_C",
            "Sounds");
        Directory.CreateDirectory(soundDirectory);
        File.WriteAllText(Path.Combine(soundDirectory, "old_callout.ogg"), "existing audio");
        CalloutsMediaAddOnService service = CreateService([], ["OldCallout"]);

        AddOn addOn = await service.BuildAddOnAsync(_temporaryDirectory, TtsSettings);

        Assert.Contains(addOn.SoundFiles, sound => sound.DisplayName == "Old Callout");
        Assert.Contains("Old Callout", addOn.GetFileContent("Core.lua"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAddOnAsync_SkipsAMissingCaseOnlyRetiredKeyButKeepsTheLiveKey()
    {
        CalloutsMediaAddOnService service = CreateService(["soak"], ["Soak"]);

        AddOn addOn = await service.BuildAddOnAsync(_temporaryDirectory, TtsSettings);

        SoundFile sound = Assert.Single(addOn.SoundFiles);
        Assert.Equal("soak", sound.DisplayName);
    }

    public void Dispose() => Directory.Delete(_temporaryDirectory, recursive: true);

    private CalloutsMediaAddOnService CreateService(
        IReadOnlyList<string> currentNames,
        IReadOnlyList<string> retiredNames)
    {
        string inputDirectory = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, $"input-{Guid.NewGuid():N}")).FullName;
        string curatedPath = Path.Combine(inputDirectory, "Callouts_Sounds.json");
        string overridesPath = Path.Combine(inputDirectory, "CalloutPronunciations.json");
        string lorrgsPath = Path.Combine(inputDirectory, "lorrgs-vocabulary.txt");
        string retiredPath = Path.Combine(inputDirectory, "RetiredCallouts.json");

        File.WriteAllText(curatedPath, "[]");
        File.WriteAllText(overridesPath, "{}");
        File.WriteAllLines(lorrgsPath, currentNames);
        File.WriteAllText(retiredPath, System.Text.Json.JsonSerializer.Serialize(retiredNames));

        CalloutsVocabularyProvider vocabulary =
            new(curatedPath, overridesPath, lorrgsPath, retiredPath);
        return new CalloutsMediaAddOnService(new StubOptions(Settings), vocabulary);
    }

    private sealed class StubOptions(AddOnSettings settings) : IOptionsSnapshot<AddOnSettings>
    {
        public AddOnSettings Value => settings;

        public AddOnSettings Get(string? name) => settings;
    }
}
