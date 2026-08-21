using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.AddOns.Callouts;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public sealed class NorthernSkyRaidToolsAddOnServiceTests : IDisposable
{
    private readonly string _temporaryDirectory = Directory.CreateTempSubdirectory().FullName;

    [Fact]
    public async Task BuildAddOnAsync_CreatesASelfContainedPerVoicePack()
    {
        string vocabularyPath = Path.Combine(_temporaryDirectory, "nsrt-vocabulary.txt");
        string overridesPath = Path.Combine(_temporaryDirectory, "CalloutPronunciations.json");
        File.WriteAllText(vocabularyPath, "DropPool\n");
        File.WriteAllText(overridesPath, "{\"DropPool\":{\"Text\":\"Drop Pool\"}}");
        NorthernSkyRaidToolsAddOnService service = new(
            new StubOptions(new AddOnSettings
            {
                Title = "unused",
                Version = "12.0.7",
                Author = "Tester",
                Notes = "Test."
            }),
            new NorthernSkyRaidToolsVocabularyProvider([vocabularyPath], overridesPath));

        AddOn addOn = await service.BuildAddOnAsync(
            _temporaryDirectory,
            new TtsSettings { Voice = VoiceName.Studio_O });

        Assert.Equal("WoWVoxPacks_NorthernSkyRaidTools_Studio_O", addOn.Title);
        Assert.Equal("WoWVoxPacks_NorthernSkyRaidTools_Studio_O", addOn.AddOnDirectoryName);
        Assert.Equal("WoWVoxPacks_NorthernSkyRaidTools_Studio_O.toc", addOn.TocFileName);
        Assert.Equal("drop_pool.ogg", Assert.Single(addOn.SoundFiles).FileName);
        Assert.Contains(
            "LSM:Register(\"sound\", \"DropPool\", path .. \"drop_pool.ogg\")",
            addOn.GetFileContent("Core.lua"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAddOnAsync_ReusesOneSoundFileForCaseDistinctMediaKeys()
    {
        string vocabularyPath = Path.Combine(_temporaryDirectory, "nsrt-vocabulary.txt");
        string overridesPath = Path.Combine(_temporaryDirectory, "CalloutPronunciations.json");
        File.WriteAllText(vocabularyPath, "Soak\nsoak\n");
        File.WriteAllText(overridesPath, "{}");
        NorthernSkyRaidToolsAddOnService service = new(
            new StubOptions(new AddOnSettings
            {
                Title = "unused",
                Version = "12.0.7",
                Author = "Tester"
            }),
            new NorthernSkyRaidToolsVocabularyProvider([vocabularyPath], overridesPath));

        AddOn addOn = await service.BuildAddOnAsync(
            _temporaryDirectory,
            new TtsSettings { Voice = VoiceName.Studio_O });

        Assert.Single(addOn.SoundFiles);
        Assert.Equal("soak.ogg", Assert.Single(addOn.SoundFiles).FileName);
        Assert.Contains("LSM:Register(\"sound\", \"Soak\", path .. \"soak.ogg\")",
            addOn.GetFileContent("Core.lua"), StringComparison.Ordinal);
        Assert.Contains("LSM:Register(\"sound\", \"soak\", path .. \"soak.ogg\")",
            addOn.GetFileContent("Core.lua"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAddOnAsync_RegistersARetiredCalloutKeyOnlyWhileItsRecordingShips()
    {
        string vocabularyPath = Path.Combine(_temporaryDirectory, "nsrt-vocabulary.txt");
        string overridesPath = Path.Combine(_temporaryDirectory, "CalloutPronunciations.json");
        File.WriteAllText(vocabularyPath, "DropPool\n");
        File.WriteAllText(overridesPath, "{}");
        File.WriteAllText(Path.Combine(_temporaryDirectory, "Callouts_Sounds.json"), "[]");
        File.WriteAllText(Path.Combine(_temporaryDirectory, "lorrgs-vocabulary.txt"), string.Empty);
        File.WriteAllText(Path.Combine(_temporaryDirectory, "RetiredCallouts.json"), "[\"OldCallout\"]");
        NorthernSkyRaidToolsAddOnService service = new(
            new StubOptions(new AddOnSettings
            {
                Title = "unused",
                Version = "12.0.7",
                Author = "Tester"
            }),
            new NorthernSkyRaidToolsVocabularyProvider([vocabularyPath], overridesPath,
                new CalloutsVocabularyProvider(
                    Path.Combine(_temporaryDirectory, "Callouts_Sounds.json"),
                    overridesPath,
                    Path.Combine(_temporaryDirectory, "lorrgs-vocabulary.txt"),
                    Path.Combine(_temporaryDirectory, "RetiredCallouts.json"))));
        TtsSettings ttsSettings = new() { Voice = VoiceName.Studio_O };

        AddOn withoutRecording = await service.BuildAddOnAsync(_temporaryDirectory, ttsSettings);

        Assert.DoesNotContain("OldCallout", withoutRecording.GetFileContent("Core.lua"),
            StringComparison.Ordinal);

        Directory.CreateDirectory(withoutRecording.SoundDirectory);
        File.WriteAllBytes(Path.Combine(withoutRecording.SoundDirectory, "old_callout.ogg"), [1]);
        AddOn withRecording = await service.BuildAddOnAsync(_temporaryDirectory, ttsSettings);

        Assert.Contains("LSM:Register(\"sound\", \"OldCallout\", path .. \"old_callout.ogg\")",
            withRecording.GetFileContent("Core.lua"), StringComparison.Ordinal);
        Assert.Contains(withRecording.SoundFiles, f => f.FileName == "old_callout.ogg");
    }

    [Fact]
    public async Task BuildAddOnAsync_KeepsARetiredKeyWhoseFileShipsUnderANativeKey()
    {
        string vocabularyPath = Path.Combine(_temporaryDirectory, "nsrt-vocabulary.txt");
        string overridesPath = Path.Combine(_temporaryDirectory, "CalloutPronunciations.json");
        File.WriteAllText(vocabularyPath, "AntiMagicShell\n");
        File.WriteAllText(overridesPath,
            """
            {"AntiMagicShell":{"Ssml":"<speak>Anti-Magic Shell</speak>"},
             "Anti-Magic Shell":{"Ssml":"<speak>Anti-Magic Shell</speak>"}}
            """);
        File.WriteAllText(Path.Combine(_temporaryDirectory, "Callouts_Sounds.json"), "[]");
        File.WriteAllText(Path.Combine(_temporaryDirectory, "lorrgs-vocabulary.txt"), string.Empty);
        File.WriteAllText(Path.Combine(_temporaryDirectory, "RetiredCallouts.json"),
            "[\"Anti-Magic Shell\"]");
        NorthernSkyRaidToolsAddOnService service = new(
            new StubOptions(new AddOnSettings
            {
                Title = "unused",
                Version = "12.0.7",
                Author = "Tester"
            }),
            new NorthernSkyRaidToolsVocabularyProvider([vocabularyPath], overridesPath,
                new CalloutsVocabularyProvider(
                    Path.Combine(_temporaryDirectory, "Callouts_Sounds.json"),
                    overridesPath,
                    Path.Combine(_temporaryDirectory, "lorrgs-vocabulary.txt"),
                    Path.Combine(_temporaryDirectory, "RetiredCallouts.json"))));

        AddOn addOn = await service.BuildAddOnAsync(
            _temporaryDirectory, new TtsSettings { Voice = VoiceName.Studio_O });

        Assert.Equal("anti_magic_shell.ogg", Assert.Single(addOn.SoundFiles).FileName);
        Assert.Contains(
            "LSM:Register(\"sound\", \"Anti-Magic Shell\", path .. \"anti_magic_shell.ogg\")",
            addOn.GetFileContent("Core.lua"), StringComparison.Ordinal);
    }

    public void Dispose() => Directory.Delete(_temporaryDirectory, recursive: true);

    private sealed class StubOptions(AddOnSettings settings) : IOptionsSnapshot<AddOnSettings>
    {
        public AddOnSettings Value => settings;
        public AddOnSettings Get(string? name) => settings;
    }
}
