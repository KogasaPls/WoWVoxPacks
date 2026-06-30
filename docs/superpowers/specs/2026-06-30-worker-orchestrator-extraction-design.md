# Worker Orchestrator Extraction — Design

Date: 2026-06-30

## Problem

`src/WoWVoxPack.Builder/Worker.cs` is an `IHostedService` whose constructor
resolves `OutputDirectoryBase` via assembly reflection
(`Assembly.GetExecutingAssembly().GetCustomAttribute<SolutionFileAttribute>()`),
always pointing at the real repository's tracked `output/` directory. Its
`StartAsync` method contains the entire addon-build loop: iterating the
addon-service × `TtsSettings` matrix, building each `AddOn`, writing files via
`AddOnFileWriter`, diffing/synthesizing sound files via `SoundFileManifest`
and `ISoundFileService`, and finally stopping the host.

This was flagged as a hotspot in the original architecture review
(`docs/superpowers/specs/2026-06-30-addon-domain-model-design.md`'s
predecessor review) and explicitly deferred as "a separate design decision"
in that spec's non-goals. The trigger for picking it up now: the AddOn
domain-model refactor (just completed) made every other piece of this loop
independently testable — `IAddOnService` and `ISoundFileService` are
interfaces, `AddOnFileWriter`/`SoundFileManifest` operate on plain data and
temp directories — except `Worker` itself, because its constructor hardcodes
a real, non-injectable output path and bundles orchestration logic together
with `IHostedService` lifecycle concerns.

Goal: extract the orchestration loop into a class that can be constructed
and run directly in a unit test, with no `IHostedService` machinery and no
writes to the real `output/` directory, while leaving `Worker` as a thin
hosting wrapper.

## Design

### 1. `AddOnBuildOrchestrator` — new class, lives in `WoWVoxPack.Core`

Project placement: `WoWVoxPack.Core`, not `WoWVoxPack.Builder`. This matches
the existing split (Core holds business logic; `WoWVoxPack.Builder` is the
composition root that wires DI and runs the host) and means
`tests/WoWVoxPack.UnitTests` — which already references `WoWVoxPack.Core` —
can test it without adding a new project reference. Namespace:
`WoWVoxPack.Builder`, matching where `Worker` already lives; this codebase
already places namespaces independently of project boundaries (e.g.
`AddOnTocFile` lives in the Core project under the `WoWVoxPack.AddOns`
namespace).

Constructor: `ILogger<AddOnBuildOrchestrator>`, `IEnumerable<IAddOnService>`,
`IOptions<BuildMatrix>`, `ISoundFileService`, and `string
outputDirectoryBase` as an explicit parameter — the key testability change.
No `IHostApplicationLifetime` dependency; stopping the host is not this
class's concern.

Single method: `Task RunAsync(CancellationToken cancellationToken)`,
containing the current loop body from `Worker.StartAsync` verbatim (build →
log → `AddOnFileWriter.WriteAllFilesAsync` → create sound directory → load
`SoundFileManifest` → diff → synthesize missing/changed sounds in parallel →
save manifest → log), minus the final `ApplicationLifetime.StopApplication()`
call. The existing "Finished building add-ons, stopping" log message is
trimmed to "Finished building add-ons" since stopping is no longer this
class's responsibility.

No `IAddOnBuildOrchestrator` interface. There is exactly one
implementation and no plan for a second; per "one adapter is a hypothetical
seam, two adapters is a real one," an interface here would be an unneeded
abstraction. `Worker` depends on the concrete class directly — DI containers
inject concrete classes natively.

### 2. `Worker` becomes a thin `IHostedService` wrapper

```csharp
public class Worker(ILogger<Worker> logger, AddOnBuildOrchestrator orchestrator,
    IHostApplicationLifetime applicationLifetime) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await orchestrator.RunAsync(cancellationToken);
        applicationLifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

`Worker` loses its constructor's reflection-based path resolution, the
`AddOnServices`/`BuildMatrix`/`SoundFileService`/`Matrix` members, and the
entire loop body — all of that now lives in `AddOnBuildOrchestrator`.
`Worker` itself is not unit tested: at three lines of glue code with no
branching, the marginal value of isolating it from a real orchestrator is
low, and the meaningful logic is fully covered by
`AddOnBuildOrchestratorTests`.

### 3. `Program.cs` — DI registration changes

The `SolutionFileAttribute` reflection lookup moves out of `Worker`'s
constructor into a small helper invoked once during service registration in
`Program.cs`. Because `AddOnBuildOrchestrator` takes a plain `string`
parameter that DI cannot auto-resolve, its registration becomes an explicit
factory:

```csharp
services.AddSingleton(sp => new AddOnBuildOrchestrator(
    sp.GetRequiredService<ILogger<AddOnBuildOrchestrator>>(),
    sp.GetRequiredService<IEnumerable<IAddOnService>>(),
    sp.GetRequiredService<IOptions<BuildMatrix>>(),
    sp.GetRequiredService<ISoundFileService>(),
    ResolveOutputDirectoryBase()));
