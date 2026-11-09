using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.Core.Builder;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class AddOnBuildOrchestratorTests : IDisposable
{
    private readonly string _solutionDirectory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

    private readonly string _tempDirectory;

    public AddOnBuildOrchestratorTests()
    {
        _tempDirectory = Directory.CreateDirectory(Path.Combine(_solutionDirectory, "output")).FullName;
    }

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
            EmptyResolver(),
            _solutionDirectory,
            [GameFlavor.Retail]);

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
                .BuildDraft(dir));
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
            new AddOnBuilder(settings, tts).AddSoundFile(soundFile).BuildDraft(dir));
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
                .BuildDraft(dir));

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
                .BuildDraft(dir));

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
        FakeAddOnService service = new((dir, tts) => new AddOnBuilder(settings, tts).BuildDraft(dir));

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

            return builder.BuildDraft(dir);
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
                .BuildDraft(dir));

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

            return builder.BuildDraft(dir);
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

    /// <summary>
    /// A composed recording is cut from files the same build may be rendering, and renders run
    /// many at a time, so it has to wait for every one of them rather than take its turn.
    /// </summary>
    [Fact]
    public async Task RunAsync_ComposesARecording_OnlyAfterEverySourceIsRendered()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");
        FakeSoundFileService soundFileService = new();
        const int digits = 40;

        // Registered ahead of its sources, so taking the entries in order would compose it first.
        FakeAddOnService service = new((dir, tts) =>
        {
            AddOnBuilder builder = new AddOnBuilder(settings, tts)
                .AddSoundFile(new SoundFile("countdown.ogg", displayName: "Countdown",
                    composition: new SoundComposition(digits + 1,
                        Enumerable.Range(0, digits).Select(i => new SoundCompositionPart($"digit{i}.ogg", i)).ToArray())));
            for (int i = 0; i < digits; i++)
            {
                builder.AddSoundFile(new SoundFile($"digit{i}.ogg", text: $"Digit {i}", displayName: $"Digit {i}"));
            }

            return builder.BuildDraft(dir);
        });

        await Orchestrator(service, soundFileService).RunAsync(CancellationToken.None);

        Assert.Equal(digits + 1, soundFileService.CreatedSoundFiles.Count);
        Assert.Equal("countdown.ogg", soundFileService.CreatedSoundFiles[^1].FileName);
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
                .BuildDraft(dir));

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
                .BuildDraft(dir));

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
                .BuildDraft(dir));

        AddOnBuildOrchestrator orchestrator = Orchestrator(service, soundFileService);

        await Assert.ThrowsAsync<HttpRequestException>(() => orchestrator.RunAsync(CancellationToken.None));

        // The manifest still describes the last complete build, so the next run retries the file
        // instead of trusting a half-written folder.
        Assert.False(File.Exists(Path.Combine(_tempDirectory, "Neural2_C", "Test_AddOn", "SoundFiles.json")));
    }

    [Fact]
    public async Task RunAsync_RecordsTheRendersItFinished_WhenTheBuildFailsPartway()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");
        string suffix = string.Empty;
        FakeAddOnService service = new((dir, tts) =>
            new AddOnBuilder(settings, tts)
                .AddSoundFile(new SoundFile("alert.ogg", text: "Alert" + suffix, displayName: "Alert"))
                .AddSoundFile(new SoundFile("soak.ogg", text: "Soak" + suffix, displayName: "Soak"))
                .BuildDraft(dir));
        await Orchestrator(service, new FakeSoundFileService()).RunAsync(CancellationToken.None);

        // Both files change, so without a record of the one that finished the retry would pay
        // for it again: the last complete manifest still has its old text.
        suffix = ", reworded";
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Orchestrator(service, new FakeSoundFileService(failsFor: "soak.ogg")).RunAsync(CancellationToken.None));

        string manifestPath = Path.Combine(_tempDirectory, "Neural2_C", "Test_AddOn", "SoundFiles.json");
        Dictionary<string, string?> recorded = AddOnBuilder.LoadSoundFileJson(manifestPath)
            .ToDictionary(f => f.FileName, f => f.Text);
        Assert.Equal("Alert, reworded", recorded["alert.ogg"]);
        Assert.Equal("Soak", recorded["soak.ogg"]);

        FakeSoundFileService retry = new();
        await Orchestrator(service, retry).RunAsync(CancellationToken.None);

        Assert.Equal(["soak.ogg"], retry.CreatedSoundFiles.Select(f => f.FileName));
    }

    [Fact]
    public async Task RunAsync_RecomposesARecording_WhoseSourcesAPartialBuildReRendered()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");
        string one = "One";
        bool withSoak = false;
        FakeAddOnService service = new((dir, tts) =>
        {
            AddOnBuilder builder = new AddOnBuilder(settings, tts)
                .AddSoundFile(new SoundFile("one.ogg", text: one, displayName: "One"))
                .AddSoundFile(new SoundFile("two.ogg", text: "Two", displayName: "Two"))
                .AddSoundFile(new SoundFile("count.ogg", displayName: "Count",
                    composition: new SoundComposition(2,
                        [new SoundCompositionPart("two.ogg", 0), new SoundCompositionPart("one.ogg", 1)])));
            if (withSoak)
            {
                builder.AddSoundFile(new SoundFile("soak.ogg", text: "Soak", displayName: "Soak"));
            }

            return builder.BuildDraft(dir);
        });
        await Orchestrator(service, new FakeSoundFileService()).RunAsync(CancellationToken.None);

        // one.ogg re-renders and is recorded, but the build fails before the composing pass, so
        // count.ogg is still cut from the old take.
        one = "Wun";
        withSoak = true;
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Orchestrator(service, new FakeSoundFileService(failsFor: "soak.ogg")).RunAsync(CancellationToken.None));

        FakeSoundFileService retry = new();
        await Orchestrator(service, retry).RunAsync(CancellationToken.None);

        Assert.Equal(["count.ogg", "soak.ogg"], retry.CreatedSoundFiles.Select(f => f.FileName).Order());
    }

    [Fact]
    public async Task RunAsync_RetiresAFile_AfterAPartialBuildKeptItInTheManifest()
    {
        AddOnSettings settings = DefaultSettings("Test_AddOn");
        bool registerOld = true;
        FakeAddOnService service = new((dir, tts) =>
        {
            AddOnBuilder builder = new AddOnBuilder(settings, tts)
                .AddSoundFile(new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"));
            if (registerOld)
            {
                builder.AddSoundFile(new SoundFile("old.ogg", text: "Old", displayName: "Old"));
            }
            else
            {
                builder.AddSoundFile(new SoundFile("new.ogg", text: "New", displayName: "New"))
                    .AddSoundFile(new SoundFile("soak.ogg", text: "Soak", displayName: "Soak"));
            }

            return builder.BuildDraft(dir);
        });
        await Orchestrator(service, new FakeSoundFileService()).RunAsync(CancellationToken.None);

        registerOld = false;
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Orchestrator(service, new FakeSoundFileService(failsFor: "soak.ogg")).RunAsync(CancellationToken.None));
        await Orchestrator(service, new FakeSoundFileService()).RunAsync(CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(_tempDirectory, "Neural2_C", "Test_AddOn", "Sounds", "old.ogg")));
    }

    [Fact]
    public async Task RunAsync_BuildsForeverOnlyForIncludedAddOns()
    {
        FakeAddOnService serviceA = AlertService("A");
        FakeAddOnService serviceB = AlertService("B");
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
            [serviceA, serviceB],
            Options.Create(buildMatrix),
            new FakeSoundFileService(),
            EmptyResolver(),
            _solutionDirectory,
            [GameFlavor.Retail, ForeverFor("A")]);

        await orchestrator.RunAsync(CancellationToken.None);

        Assert.Equal(
            [
                ("Retail", VoiceName.Neural2_C), ("Retail", VoiceName.Wavenet_A),
                ("Forever", VoiceName.Neural2_C), ("Forever", VoiceName.Wavenet_A)
            ],
            serviceA.Calls);
        Assert.Equal([("Retail", VoiceName.Neural2_C), ("Retail", VoiceName.Wavenet_A)], serviceB.Calls);
    }

    [Fact]
    public async Task RunAsync_ForeverReusesRetailRecordingsWithoutRendering()
    {
        FakeSoundFileService soundFileService = new();

        await Orchestrator([AlertService("A")], soundFileService, [GameFlavor.Retail, ForeverFor("A")])
            .RunAsync(CancellationToken.None);

        Assert.Equal(["alert.ogg", "soak.ogg"], soundFileService.CreatedSoundFiles.Select(f => f.FileName).Order());
        string retailSounds = Path.Combine(_tempDirectory, "Neural2_C", "A_AddOn", "Sounds");
        string foreverSounds = Path.Combine(_solutionDirectory, "output-forever", "Neural2_C", "A_AddOn", "Sounds");
        foreach (string fileName in new[] { "alert.ogg", "soak.ogg" })
        {
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(retailSounds, fileName)),
                await File.ReadAllBytesAsync(Path.Combine(foreverSounds, fileName)));
        }

        string foreverToc = Path.Combine(_solutionDirectory, "output-forever", "Neural2_C", "A_AddOn", "A_AddOn.toc");
        Assert.StartsWith("## Interface: 16001\n## AllowLoadGameType: camelot\n",
            await File.ReadAllTextAsync(foreverToc));
    }

    [Fact]
    public async Task RunAsync_DryRunPlansZeroForeverRendersWhenRetailCovers()
    {
        await Orchestrator([AlertService("A")], new FakeSoundFileService(), [GameFlavor.Retail])
            .RunAsync(CancellationToken.None);

        ListLogger logger = new();
        FakeSoundFileService dryRunSoundFileService = new();
        await Orchestrator([AlertService("A")], dryRunSoundFileService, [GameFlavor.Retail, ForeverFor("A")],
            dryRun: true, logger: logger).RunAsync(CancellationToken.None);

        Assert.Empty(dryRunSoundFileService.CreatedSoundFiles);
        Assert.Equal(2, logger.Messages.Count(m => m == "[dry run] A_AddOn: 0 to render, 0 to remove"));
        Assert.Contains("[dry run]   reuse alert.ogg", logger.Messages);
        Assert.Contains("[dry run]   reuse soak.ogg", logger.Messages);
        Assert.False(Directory.Exists(Path.Combine(_solutionDirectory, "output-forever")));
    }

    [Fact]
    public async Task RunAsync_Throws_WhenAForeverAddOnHasNoRetailPartner()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Orchestrator([AlertService("A")], new FakeSoundFileService(), [ForeverFor("A")])
                .RunAsync(CancellationToken.None));
    }

    public void Dispose()
    {
        Directory.Delete(_solutionDirectory, true);
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

    private static AddOnDraft BuildSimpleAddOn(string outputDirectory, TtsSettings ttsSettings, string title)
    {
        return new AddOnBuilder(DefaultSettings(title), ttsSettings).BuildDraft(outputDirectory);
    }

    private static PronunciationResolver EmptyResolver()
    {
        return new PronunciationResolver(PronunciationCatalog.Empty,
            NullLogger<PronunciationResolver>.Instance);
    }

    private static GameFlavor ForeverFor(params string[] addOnIds)
    {
        return GameFlavor.Forever(new ForeverSettings
        {
            Interface = "16001",
            AllowLoadGameType = "camelot",
            AddOns = [.. addOnIds]
        });
    }

    private static FakeAddOnService AlertService(string addOnId)
    {
        AddOnSettings settings = DefaultSettings($"{addOnId}_AddOn");
        return new FakeAddOnService(addOnId, (dir, tts, flavor) =>
            new AddOnBuilder(settings, tts, addOnId)
                .ForFlavor(flavor)
                .AddSoundFile(new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"))
                .AddSoundFile(new SoundFile("soak.ogg", text: "Soak", displayName: "Soak"))
                .BuildDraft(dir));
    }

    private AddOnBuildOrchestrator Orchestrator(IAddOnService service, ISoundFileService soundFileService,
        TtsSettings? ttsSettings = null)
    {
        return Orchestrator([service], soundFileService, [GameFlavor.Retail], ttsSettings: ttsSettings);
    }

    private AddOnBuildOrchestrator Orchestrator(IEnumerable<IAddOnService> services,
        ISoundFileService soundFileService, IReadOnlyList<GameFlavor> flavors, bool dryRun = false,
        ILogger<AddOnBuildOrchestrator>? logger = null, TtsSettings? ttsSettings = null)
    {
        return new AddOnBuildOrchestrator(
            logger ?? NullLogger<AddOnBuildOrchestrator>.Instance,
            services,
            Options.Create(new BuildMatrix
            {
                DryRun = dryRun,
                TtsSettings = [ttsSettings ?? new TtsSettings { Voice = VoiceName.Neural2_C }]
            }),
            soundFileService,
            EmptyResolver(),
            _solutionDirectory,
            flavors);
    }

    private sealed class FakeAddOnService(string addOnId, Func<string, TtsSettings, GameFlavor, AddOnDraft> buildAddOn)
        : IAddOnService
    {
        public FakeAddOnService(Func<string, TtsSettings, AddOnDraft> buildAddOn)
            : this("Test_AddOn", (dir, tts, _) => buildAddOn(dir, tts))
        {
        }

        public string AddOnId { get; } = addOnId;

        public List<(string Flavor, VoiceName? Voice)> Calls { get; } = [];

        public int CallCount => Calls.Count;

        public Task<AddOnDraft> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
            GameFlavor flavor, CancellationToken cancellationToken = default)
        {
            Calls.Add((flavor.Name, ttsSettings.Voice));
            return Task.FromResult(buildAddOn(outputDirectoryBase, ttsSettings, flavor));
        }
    }

    private sealed class ListLogger : ILogger<AddOnBuildOrchestrator>
    {
        private readonly Lock _gate = new();
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_gate)
                {
                    return _messages.ToArray();
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_gate)
            {
                _messages.Add(formatter(state, exception));
            }
        }
    }

    /// <summary>Renders run in parallel, so the record of them has to survive that.</summary>
    private sealed class FakeSoundFileService(int failuresPerFile = 0, Func<Exception>? failWith = null,
        string? failsFor = null)
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
                if (failed < failuresPerFile ||
                    string.Equals(soundFile.FileName, failsFor, StringComparison.OrdinalIgnoreCase))
                {
                    _failures[soundFile.FileName] = failed + 1;
                    throw failWith?.Invoke() ?? new HttpRequestException("the provider is having a moment");
                }

                _createdSoundFiles.Add(soundFile);
            }

            foreach (SoundCompositionPart part in soundFile.Composition?.Parts ?? [])
            {
                Assert.True(File.Exists(Path.Combine(outputDirectory, part.FileName)),
                    $"{soundFile.FileName} was composed before {part.FileName} was rendered");
            }

            File.WriteAllText(Path.Combine(outputDirectory, soundFile.FileName), "fake audio");
            return Task.CompletedTask;
        }
    }
}
