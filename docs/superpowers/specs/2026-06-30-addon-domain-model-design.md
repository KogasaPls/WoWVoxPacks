# AddOn Domain Model Refactor — Design

Date: 2026-06-30

## Problem

`src/WoWVoxPack.Core/AddOns/AddOn.cs` currently mixes four responsibilities in
one class: addon metadata (title, version, notes, properties), a builder
surface used by subclass constructors (`AddFile`, `AddSoundFile(s)`,
`AddSoundFileJson`), filesystem writing (`WriteAllFilesAsync` and friends),
and `SoundFiles.json` cache load/diff/save. The four concrete addons
(`BigWigsVoiceAddOn`, `BigWigsCountdownAddOn`, `SharedMediaAbilitiesAddOn`,
`ExBossAddOn`) each subclass `AddOn` and build themselves eagerly in their
constructors.

This makes the cache-diffing logic (`SoundFilesToCreate`,
`IsSameContentAsSoundFileInJson`) — the most bug-prone part of the file —
impossible to unit test without constructing a real subclass and touching
disk. It also means `IAddOnService<T>` exists solely to let each service
return its own `AddOn` subclass via a default-interface-method bridge.

Goal: small, testable seams for addon assembly, file writing, and
sound-file caching, without changing build output (`.toc`/`.lua`/`.ogg`
files and `SoundFiles.json` content stay byte-for-byte equivalent for a
given input).

## Design

### 1. `AddOn` becomes a sealed, pure-data class

No constructor logic beyond field assignment, no `protected` builder
methods, no virtual members. `AddOnDirectoryName` and `SoundDirectoryName`
become plain (non-virtual) computed properties — no subclass currently
overrides them.

Holds: `Title`, `DisplayTitle`, `Version`, `Author`, `PrimaryNote`,
`AdditionalNotes`, `AdditionalProperties`, `Interfaces`, `SoundFiles`,
`Files` (rendered `.toc`/`.lua` content keyed by filename), and the
directory-name properties derived from `Title`.

All T4 templates (`AddOnTocFile`, `CoreLuaFile`, `CountdownLuaFile`,
`AbilitiesLuaFile`, `LabelsFile`) take this one type. `AbilitiesLuaFile`
and `LabelsFile` currently take their addon's concrete subclass
(`SharedMediaAbilitiesAddOn`, `ExBossAddOn`) but only ever read
`AddOnDirectoryName`, `SoundFiles`, and `TtsSettings` — all base-class
members — so retargeting their `.partial.cs` constructors to `AddOn` is a
behavior-preserving signature change.

### 2. `AddOnBuilder` replaces subclassing as the addon-assembly module

Fluent surface: `WithTitle`, `WithDisplayTitle`, `WithNotes`,
`AddSoundFile(s)`, `AddSoundFileJson(path)`, `AddFile(name,
Func<AddOn,string>)`, then `Build(outputDirectoryBase)`.

`Build()` resolves the self-reference that file-content factories have
(a Lua template needs the fully-assembled `AddOn` to render) by
constructing the metadata-and-sound-files core first, then evaluating each
registered file factory against that core, then returning the complete
immutable `AddOn`. This replaces the current `Lazy<string>?` mechanism in
`AddOn._addOnFiles` with explicit, eager evaluation at the end of `Build()`
— the ordering guarantee is identical (file factories observe a fully
populated addon) but no longer relies on deferred evaluation.

### 3. Each `IAddOnService.BuildAddOnAsync` builds via `AddOnBuilder`

`BigWigsVoiceAddOn.cs`, `BigWigsCountdownAddOn.cs`,
`SharedMediaAbilitiesAddOn.cs`, and `ExBossAddOn.cs` are deleted. Their
constructor bodies move into the corresponding `*AddOnService
.BuildAddOnAsync` method as `AddOnBuilder` calls.

`IAddOnService<T>` is deleted. Every service implements plain
`IAddOnService` and returns `Task<AddOn>` directly — the generic bridge
existed only to let each service return its own `AddOn` subclass, which no
longer exists.

### 4. `AddOnFileWriter` and `SoundFileManifest` extracted as standalone modules

`AddOn` no longer owns I/O, so file writing and sound-file caching move to
their own modules:

