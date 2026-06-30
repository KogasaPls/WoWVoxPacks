# Worker Orchestrator Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract `Worker.StartAsync`'s build loop into a standalone `AddOnBuildOrchestrator` (testable without `IHostedService` or writes to the real `output/` directory), leaving `Worker` as a thin hosting wrapper, per `docs/superpowers/specs/2026-06-30-worker-orchestrator-extraction-design.md`.

**Architecture:** `AddOnBuildOrchestrator` (new, in `WoWVoxPack.Core`, namespace `WoWVoxPack.Builder`) takes `IEnumerable<IAddOnService>`, `IOptions<BuildMatrix>`, `ISoundFileService`, and an explicit `string outputDirectoryBase`, and exposes `RunAsync(CancellationToken)` containing the current loop body verbatim. `Worker` shrinks to constructing nothing — it just calls `orchestrator.RunAsync(...)` then stops the host. `Program.cs` resolves the real output path once (moved out of `Worker`'s constructor) and registers `AddOnBuildOrchestrator` via an explicit factory, since DI can't auto-inject a bare `string`.

**Tech Stack:** .NET 10, xUnit, Microsoft.Extensions.Hosting/Logging/Options, Ardalis.GuardClauses.

---

## Build state during this plan

Task 1 is purely additive (new files only) — the full solution stays green throughout. Task 2 rewrites `Worker.cs` and `Program.cs` together in one commit, since `Worker`'s constructor signature change and `Program.cs`'s registration change are interdependent; that task ends with a full solution build + test verification, same as the rest of this plan.

All `dotnet` commands need the sandbox workaround documented in this repo:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet <command> -m:1 /nr:false
```

Run test commands with escalated permissions if the sandbox blocks the VSTest socket.

---

### Task 1: `AddOnBuildOrchestrator`

**Files:**
- Create: `src/WoWVoxPack.Core/Builder/AddOnBuildOrchestrator.cs`
- Test: `tests/WoWVoxPack.UnitTests/AddOnBuildOrchestratorTests.cs`

`AddOnBuildOrchestrator` lives in the `WoWVoxPack.Core` project (so `WoWVoxPack.UnitTests`, which already references `WoWVoxPack.Core`, can test it without a new project reference) under the `WoWVoxPack.Builder` namespace (this codebase already places namespaces independently of project boundaries — `AddOnTocFile` lives in Core under `WoWVoxPack.AddOns`). It contains `Worker.StartAsync`'s current loop body verbatim, minus the final `ApplicationLifetime.StopApplication()` call, with the trailing log message trimmed since stopping the host is no longer this class's concern.

This task does not touch `Worker.cs` or `Program.cs` — it's purely additive, so the full solution stays buildable throughout.

- [ ] **Step 1: Write the failing tests**

```csharp
namespace WoWVoxPack.UnitTests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.Builder;
using WoWVoxPack.TTS;

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
```

The `FakeSoundFileService` writes a dummy file to the target path when "creating" a sound file — this is required for the incremental-skip tests to be meaningful, since `SoundFileManifest.FilesToCreate` checks `File.Exists` on disk; without it, every run would look like "file missing" regardless of the manifest.

- [ ] **Step 2: Run tests to verify they fail to compile**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet test tests/WoWVoxPack.UnitTests/WoWVoxPack.UnitTests.csproj -m:1 /nr:false
```
Expected: FAIL to build, `error CS0246: The type or namespace name 'AddOnBuildOrchestrator' could not be found`.

- [ ] **Step 3: Implement `AddOnBuildOrchestrator`**

```csharp
using Ardalis.GuardClauses;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.Builder;

public class AddOnBuildOrchestrator(
    ILogger<AddOnBuildOrchestrator> logger,
    IEnumerable<IAddOnService> addOnServices,
    IOptions<BuildMatrix> buildMatrix,
    ISoundFileService soundFileService,
    string outputDirectoryBase)
{
    private ILogger<AddOnBuildOrchestrator> Logger { get; } = logger;
    private List<IAddOnService> AddOnServices { get; } = addOnServices.ToList();
    private BuildMatrix BuildMatrix { get; } = buildMatrix.Value;
    private ISoundFileService SoundFileService { get; } = soundFileService;
    private string OutputDirectoryBase { get; } = outputDirectoryBase;

    private IEnumerable<(IAddOnService addOnService, TtsSettings ttsSettings)> Matrix =>
        from addOnService in AddOnServices
        from ttsSettings in BuildMatrix.TtsSettings
        select (addOnService, ttsSettings);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        foreach ((IAddOnService addOnService, TtsSettings ttsSettings) in Matrix)
        {
            string outputDirectory =
                Path.Combine(OutputDirectoryBase, Guard.Against.Null(ttsSettings.Voice).ToString());
            AddOn addOn = await addOnService.BuildAddOnAsync(outputDirectory, ttsSettings, cancellationToken);

            Logger.LogInformation("Building {AddOnName} addon in directory {OutputDirectory}", addOn.Title,
                addOn.AddOnDirectory);

            await AddOnFileWriter.WriteAllFilesAsync(addOn, cancellationToken);

            string soundOutputDirectory = addOn.SoundDirectory;
            Directory.CreateDirectory(soundOutputDirectory);

            SoundFileManifest manifest =
                await SoundFileManifest.LoadAsync(addOn.SoundFilesJsonPath, cancellationToken);
            SoundFile[] soundFilesToCreate =
                manifest.FilesToCreate(addOn.SoundFiles, soundOutputDirectory).ToArray();

            Task[] createSoundFileTasks = soundFilesToCreate.Select(
                soundFile =>
                    SoundFileService.CreateSoundFileAsync(soundFile, soundOutputDirectory, ttsSettings,
                        cancellationToken)).ToArray();

            await Task.WhenAll(createSoundFileTasks);
            await manifest.SaveAsync(addOn.SoundFilesJsonPath, addOn.SoundFiles, cancellationToken);

            Logger.LogInformation("Finished building addon: {AddOnName}", addOn.Title);
        }

        Logger.LogInformation("Finished building add-ons");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet test tests/WoWVoxPack.UnitTests/WoWVoxPack.UnitTests.csproj -m:1 /nr:false
```
Expected: PASS, `AddOnBuildOrchestratorTests`: 4 passed, plus all 15 pre-existing unit tests still passing (19 total).

- [ ] **Step 5: Run the full solution build to confirm nothing else broke**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet build -m:1 /nr:false
```
Expected: `Build succeeded`, 0 errors — this task is purely additive, so the whole solution (including `WoWVoxPack.Builder`) should already be green.

- [ ] **Step 6: Commit**

```bash
git add src/WoWVoxPack.Core/Builder/AddOnBuildOrchestrator.cs tests/WoWVoxPack.UnitTests/AddOnBuildOrchestratorTests.cs
git commit -m "feat: extract AddOnBuildOrchestrator from Worker's build loop"
```

---

### Task 2: Rewire `Worker` and `Program.cs`

**Files:**
- Modify: `src/WoWVoxPack.Builder/Worker.cs`
- Modify: `src/WoWVoxPack.Builder/Program.cs`

`Worker` shrinks to a thin `IHostedService` wrapper around `AddOnBuildOrchestrator`. `Program.cs` moves the `SolutionFileAttribute` reflection lookup (previously inside `Worker`'s constructor) into a local function, and registers `AddOnBuildOrchestrator` via an explicit factory since DI can't auto-inject the resulting `string`.

- [ ] **Step 1: Rewrite `Worker.cs`**

```csharp
using Microsoft.Extensions.Hosting;

namespace WoWVoxPack.Builder;

public class Worker(AddOnBuildOrchestrator orchestrator, IHostApplicationLifetime applicationLifetime)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await orchestrator.RunAsync(cancellationToken);
        applicationLifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Rewrite `Program.cs`**

```csharp
using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WoWVoxPack;
using WoWVoxPack.AddOns;
using WoWVoxPack.AddOns.BigWigs_Countdown;
using WoWVoxPack.AddOns.BigWigs_Voice;
using WoWVoxPack.AddOns.ExBoss;
using WoWVoxPack.AddOns.SharedMedia_Abilities;
using WoWVoxPack.Builder;
using WoWVoxPack.TTS;

static string ResolveOutputDirectoryBase()
{
    string solutionFile =
        Assembly.GetExecutingAssembly().GetCustomAttribute<SolutionFileAttribute>()?.SolutionFile ??
        throw new Exception("Solution file not found.");
    return Path.Combine(
        Path.GetDirectoryName(solutionFile) ?? throw new Exception("Solution file not found."),
        "output");
}

IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args)
    .UseConsoleLifetime()
    .ConfigureServices((_, services) =>
    {
        services.AddTextToSpeechClient();
        services.AddSingleton<GoogleTtsClient>();
        services.AddSingleton<ITtsProvider, GoogleTtsProvider>();
        services.AddSingleton<ISoundFileService, SoundFileService>();
        services.AddHttpClient<IBigWigsVoiceUpstreamClient, BigWigsVoiceUpstreamClient>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAddOnService, BigWigsVoiceAddOnService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAddOnService, BigWigsCountdownAddOnService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAddOnService, SharedMediaAbilitiesAddOnService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAddOnService, ExBossAddOnService>());
        services.AddOptionsWithValidateOnStart<BuildMatrix>().BindConfiguration("Matrix");
        services.AddOptionsWithValidateOnStart<AddOnSettings>("BigWigs_Voice").BindConfiguration("AddOn:BigWigs_Voice")
            .BindConfiguration("AddOn");
        services.AddOptionsWithValidateOnStart<AddOnSettings>("BigWigs_Countdown")
            .BindConfiguration("AddOn:BigWigs_Countdown")
            .BindConfiguration("AddOn");
        services.AddOptionsWithValidateOnStart<AddOnSettings>("SharedMedia_Abilities")
            .BindConfiguration("AddOn:SharedMedia_Abilities")
            .BindConfiguration("AddOn");
        services.AddOptionsWithValidateOnStart<AddOnSettings>("ExBoss")
            .BindConfiguration("AddOn:ExBoss")
            .BindConfiguration("AddOn");
        services.AddSingleton(sp => new AddOnBuildOrchestrator(
            sp.GetRequiredService<ILogger<AddOnBuildOrchestrator>>(),
            sp.GetRequiredService<IEnumerable<IAddOnService>>(),
            sp.GetRequiredService<IOptions<BuildMatrix>>(),
            sp.GetRequiredService<ISoundFileService>(),
            ResolveOutputDirectoryBase()));
        services.AddHostedService<Worker>();
    }).ConfigureLogging((_, logging) =>
    {
        logging.AddConsole();
        logging.AddDebug();
    })
    .ConfigureAppConfiguration((hostContext, config) =>
    {
        config.AddJsonFile(Path.Combine("appsettings.json"), false);
        config.AddJsonFile(
            $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", true);

        config.AddCommandLine(args);
    });


using IHost host = hostBuilder.Build();
await host.RunAsync();
```

- [ ] **Step 3: Build the full solution**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet build -m:1 /nr:false
```
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Run the full test suite**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet test --no-restore -m:1 /nr:false
```
Expected: `WoWVoxPack.UnitTests`: 19 total passing (15 pre-existing + 4 `AddOnBuildOrchestratorTests`). `WoWVoxPack.IntegrationTests`: 1 passing.

- [ ] **Step 5: Commit**

```bash
git add src/WoWVoxPack.Builder/Worker.cs src/WoWVoxPack.Builder/Program.cs
git commit -m "refactor: reduce Worker to a thin wrapper around AddOnBuildOrchestrator"
```

---

## Spec coverage check

- `AddOnBuildOrchestrator` in `WoWVoxPack.Core`, namespace `WoWVoxPack.Builder`, explicit `outputDirectoryBase` parameter, no `IAddOnBuildOrchestrator` interface — Task 1.
- `Worker` reduced to a thin `IHostedService` wrapper, no unit tests for `Worker` itself (per the design's explicit scoping decision) — Task 2.
- `Program.cs` DI registration changes, `ResolveOutputDirectoryBase()` helper — Task 2.
- Matrix iteration, end-to-end file writing, incremental-skip, and changed-content test cases — all four covered in Task 1's test file.
- No change to `AddOnFileWriter`, `SoundFileManifest`, `AddOn`, `AddOnBuilder` — confirmed; this plan's two tasks touch only `AddOnBuildOrchestrator.cs`, `AddOnBuildOrchestratorTests.cs`, `Worker.cs`, and `Program.cs`.