services.AddHostedService<Worker>();
```

where `ResolveOutputDirectoryBase()` is the same logic `Worker`'s
constructor used to run (resolve `SolutionFileAttribute` via
`Assembly.GetExecutingAssembly()`, combine with `"output"`), moved to
`Program.cs` as a local function or small static helper. `GetExecutingAssembly()`
still resolves correctly to `WoWVoxPack.Builder.dll` when called from
`Program.cs`'s top-level statements, since that's the assembly the code
compiles into.

## Testing

`AddOnBuildOrchestratorTests` (new), in `tests/WoWVoxPack.UnitTests`:

- **Matrix iteration**: 2 fake `IAddOnService` × 2 `TtsSettings` entries in
  the `BuildMatrix` → assert `BuildAddOnAsync` is called 4 times total
  (covers the cartesian-product LINQ query, which had no prior coverage).
- **End-to-end file writing**: one fake `IAddOnService` returning a real
  `AddOn` (built via `AddOnBuilder`, with one `AddFile` registration and one
  `SoundFile`) and a real temp directory as `outputDirectoryBase`. After
  `RunAsync`, assert the `.toc` file and the registered file actually exist
  on disk with correct content, and the fake `ISoundFileService
  .CreateSoundFileAsync` was invoked once with the expected `SoundFile`,
  output directory, and `TtsSettings`.
- **Incremental skip**: run `RunAsync` twice against the same temp
  directory with the same addon. The fake `ISoundFileService` must actually
  write a dummy file to the target path when "synthesizing," simulating
  real TTS output — otherwise `SoundFileManifest`'s disk-existence check
  would treat every run as "file missing" and the skip behavior would never
  be observable. Assert the sound service is called once on the first run
  and not called again on the second, since `SoundFiles.json` now records
  the file as unchanged. This is the highest-value new test: it validates
  the integration between `SoundFileManifest`'s diffing and `Worker`'s
  (now the orchestrator's) orchestration, which no existing test exercises
  end-to-end.
- **Changed content**: same two-run setup, but the second run's `AddOn`
  registers a `SoundFile` with the same `FileName`/`DisplayName` but
  different `Text`. Assert the sound service is called again for that file
  on the second run.

`IAddOnService` and `ISoundFileService` fakes are hand-written test doubles
(simple classes implementing the interface, recording calls in a list).
`IOptions<BuildMatrix>` uses `Options.Create(...)`. `ILogger<T>` uses
`NullLogger<T>.Instance` from `Microsoft.Extensions.Logging.Abstractions`
(already a transitive dependency via `Microsoft.Extensions.Logging`, which
`WoWVoxPack.Core` already references).

## Non-goals

- No change to `AddOnFileWriter`, `SoundFileManifest`, `AddOn`, or
  `AddOnBuilder` — all untouched by this extraction.
- No change to `Program.cs`'s addon/service registration blocks beyond the
  `AddOnBuildOrchestrator`/`Worker` wiring described above.
- No interface introduced for `AddOnBuildOrchestrator` (see "no
  `IAddOnBuildOrchestrator` interface" above).
- No behavior change to the build output: `.toc`/`.lua` content and
  `SoundFiles.json` schema remain identical for the same inputs — this is a
  structural extraction, not a logic change.

## Blast radius

**Added**: `AddOnBuildOrchestrator.cs` (in `WoWVoxPack.Core`),
`AddOnBuildOrchestratorTests.cs`.

**Rewritten**: `Worker.cs` (shrinks to a thin wrapper).

**Edited**: `Program.cs` (DI registration for `AddOnBuildOrchestrator` and
the `ResolveOutputDirectoryBase()` helper).

No `appsettings.json` changes required.
