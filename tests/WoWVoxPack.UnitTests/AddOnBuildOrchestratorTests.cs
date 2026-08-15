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
        AddOnBuildOrchestrator orchestrator = Orchestrator(service, soundFileService);

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
        AddOnBuildOrchestrator orchestrator = Orchestrator(service, soundFileService);

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
        string text = "Alert";

        FakeAddOnService service = new((dir, tts) =>
            new AddOnBuilder(settings, tts)
                .AddSoundFile(new SoundFile("alert.ogg", text: text, displayName: "Alert"))
                .Build(dir));

        AddOnBuildOrchestrator orchestrator = Orchestrator(service, soundFileService);

        await orchestrator.RunAsync(CancellationToken.None);
        Assert.Single(soundFileService.CreatedSoundFiles);

        text = "Alert, now with new text";
        await orchestrator.RunAsync(CancellationToken.None);

        Assert.Equal(2, soundFileService.CreatedSoundFiles.Count);
    }

    [Fact]
    public async Task RunAsync_ResynthesizesEverySound_WhenTheVoiceRecipeChanges()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");
        FakeSoundFileService soundFileService = new();
        float speakingRate = 1.0f;

        FakeAddOnService service = new((dir, tts) =>
            new AddOnBuilder(settings, tts)
                .AddSoundFile(new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"))
                .AddSoundFile(new SoundFile("adds.ogg", text: "Adds", displayName: "Adds"))
                .Build(dir));

        AddOnBuildOrchestrator AtCurrentRate() => Orchestrator(service, soundFileService,
            new TtsSettings { Voice = VoiceName.Neural2_C, SpeakingRate = speakingRate });

        await AtCurrentRate().RunAsync(CancellationToken.None);
        Assert.Equal(2, soundFileService.CreatedSoundFiles.Count);

        speakingRate = 1.2f;
        await AtCurrentRate().RunAsync(CancellationToken.None);

        Assert.Equal(4, soundFileService.CreatedSoundFiles.Count);
    }

    [Fact]
    public async Task RunAsync_WritesTheRecipe_BesideTheAddOnDirectorySoItIsNeverPackaged()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");
        FakeAddOnService service = new((dir, tts) => new AddOnBuilder(settings, tts).Build(dir));

        AddOnBuildOrchestrator orchestrator = Orchestrator(service, new FakeSoundFileService());

        await orchestrator.RunAsync(CancellationToken.None);

        string voiceDirectory = Path.Combine(_tempDirectory, "Neural2_C");
        Assert.True(File.Exists(Path.Combine(voiceDirectory, "Test_AddOn.recipe.json")));
        Assert.False(File.Exists(Path.Combine(voiceDirectory, "Test_AddOn", "Test_AddOn.recipe.json")));
    }

    [Fact]
    public async Task RunAsync_DeletesTheRecording_WhenTheAddOnStopsRegisteringIt()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");
        FakeSoundFileService soundFileService = new();
        bool includeRetiredSound = true;

        FakeAddOnService service = new((dir, tts) =>
        {
            AddOnBuilder builder = new AddOnBuilder(settings, tts)
                .AddSoundFile(new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"));

            if (includeRetiredSound)
            {
                builder.AddSoundFile(new SoundFile("dropped.ogg", text: "Dropped", displayName: "Dropped"));
            }

            return builder.Build(dir);
        });

        AddOnBuildOrchestrator orchestrator = Orchestrator(service, soundFileService);

        await orchestrator.RunAsync(CancellationToken.None);
        string soundDirectory = Path.Combine(_tempDirectory, "Neural2_C", "Test_AddOn", "Sounds");
        Assert.True(File.Exists(Path.Combine(soundDirectory, "dropped.ogg")));

        includeRetiredSound = false;
        await orchestrator.RunAsync(CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(soundDirectory, "dropped.ogg")));
        Assert.True(File.Exists(Path.Combine(soundDirectory, "alert.ogg")));
    }

    [Fact]
    public async Task RunAsync_LeavesAlone_ARecordingTheManifestNeverKnewAbout()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");
        FakeAddOnService service = new((dir, tts) =>
            new AddOnBuilder(settings, tts)
                .AddSoundFile(new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"))
                .Build(dir));

        AddOnBuildOrchestrator orchestrator = Orchestrator(service, new FakeSoundFileService());

        await orchestrator.RunAsync(CancellationToken.None);

        string soundDirectory = Path.Combine(_tempDirectory, "Neural2_C", "Test_AddOn", "Sounds");
        string handPlaced = Path.Combine(soundDirectory, "alerty.ogg");
        await File.WriteAllTextAsync(handPlaced, "hand placed audio");

        await orchestrator.RunAsync(CancellationToken.None);

        Assert.True(File.Exists(handPlaced));
    }

    [Fact]
    public async Task RunAsync_RendersNothing_WhenAPackLosesMostOfItsVocabulary()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");
        FakeSoundFileService soundFileService = new();
        int soundCount = 40;

        // The survivor changes too, so a build that renders before it checks would bill for it.
        string survivorText = "Spell 0";

        FakeAddOnService service = new((dir, tts) =>
        {
            AddOnBuilder builder = new(settings, tts);
            builder.AddSoundFile(new SoundFile("0.ogg", text: survivorText, displayName: "Spell 0"));
            for (int i = 1; i < soundCount; i++)
            {
                builder.AddSoundFile(new SoundFile($"{i}.ogg", text: $"Spell {i}", displayName: $"Spell {i}"));
            }

            return builder.Build(dir);
        });

        AddOnBuildOrchestrator orchestrator = Orchestrator(service, soundFileService);

        await orchestrator.RunAsync(CancellationToken.None);
        Assert.Equal(40, soundFileService.CreatedSoundFiles.Count);

        soundCount = 1;
        survivorText = "Spell 0, rewritten";
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.RunAsync(CancellationToken.None));

        // Nothing was rendered and nothing was deleted: the collapse is caught before either.
        Assert.Equal(40, soundFileService.CreatedSoundFiles.Count);
        Assert.Equal(40,
            Directory.GetFiles(Path.Combine(_tempDirectory, "Neural2_C", "Test_AddOn", "Sounds")).Length);
    }

    [Fact]
    public async Task RunAsync_RetriesARender_WhenTheProviderTimesOut()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");

        // A client-side deadline surfaces as TaskCanceledException, which is an
        // OperationCanceledException and so is easily mistaken for the caller giving up.
        FakeSoundFileService soundFileService = new(failuresPerFile: 1, failWith: () => new TaskCanceledException());

        FakeAddOnService service = new((dir, tts) =>
            new AddOnBuilder(settings, tts)
                .AddSoundFile(new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"))
                .Build(dir));

        AddOnBuildOrchestrator orchestrator = Orchestrator(service, soundFileService);

        await orchestrator.RunAsync(CancellationToken.None);

        Assert.Single(soundFileService.CreatedSoundFiles);
    }

    [Fact]
    public async Task RunAsync_RetriesARender_WhenTheProviderFailsOnce()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");
        FakeSoundFileService soundFileService = new(failuresPerFile: 1);

        FakeAddOnService service = new((dir, tts) =>
            new AddOnBuilder(settings, tts)
                .AddSoundFile(new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"))
                .Build(dir));

        AddOnBuildOrchestrator orchestrator = Orchestrator(service, soundFileService);

        await orchestrator.RunAsync(CancellationToken.None);

        Assert.Single(soundFileService.CreatedSoundFiles);
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "Neural2_C", "Test_AddOn", "Sounds", "alert.ogg")));
    }

    [Fact]
    public async Task RunAsync_Throws_WhenARenderKeepsFailing()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");
        FakeSoundFileService soundFileService = new(failuresPerFile: int.MaxValue);

        FakeAddOnService service = new((dir, tts) =>
            new AddOnBuilder(settings, tts)
                .AddSoundFile(new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"))
                .Build(dir));

        AddOnBuildOrchestrator orchestrator = Orchestrator(service, soundFileService);

        await Assert.ThrowsAsync<HttpRequestException>(() => orchestrator.RunAsync(CancellationToken.None));

        // The manifest still describes the last complete build, so the next run retries the file
        // instead of trusting a half-written folder.
        Assert.False(File.Exists(Path.Combine(_tempDirectory, "Neural2_C", "Test_AddOn", "SoundFiles.json")));
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

    private AddOnBuildOrchestrator Orchestrator(IAddOnService service, ISoundFileService soundFileService,
        TtsSettings? ttsSettings = null)
    {
        return new AddOnBuildOrchestrator(
            NullLogger<AddOnBuildOrchestrator>.Instance,
            [service],
            Options.Create(new BuildMatrix
            {
                TtsSettings = [ttsSettings ?? new TtsSettings { Voice = VoiceName.Neural2_C }]
            }),
            soundFileService,
            _tempDirectory);
    }

    private sealed class FakeAddOnService(Func<string, TtsSettings, AddOn> buildAddOn)
        : IAddOnService
    {
        public int CallCount { get; private set; }

        public Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(buildAddOn(outputDirectoryBase, ttsSettings));
        }
    }

    /// <summary>Renders run in parallel, so the record of them has to survive that.</summary>
    private sealed class FakeSoundFileService(int failuresPerFile = 0, Func<Exception>? failWith = null)
        : ISoundFileService
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<string, int> _failures = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<SoundFile> _createdSoundFiles = [];

        public IReadOnlyList<SoundFile> CreatedSoundFiles
        {
            get
            {
                lock (_gate)
                {
                    return _createdSoundFiles.ToArray();
                }
            }
        }

        public Task CreateSoundFileAsync(SoundFile soundFile, string outputDirectory, TtsSettings settings,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _failures.TryGetValue(soundFile.FileName, out int failed);
                if (failed < failuresPerFile)
                {
                    _failures[soundFile.FileName] = failed + 1;
                    throw failWith?.Invoke() ?? new HttpRequestException("the provider is having a moment");
                }

                _createdSoundFiles.Add(soundFile);
            }

            File.WriteAllText(Path.Combine(outputDirectory, soundFile.FileName), "fake audio");
            return Task.CompletedTask;
        }
    }
}
