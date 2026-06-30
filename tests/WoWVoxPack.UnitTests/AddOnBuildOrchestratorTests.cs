using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.Core.Builder;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class AddOnBuildOrchestratorTests : IDisposable
{
    private readonly string _tempDirectory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

    [Fact]
    public async Task RunAsync_CallsBuildAddOnAsync_ForEveryServiceAndTtsSettingsCombination()
    {
        FakeAddOnService service1 = new((dir, tts) => BuildSimpleAddOn(dir, tts, "Service1"));
        FakeAddOnService service2 = new((dir, tts) => BuildSimpleAddOn(dir, tts, "Service2"));
        FakeSoundFileService soundFileService = new();
        BuildMatrix buildMatrix = new()
        {
            TtsSettings =
            [
                new TtsSettings { Voice = VoiceName.Neural2_C },
                new TtsSettings { Voice = VoiceName.Wavenet_A }
            ]
        };

        AddOnBuildOrchestrator orchestrator = new(
            NullLogger<AddOnBuildOrchestrator>.Instance,
            [service1, service2],
            Options.Create(buildMatrix),
            soundFileService,
            _tempDirectory);

        await orchestrator.RunAsync(CancellationToken.None);

        Assert.Equal(2, service1.CallCount);
        Assert.Equal(2, service2.CallCount);
    }

    [Fact]
    public async Task RunAsync_WritesAddOnFiles_AndCreatesMissingSoundFiles()
    {
        SoundFile soundFile = new("alert.ogg", text: "Alert", displayName: "Alert");
        AddOnSettings settings = DefaultSettings("Test_AddOn");

        FakeAddOnService service = new((dir, tts) =>
            new AddOnBuilder(settings, tts)
                .AddSoundFile(soundFile)
                .AddFile("Core.lua", _ => "-- generated lua")
                .Build(dir));
        FakeSoundFileService soundFileService = new();
        BuildMatrix buildMatrix = new() { TtsSettings = [new TtsSettings { Voice = VoiceName.Neural2_C }] };

        AddOnBuildOrchestrator orchestrator = new(
            NullLogger<AddOnBuildOrchestrator>.Instance,
            [service],
            Options.Create(buildMatrix),
            soundFileService,
            _tempDirectory);

        await orchestrator.RunAsync(CancellationToken.None);

        string addOnDirectory = Path.Combine(_tempDirectory, "Neural2_C", "Test_AddOn");
        Assert.True(File.Exists(Path.Combine(addOnDirectory, "Test_AddOn.toc")));
        Assert.Equal("-- generated lua", await File.ReadAllTextAsync(Path.Combine(addOnDirectory, "Core.lua")));
        Assert.Single(soundFileService.CreatedSoundFiles);
        Assert.Equal("alert.ogg", soundFileService.CreatedSoundFiles[0].FileName);
    }

    [Fact]
    public async Task RunAsync_DoesNotResynthesizeSoundFile_WhenContentUnchangedOnSecondRun()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");
        SoundFile soundFile = new("alert.ogg", text: "Alert", displayName: "Alert");

        FakeAddOnService service = new((dir, tts) =>
            new AddOnBuilder(settings, tts).AddSoundFile(soundFile).Build(dir));
        FakeSoundFileService soundFileService = new();
        BuildMatrix buildMatrix = new() { TtsSettings = [new TtsSettings { Voice = VoiceName.Neural2_C }] };

        AddOnBuildOrchestrator orchestrator = new(
            NullLogger<AddOnBuildOrchestrator>.Instance,
            [service],
            Options.Create(buildMatrix),
            soundFileService,
            _tempDirectory);

        await orchestrator.RunAsync(CancellationToken.None);
        Assert.Single(soundFileService.CreatedSoundFiles);

        await orchestrator.RunAsync(CancellationToken.None);
        Assert.Single(soundFileService.CreatedSoundFiles);
    }

    [Fact]
    public async Task RunAsync_ResynthesizesSoundFile_WhenContentChangesOnSecondRun()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");
        FakeSoundFileService soundFileService = new();
        BuildMatrix buildMatrix = new() { TtsSettings = [new TtsSettings { Voice = VoiceName.Neural2_C }] };
        string text = "Alert";

        FakeAddOnService service = new((dir, tts) =>
            new AddOnBuilder(settings, tts)
                .AddSoundFile(new SoundFile("alert.ogg", text: text, displayName: "Alert"))
                .Build(dir));

        AddOnBuildOrchestrator orchestrator = new(
            NullLogger<AddOnBuildOrchestrator>.Instance,
            [service],
            Options.Create(buildMatrix),
            soundFileService,
            _tempDirectory);

        await orchestrator.RunAsync(CancellationToken.None);
        Assert.Single(soundFileService.CreatedSoundFiles);

        text = "Alert, now with new text";
        await orchestrator.RunAsync(CancellationToken.None);

        Assert.Equal(2, soundFileService.CreatedSoundFiles.Count);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }

    private static AddOnSettings DefaultSettings(string title)
    {
        return new AddOnSettings
        {
            Title = title,
            Version = "12.0.7",
            Author = "Tester",
            Notes = "A test addon."
        };
    }

    private static AddOn BuildSimpleAddOn(string outputDirectory, TtsSettings ttsSettings, string title)
    {
        return new AddOnBuilder(DefaultSettings(title), ttsSettings).Build(outputDirectory);
    }

    private sealed class FakeAddOnService(Func<string, TtsSettings, AddOn> buildAddOn) : IAddOnService
    {
        public int CallCount { get; private set; }

        public Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(buildAddOn(outputDirectoryBase, ttsSettings));
        }
    }

    private sealed class FakeSoundFileService : ISoundFileService
    {
        public List<SoundFile> CreatedSoundFiles { get; } = [];

        public Task CreateSoundFileAsync(SoundFile soundFile, string outputDirectory, TtsSettings settings,
            CancellationToken cancellationToken = default)
        {
            CreatedSoundFiles.Add(soundFile);
            File.WriteAllText(Path.Combine(outputDirectory, soundFile.FileName), "fake audio");
            return Task.CompletedTask;
        }
    }
}