- **`AddOnFileWriter`** — stateless, `WriteAllFilesAsync(AddOn addOn,
  CancellationToken ct)`. Writes the `.toc` (via `AddOnTocFile`) and the
  addon's registered `Files` to `addOn.AddOnDirectory`. Pure function of an
  `AddOn` plus the filesystem; testable against a temp directory.
- **`SoundFileManifest`** — owns the `SoundFiles.json` cache.
  `LoadAsync(path, ct)` reads (or defaults to empty if absent);
  `FilesToCreate(IEnumerable<SoundFile> current, string soundDirectory)`
  is the diff logic moved verbatim from `AddOn.SoundFilesToCreate` /
  `IsSameContentAsSoundFileInJson` (file-exists check unioned with
  content-hash mismatch against the loaded manifest); `SaveAsync(path,
  IEnumerable<SoundFile>, ct)` persists. No dependency on `AddOn` — takes
  plain `SoundFile` collections and paths, so it is testable with fake
  data and temp directories without constructing any addon.

### 5. `Worker.StartAsync` orchestrates the separated pieces

```csharp
AddOn addOn = await addOnService.BuildAddOnAsync(outputDirectory, ttsSettings, ct);
await AddOnFileWriter.WriteAllFilesAsync(addOn, ct);
SoundFileManifest manifest = await SoundFileManifest.LoadAsync(addOn.SoundFilesJsonPath, ct);
IEnumerable<SoundFile> toCreate = manifest.FilesToCreate(addOn.SoundFiles, addOn.SoundDirectory);
await Task.WhenAll(toCreate.Select(f =>
    SoundFileService.CreateSoundFileAsync(f, addOn.SoundDirectory, ttsSettings, ct)));
await manifest.SaveAsync(addOn.SoundFilesJsonPath, addOn.SoundFiles, ct);
```

`Worker`'s own shape (the `IHostedService` loop, matrix iteration) is
unchanged — restructuring `Worker` itself is a separate, later design
decision. This step only changes what `Worker` calls.

## Testing

- `AddOnBuilder`: exercise directly with fake `AddOnSettings`/`TtsSettings`
  and in-memory sound files, no filesystem; assert the resulting `AddOn`'s
  metadata and rendered `Files` content. Covers per-addon Lua generation
  (BigWigs Core.lua, Countdown.lua, SharedMedia/ExBoss LSM registration)
  that has no current test coverage.
- `SoundFileManifest`: unit tests for the diff logic against temp-dir and
  JSON fixtures — new-file, missing-file, changed-content, and
  unchanged-content cases. This is the logic the handoff flagged as
  highest-risk and currently untestable.
- `AddOnFileWriter`: thin test writing to a temp directory, asserting
  `.toc` and registered file contents land on disk.

## Non-goals

- `Worker`'s `IHostedService` structure (hotspot #2 from the broader
  review) — separate design decision.
- `SoundFileService`'s copy/TTS-call/file-write/ffmpeg-convert split
  (hotspot #3) — separate design decision.
- `Program.cs` registration deduplication (hotspot #5) — separate design
  decision.
- No change to on-disk output format: `.toc`/`.lua` content and
  `SoundFiles.json` schema must remain byte-for-byte identical for the
  same inputs, since `output/` is a tracked, diffed directory and CI opens
  PRs against it.

## Blast radius

**Deleted:** `BigWigsVoiceAddOn.cs`, `BigWigsCountdownAddOn.cs`,
`SharedMediaAbilitiesAddOn.cs`, `ExBossAddOn.cs`, `IAddOnService<T>`.

**Added:** `AddOnBuilder.cs`, `AddOnFileWriter.cs`, `SoundFileManifest.cs`,
plus unit tests for each.

**Rewritten:** `AddOn.cs`.

**Edited:** `BigWigsVoiceAddOnService.cs`, `BigWigsCountdownAddOnService.cs`,
`SharedMediaAbilitiesAddOnService.cs`, `ExBossAddOnService.cs` (build via
`AddOnBuilder` instead of `new XyzAddOn(...)`), `AbilitiesLuaFile
.partial.cs`, `LabelsFile.partial.cs` (constructor retargeted from concrete
subclass to `AddOn`), `Worker.cs` (orchestration block), `IAddOnService.cs`
(drop the generic interface).

No `appsettings.json` or `.tt` template *content* changes required.
