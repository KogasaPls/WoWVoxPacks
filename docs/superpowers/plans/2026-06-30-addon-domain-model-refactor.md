# AddOn Domain Model Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split `AddOn.cs` (currently metadata + builder + file writer + JSON cache + diff logic) into a sealed pure-data `AddOn`, an `AddOnBuilder` assembly module, and standalone `AddOnFileWriter`/`SoundFileManifest` modules, per `docs/superpowers/specs/2026-06-30-addon-domain-model-design.md`.

**Architecture:** `AddOn` becomes an immutable, sealed class with no subclasses. `AddOnBuilder` (fluent, in `WoWVoxPack.Core`) replaces the four `AddOn` subclasses' constructors — each `IAddOnService.BuildAddOnAsync` now builds via `AddOnBuilder` instead of `new XyzAddOn(...)`. `AddOnFileWriter` (stateless) and `SoundFileManifest` (owns `SoundFiles.json` load/diff/save) are extracted from `AddOn`'s old instance methods. `IAddOnService<T>` is deleted since there is no longer a subclass for it to specialize over.

**Tech Stack:** .NET 10, xUnit, Ardalis.GuardClauses.

---

## Important: build state during this plan

Tasks 1–3 only touch `WoWVoxPack.Core` and `tests/WoWVoxPack.UnitTests`. Task 2 removes `AddOn`'s old constructor and builder methods, which **breaks compilation** of the four `*AddOn.cs` subclasses, their services, and `Worker.cs` — those live in other projects and are not fixed until Tasks 4–6. This is expected. Verify each of Tasks 1–3 with a project-scoped test run (commands given per task); don't run a solution-wide build until Task 6, where it's the explicit verification step.

Work on a feature branch / isolated worktree (per `superpowers:using-git-worktrees`, used automatically by the execution skill) so `main` is never left mid-refactor.

All `dotnet` commands in this plan need the sandbox workaround documented in the repo's environment notes:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet <command> -m:1 /nr:false
```

Run test commands with escalated permissions if the sandbox blocks the VSTest socket (it does, per prior verification in this repo).

---

### Task 1: `SoundFileManifest`

**Files:**
- Create: `src/WoWVoxPack.Core/TTS/SoundFileManifest.cs`
- Test: `tests/WoWVoxPack.UnitTests/SoundFileManifestTests.cs`

This extracts the `SoundFiles.json` cache load/diff/save logic from `AddOn` (currently `LoadSoundFilesJsonAsync`, `WriteSoundFilesJsonAsync`, `SoundFilesToCreate`, `NewOrModifiedSoundFiles`, `IsSameContentAsSoundFileInJson` in `src/WoWVoxPack.Core/AddOns/AddOn.cs:76-111`) into a standalone, `AddOn`-independent type. The diff semantics must match exactly: a sound file counts as "to create" if it's missing from `soundDirectory` on disk, OR if it's missing from the manifest's prior most call `Asyn` Was loaded JSON, set, **OR** if it's present in the manifest but the content differs. A sound file **not found in the manifest** is treated as "same" (not flagged as changed) — only `File.Exists` flags brand-new files; this is the existing (intentional) fallback behavior when no cache entry exists, and must be preserved exactly.

- [ ] **Step 1: Write the failing tests**

```csharp
namespace WoWVoxPack.UnitTests;

using WoWVoxPack.TTS;

public class SoundFileManifestTests : IDisposable
{
    private readonly string _tempDirectory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

    private string ManifestPath => Path.Combine(_tempDirectory, "SoundFiles.json");

    [Fact]
    public async Task LoadAsync_ReturnsEmptyManifest_WhenFileDoesNotExist()
    {
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);
        SoundFile soundFile = new("alert.ogg", text: "Alert", displayName: "Alert");

        IEnumerable<SoundFile> filesToCreate = manifest.FilesToCreate([soundFile], _tempDirectory);

        Assert.Contains(soundFile, filesToCreate);
    }

    [Fact]
    public async Task FilesToCreate_IncludesFiles_MissingFromDisk()
    {
        SoundFile soundFile = new("alert.ogg", text: "Alert", displayName: "Alert");
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, [soundFile]);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);

        IEnumerable<SoundFile> filesToCreate = manifest.FilesToCreate([soundFile], _tempDirectory);

        Assert.Contains(soundFile, filesToCreate);
    }

    [Fact]
    public async Task FilesToCreate_ExcludesFiles_PresentOnDiskWithUnchangedContent()
    {
        SoundFile soundFile = new("alert.ogg", text: "Alert", displayName: "Alert");
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, [soundFile]);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, soundFile.FileName), "fake audio");

        IEnumerable<SoundFile> filesToCreate = manifest.FilesToCreate([soundFile], _tempDirectory);

        Assert.DoesNotContain(soundFile, filesToCreate);
    }

    [Fact]
    public async Task FilesToCreate_IncludesFiles_PresentOnDiskWithChangedContent()
    {
        SoundFile original = new("alert.ogg", text: "Alert", displayName: "Alert");
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, [original]);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, original.FileName), "fake audio");

        SoundFile changed = new("alert.ogg", text: "Alert, now with new text", displayName: "Alert");
        IEnumerable<SoundFile> filesToCreate = manifest.FilesToCreate([changed], _tempDirectory);

        Assert.Contains(changed, filesToCreate);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet test tests/WoWVoxPack.UnitTests/WoWVoxPack.UnitTests.csproj -m:1 /nr:false
```
Expected: FAIL to build, `error CS0246: The type or namespace name 'SoundFileManifest' could not be found`.

- [ ] **Step 3: Implement `SoundFileManifest`**

```csharp
using System.Text.Json;

namespace WoWVoxPack.TTS;

public sealed class SoundFileManifest
{
    private readonly IReadOnlyDictionary<string, SoundFile> _soundFilesByDisplayName;

    private SoundFileManifest(IReadOnlyDictionary<string, SoundFile> soundFilesByDisplayName)
    {
        _soundFilesByDisplayName = soundFilesByDisplayName;
    }

    public static async Task<SoundFileManifest> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return new SoundFileManifest(new Dictionary<string, SoundFile>(StringComparer.OrdinalIgnoreCase));
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken);
        List<SoundFile> soundFiles =
            JsonSerializer.Deserialize<List<SoundFile>>(json, SoundFileJsonContext.Default.ListSoundFile) ??
            throw new Exception("Failed to deserialize sound files.");

        return new SoundFileManifest(
            soundFiles.ToDictionary(f => f.DisplayName, StringComparer.OrdinalIgnoreCase));
    }

    public IEnumerable<SoundFile> FilesToCreate(IEnumerable<SoundFile> currentSoundFiles, string soundDirectory)
    {
        List<SoundFile> current = currentSoundFiles.ToList();

        IEnumerable<SoundFile> missing =
            current.Where(f => !File.Exists(Path.Combine(soundDirectory, f.FileName)));
        IEnumerable<SoundFile> changed = current.Where(f => !IsSameContentAsManifestEntry(f));

        return missing.UnionBy(changed, f => f.FileName);
    }

    public Task SaveAsync(string path, IEnumerable<SoundFile> soundFiles, CancellationToken cancellationToken = default)
    {
        string json = JsonSerializer.Serialize(soundFiles.OrderBy(s => s.FileName).ToList(),
            SoundFileJsonContext.Default.ListSoundFile);
        return File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private bool IsSameContentAsManifestEntry(SoundFile soundFile)
    {
        if (!_soundFilesByDisplayName.TryGetValue(soundFile.DisplayName, out SoundFile? existing))
        {
            return true;
        }

        return SoundFileContentEqualityComparer.Default.Equals(soundFile, existing);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet test tests/WoWVoxPack.UnitTests/WoWVoxPack.UnitTests.csproj -m:1 /nr:false
```
Expected: PASS, `SoundFileManifestTests`: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add src/WoWVoxPack.Core/TTS/SoundFileManifest.cs tests/WoWVoxPack.UnitTests/SoundFileManifestTests.cs
git commit -m "feat: extract SoundFileManifest from AddOn's JSON cache logic"
```

---

### Task 2: `AddOn` pure-data rewrite + `AddOnBuilder`

**Files:**
- Modify: `src/WoWVoxPack.Core/AddOns/AddOn.cs`
- Create: `src/WoWVoxPack.Core/AddOns/AddOnBuilder.cs`
- Test: `tests/WoWVoxPack.UnitTests/AddOnBuilderTests.cs`

`AddOn` loses its constructor, protected builder methods (`AddFile`, `AddSoundFile(s)`, `AddSoundFileJson`), and all I/O/cache methods (those moved in Task 1, and `WriteAllFilesAsync`/`WriteTocFileAsync`/`WriteAddonFilesAsync` move in Task 3). It keeps metadata properties and gains an `internal` constructor only `AddOnBuilder` can call, plus `FileContents` (filename → rendered content, for `AddOnFileWriter`) alongside the existing `Files` (filenames only, for `AddOnTocFile.tt`'s load directives — unchanged shape, so the template doesn't need editing).

- [ ] **Step 1: Write the failing tests**

```csharp
namespace WoWVoxPack.UnitTests;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

public class AddOnBuilderTests
{
    private static AddOnSettings DefaultSettings => new()
    {
        Title = "Test_AddOn",
        Version = "12.0.7",
        Author = "Tester",
        Notes = "A test addon.",
        AdditionalProperties = new Dictionary<string, string> { ["X-License"] = "Apache-2.0" }
    };

    private static TtsSettings DefaultTtsSettings => new() { Voice = VoiceName.Neural2_C };

    [Fact]
    public void Build_UsesSettingsForMetadata_WhenNotOverridden()
    {
        AddOn addOn = new AddOnBuilder(DefaultSettings, DefaultTtsSettings).Build("/tmp/output");

        Assert.Equal("Test_AddOn", addOn.Title);
        Assert.Equal("Test_AddOn", addOn.DisplayTitle);
        Assert.Equal("12.0.7", addOn.Version);
        Assert.Equal("Tester", addOn.Author);
        Assert.Equal("120007", addOn.Interfaces.Single());
        Assert.Equal("A test addon.", addOn.PrimaryNote?.Text);
        Assert.Equal("Apache-2.0", addOn.AdditionalProperties["X-License"]);
    }

    [Fact]
    public void Build_PrefersExplicitTitleAndDisplayTitle_OverSettings()
    {
        AddOn addOn = new AddOnBuilder(DefaultSettings, DefaultTtsSettings)
            .WithTitle("Overridden Title")
            .WithDisplayTitle("Overridden Display")
            .Build("/tmp/output");

        Assert.Equal("Overridden Title", addOn.Title);
        Assert.Equal("Overridden Display", addOn.DisplayTitle);
    }

    [Fact]
    public void AddSoundFile_DoesNotOverwriteExisting_UnlessOverwriteIsTrue()
    {
        SoundFile original = new("alert.ogg", text: "Original", displayName: "Alert");
        SoundFile replacement = new("alert.ogg", text: "Replacement", displayName: "Alert");

        AddOn addOn = new AddOnBuilder(DefaultSettings, DefaultTtsSettings)
            .AddSoundFile(original)
            .AddSoundFile(replacement)
            .Build("/tmp/output");

        Assert.Equal("Original", Assert.Single(addOn.SoundFiles).Text);
    }

    [Fact]
    public void AddSoundFile_Overwrites_WhenOverwriteIsTrue()
    {
        SoundFile original = new("alert.ogg", text: "Original", displayName: "Alert");
        SoundFile replacement = new("alert.ogg", text: "Replacement", displayName: "Alert");

        AddOn addOn = new AddOnBuilder(DefaultSettings, DefaultTtsSettings)
            .AddSoundFile(original)
            .AddSoundFile(replacement, overwrite: true)
            .Build("/tmp/output");

        Assert.Equal("Replacement", Assert.Single(addOn.SoundFiles).Text);
    }

    [Fact]
    public void AddFile_FactoryReceivesFullyAssembledAddOn()
    {
        SoundFile soundFile = new("alert.ogg", text: "Alert", displayName: "Alert");

        AddOn addOn = new AddOnBuilder(DefaultSettings, DefaultTtsSettings)
            .AddSoundFile(soundFile)
            .AddFile("Core.lua", built => string.Join(",", built.SoundFiles.Select(f => f.DisplayName)))
            .Build("/tmp/output");

        Assert.Equal("Alert", addOn.FileContents["Core.lua"]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet test tests/WoWVoxPack.UnitTests/WoWVoxPack.UnitTests.csproj -m:1 /nr:false
```
Expected: FAIL to build, `error CS0246: The type or namespace name 'AddOnBuilder' could not be found`.

- [ ] **Step 3: Rewrite `AddOn.cs`**

```csharp
using Ardalis.GuardClauses;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns;

public sealed class AddOn
{
    private readonly string _outputDirectoryBase;
    private readonly IReadOnlyDictionary<string, SoundFile> _soundFiles;
    private readonly IReadOnlyDictionary<string, Func<AddOn, string>> _fileFactories;
    private Dictionary<string, string>? _fileContents;

    internal AddOn(
        string outputDirectoryBase,
        string title,
        string displayTitle,
        string version,
        string author,
        TtsSettings ttsSettings,
        Note? primaryNote,
        IReadOnlyCollection<Note> additionalNotes,
        IReadOnlyDictionary<string, string> additionalProperties,
        IReadOnlyDictionary<string, SoundFile> soundFiles,
        IReadOnlyDictionary<string, Func<AddOn, string>> fileFactories)
    {
        _outputDirectoryBase = outputDirectoryBase;
        Title = Guard.Against.NullOrWhiteSpace(title);
        DisplayTitle = displayTitle;
        Version = Guard.Against.NullOrWhiteSpace(version);
        Author = Guard.Against.NullOrWhiteSpace(author);
        TtsSettings = ttsSettings;
        PrimaryNote = primaryNote;
        AdditionalNotes = additionalNotes;
        AdditionalProperties = additionalProperties;
        Interfaces = [ToInterfaceNumber(Version)];
        _soundFiles = soundFiles;
        _fileFactories = fileFactories;
    }

    public string Title { get; }
    public string DisplayTitle { get; }
    public string Version { get; }
    public string Author { get; }
    public TtsSettings TtsSettings { get; }
    public Note? PrimaryNote { get; }
    public IReadOnlyCollection<Note> AdditionalNotes { get; }
    public IReadOnlyDictionary<string, string> AdditionalProperties { get; }
    public IReadOnlyCollection<string> Interfaces { get; }

    public IEnumerable<SoundFile> SoundFiles => _soundFiles.Values;

    public IEnumerable<string> Files => _fileFactories.Keys;

    public IReadOnlyDictionary<string, string> FileContents =>
        _fileContents ??= _fileFactories.ToDictionary(kvp => kvp.Key, kvp => kvp.Value(this),
            StringComparer.OrdinalIgnoreCase);

    public string AddOnDirectory => Path.Combine(_outputDirectoryBase, AddOnDirectoryName);
    public string AddOnDirectoryName => Title.Replace(' ', '_');
    public string SoundDirectory => Path.Combine(AddOnDirectory, SoundDirectoryName);
    public string SoundDirectoryName => "Sounds";
    public string TocFileName => $"{AddOnDirectoryName}.toc";
    public string SoundFilesJsonPath => Path.Combine(AddOnDirectory, "SoundFiles.json");

    public record Note(string? LanguageCode, string Text);

    /// <summary>
    /// Converts a dotted game version (e.g. "12.0.7") into the WoW toc Interface number
    /// (e.g. "120007"): the major version followed by two-digit minor and patch components.
    /// </summary>
    internal static string ToInterfaceNumber(string version)
    {
        Guard.Against.NullOrWhiteSpace(version);

        int[] parts = version
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();

        int major = parts.Length > 0 ? parts[0] : 0;
        int minor = parts.Length > 1 ? parts[1] : 0;
        int patch = parts.Length > 2 ? parts[2] : 0;

        return $"{major}{minor:D2}{patch:D2}";
    }
}
```

- [ ] **Step 4: Create `AddOnBuilder.cs`**

```csharp
using System.Text.Json;

using Ardalis.GuardClauses;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns;

public sealed class AddOnBuilder(AddOnSettings settings, TtsSettings ttsSettings)
{
    private readonly Dictionary<string, SoundFile> _soundFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<AddOn, string>> _fileFactories = new(StringComparer.OrdinalIgnoreCase);
    private string? _title;
    private string? _displayTitle;

    public AddOnBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public AddOnBuilder WithDisplayTitle(string displayTitle)
    {
        _displayTitle = displayTitle;
        return this;
    }

    public AddOnBuilder AddSoundFile(SoundFile soundFile, bool overwrite = false)
    {
        if (overwrite || !_soundFiles.ContainsKey(soundFile.DisplayName))
        {
            _soundFiles[soundFile.DisplayName] = soundFile;
        }

        return this;
    }

    public AddOnBuilder AddSoundFiles(IEnumerable<SoundFile> soundFiles, bool overwrite = false)
    {
        foreach (SoundFile soundFile in soundFiles)
        {
            AddSoundFile(soundFile, overwrite);
        }

        return this;
    }

    public AddOnBuilder AddSoundFileJson(string filePath, bool overwrite = true)
    {
        List<SoundFile> soundFiles = Guard.Against.Null(
            JsonSerializer.Deserialize<List<SoundFile>>(File.ReadAllText(filePath),
                SoundFileJsonContext.Default.ListSoundFile));

        return AddSoundFiles(soundFiles, overwrite);
    }

    public AddOnBuilder AddFile(string fileName, Func<AddOn, string> contentFactory)
    {
        _fileFactories.Add(fileName, contentFactory);
        return this;
    }

    public AddOn Build(string outputDirectoryBase)
    {
        string title = Guard.Against.NullOrWhiteSpace(_title ?? settings.Title);
        string displayTitle = _displayTitle ?? settings.DisplayTitle ?? title;
        string version = Guard.Against.NullOrWhiteSpace(settings.Version);
        string author = Guard.Against.NullOrWhiteSpace(settings.Author);
        AddOn.Note? primaryNote = settings.Notes is null ? null : new AddOn.Note(null, settings.Notes);
        AddOn.Note[] additionalNotes =
            settings.AdditionalNotes?.Select(n => new AddOn.Note(n.Key, n.Value)).ToArray() ?? [];
        Dictionary<string, string> additionalProperties = new(
            settings.AdditionalProperties ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);

        return new AddOn(
            outputDirectoryBase,
            title,
            displayTitle,
            version,
            author,
            ttsSettings,
            primaryNote,
            additionalNotes,
            additionalProperties,
            new Dictionary<string, SoundFile>(_soundFiles, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, Func<AddOn, string>>(_fileFactories, StringComparer.OrdinalIgnoreCase));
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet test tests/WoWVoxPack.UnitTests/WoWVoxPack.UnitTests.csproj -m:1 /nr:false
```
Expected: PASS, `AddOnBuilderTests`: 5 passed, plus the existing `SoundFileTests` and `SoundFileManifestTests` still passing.

- [ ] **Step 6: Commit**

```bash
git add src/WoWVoxPack.Core/AddOns/AddOn.cs src/WoWVoxPack.Core/AddOns/AddOnBuilder.cs tests/WoWVoxPack.UnitTests/AddOnBuilderTests.cs
git commit -m "feat: make AddOn a pure-data class assembled by AddOnBuilder"
```

Note: this commit leaves `WoWVoxPack.AddOns.BigWigs_Voice`, `WoWVoxPack.AddOns.BigWigs_Countdown`, `WoWVoxPack.AddOns.SharedMedia_Abilities`, `WoWVoxPack.AddOns.ExBoss`, and `WoWVoxPack.Builder` non-compiling — expected, fixed in Tasks 4–6.

---

### Task 3: `AddOnFileWriter`

**Files:**
- Create: `src/WoWVoxPack.Core/AddOns/AddOnFileWriter.cs`
- Test: `tests/WoWVoxPack.UnitTests/AddOnFileWriterTests.cs`

Extracts `WriteAllFilesAsync`/`WriteTocFileAsync`/`WriteAddonFilesAsync` (previously `AddOn.cs:148-178`) into a standalone writer operating on the now-immutable `AddOn`.

- [ ] **Step 1: Write the failing test**

```csharp
namespace WoWVoxPack.UnitTests;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

public class AddOnFileWriterTests : IDisposable
{
    private readonly string _tempDirectory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

    [Fact]
    public async Task WriteAllFilesAsync_WritesTocFileAndRegisteredFiles()
    {
        AddOnSettings settings = new()
        {
            Title = "Test_AddOn",
            Version = "12.0.7",
            Author = "Tester",
            Notes = "A test addon."
        };
        TtsSettings ttsSettings = new() { Voice = VoiceName.Neural2_C };

        AddOn addOn = new AddOnBuilder(settings, ttsSettings)
            .AddFile("Core.lua", _ => "-- generated lua")
            .Build(_tempDirectory);

        await AddOnFileWriter.WriteAllFilesAsync(addOn);

        string tocPath = Path.Combine(addOn.AddOnDirectory, addOn.TocFileName);
        string luaPath = Path.Combine(addOn.AddOnDirectory, "Core.lua");

        Assert.True(File.Exists(tocPath));
        Assert.Contains("## Title: Test_AddOn", await File.ReadAllTextAsync(tocPath));
        Assert.Equal("-- generated lua", await File.ReadAllTextAsync(luaPath));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }
}
```

- [ ] **Step 2: Run test to verify it fails to compile**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet test tests/WoWVoxPack.UnitTests/WoWVoxPack.UnitTests.csproj -m:1 /nr:false
```
Expected: FAIL to build, `error CS0246: The type or namespace name 'AddOnFileWriter' could not be found`.

- [ ] **Step 3: Implement `AddOnFileWriter`**

```csharp
namespace WoWVoxPack.AddOns;

public static class AddOnFileWriter
{
    public static async Task WriteAllFilesAsync(AddOn addOn, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(addOn.AddOnDirectory);

        await WriteTocFileAsync(addOn, cancellationToken).ConfigureAwait(false);
        await WriteAddOnFilesAsync(addOn, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteTocFileAsync(AddOn addOn, CancellationToken cancellationToken)
    {
        AddOnTocFile tocFile = new(addOn);
        string tocFilePath = Path.Combine(addOn.AddOnDirectory, addOn.TocFileName);
        await File.WriteAllTextAsync(tocFilePath, (string?)tocFile.TransformText(), cancellationToken);
    }

    private static async Task WriteAddOnFilesAsync(AddOn addOn, CancellationToken cancellationToken)
    {
        foreach ((string fileName, string content) in addOn.FileContents)
        {
            string path = Path.Combine(addOn.AddOnDirectory, fileName);
            await File.WriteAllTextAsync(path, content, cancellationToken);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet test tests/WoWVoxPack.UnitTests/WoWVoxPack.UnitTests.csproj -m:1 /nr:false
```
Expected: PASS, `AddOnFileWriterTests`: 1 passed, all prior unit tests still passing.

- [ ] **Step 5: Commit**

```bash
git add src/WoWVoxPack.Core/AddOns/AddOnFileWriter.cs tests/WoWVoxPack.UnitTests/AddOnFileWriterTests.cs
git commit -m "feat: extract AddOnFileWriter from AddOn's file-writing logic"
```

---

### Task 4: Migrate `BigWigs_Countdown` and `BigWigs_Voice`

**Files:**
- Delete: `src/WoWVoxPack.AddOns.BigWigs_Countdown/BigWigsCountdownAddOn.cs`
- Delete: `src/WoWVoxPack.AddOns.BigWigs_Voice/BigWigsVoiceAddOn.cs`
- Modify: `src/WoWVoxPack.AddOns.BigWigs_Countdown/BigWigsCountdownAddOnService.cs`
- Modify: `src/WoWVoxPack.AddOns.BigWigs_Voice/BigWigsVoiceAddOnService.cs`

These two addons don't need any template constructor retargeting (`CountdownLuaFile`/`CoreLuaFile` already take base `AddOn`), so they're the simplest migrations.

- [ ] **Step 1: Delete the subclasses**

```bash
git rm src/WoWVoxPack.AddOns.BigWigs_Countdown/BigWigsCountdownAddOn.cs src/WoWVoxPack.AddOns.BigWigs_Voice/BigWigsVoiceAddOn.cs
```

- [ ] **Step 2: Rewrite `BigWigsCountdownAddOnService.cs`**

```csharp
using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Countdown;

public sealed class BigWigsCountdownAddOnService(IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService
{
    private static readonly Lazy<List<SoundFile>> CountdownSoundFiles = new(GetCountdownSoundFiles);

    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("BigWigs_Countdown");

    public Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        AddOn addOn = new AddOnBuilder(AddOnSettings, ttsSettings)
            .WithTitle($"BigWigs Countdown WoWVoxPacks {ttsSettings.Voice}")
            .WithDisplayTitle($"BigWigs |cffff7f3f+|r|cffffffffCountdown: WoWVoxPacks ({ttsSettings.Voice})|r")
            .AddSoundFiles(CountdownSoundFiles.Value)
            .AddFile("Countdown.lua", addon => new CountdownLuaFile(addon).TransformText())
            .Build(outputDirectoryBase);

        return Task.FromResult(addOn);
    }

    private static List<SoundFile> GetCountdownSoundFiles()
    {
        return
        [
            new SoundFile("countdown_1.ogg", "1"),
            new SoundFile("countdown_2.ogg", "2"),
            new SoundFile("countdown_3.ogg", "3"),
            new SoundFile("countdown_4.ogg", "4"),
            new SoundFile("countdown_5.ogg", "5"),
            new SoundFile("countdown_6.ogg", "6"),
            new SoundFile("countdown_7.ogg", "7"),
            new SoundFile("countdown_8.ogg", "8"),
            new SoundFile("countdown_9.ogg", "9"),
            new SoundFile("countdown_10.ogg", "10")
        ];
    }
}
```

- [ ] **Step 3: Rewrite `BigWigsVoiceAddOnService.cs`**

```csharp
using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public sealed class BigWigsVoiceAddOnService(
    IOptionsSnapshot<AddOnSettings> addOnOptions,
    IBigWigsVoiceUpstreamClient upstreamClient)
    : IAddOnService
{
    private BigWigsVoiceSoundFile[]? _soundFiles;

    private IBigWigsVoiceUpstreamClient UpstreamClient { get; } = upstreamClient;
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("BigWigs_Voice");

    public async Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<BigWigsVoiceSoundFile> soundFiles = await GetSoundFilesAsync(cancellationToken);
        string soundFileJsonPath = Path.Combine(AppContext.BaseDirectory, "BigWigsVoice_Sounds.json");

        return new AddOnBuilder(AddOnSettings, ttsSettings)
            .WithDisplayTitle($"BigWigs |cffff7f3f+|r|cffffffffVoice: WoWVoxPacks ({ttsSettings.Voice})|r")
            .AddFile("Core.lua", addon => new CoreLuaFile(addon).TransformText())
            .AddSoundFileJson(soundFileJsonPath)
            .AddSoundFiles(soundFiles)
            .Build(outputDirectoryBase);
    }

    private async ValueTask<IEnumerable<BigWigsVoiceSoundFile>>
        GetSoundFilesAsync(CancellationToken cancellationToken)
    {
        return _soundFiles ??= (await UpstreamClient.GetSoundFilesAsync(cancellationToken)).ToArray();
    }
}
```

- [ ] **Step 4: Verify both addon projects build on their own**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet build src/WoWVoxPack.AddOns.BigWigs_Countdown/WoWVoxPack.AddOns.BigWigs_Countdown.csproj -m:1 /nr:false
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet build src/WoWVoxPack.AddOns.BigWigs_Voice/WoWVoxPack.AddOns.BigWigs_Voice.csproj -m:1 /nr:false
```
Expected: both `Build succeeded`. (`WoWVoxPack.Builder` is still broken — expected until Task 6.)

- [ ] **Step 5: Commit**

```bash
git add -A src/WoWVoxPack.AddOns.BigWigs_Countdown src/WoWVoxPack.AddOns.BigWigs_Voice
git commit -m "refactor: build BigWigs_Countdown and BigWigs_Voice addons via AddOnBuilder"
```

---

### Task 5: Migrate `SharedMedia_Abilities` and `ExBoss`, retarget their templates

**Files:**
- Delete: `src/WoWVoxPack.AddOns.SharedMedia_Abilities/SharedMediaAbilitiesAddOn.cs`
- Delete: `src/WoWVoxPack.AddOns.ExBoss/ExBossAddOn.cs`
- Modify: `src/WoWVoxPack.AddOns.SharedMedia_Abilities/SharedMediaAbilitiesAddOnService.cs`
- Modify: `src/WoWVoxPack.AddOns.ExBoss/SharedMediaAbilitiesAddOnService.cs` (this file holds the `ExBossAddOnService` class — pre-existing filename, not changed here)
- Modify: `src/WoWVoxPack.AddOns.SharedMedia_Abilities/AbilitiesLuaFile.partial.cs`
- Modify: `src/WoWVoxPack.AddOns.ExBoss/LabelsFile.partial.cs`

`AbilitiesLuaFile` and `LabelsFile` currently take their addon's concrete subclass in their constructor but only ever read `AddOnDirectoryName`, `SoundFiles`, and `TtsSettings` — all base `AddOn` members — so retargeting to `AddOn` is behavior-preserving.

- [ ] **Step 1: Delete the subclasses**

```bash
git rm src/WoWVoxPack.AddOns.SharedMedia_Abilities/SharedMediaAbilitiesAddOn.cs src/WoWVoxPack.AddOns.ExBoss/ExBossAddOn.cs
```

- [ ] **Step 2: Retarget `AbilitiesLuaFile.partial.cs`**

```csharp
using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Abilities;

public partial class AbilitiesLuaFile
{
    public AbilitiesLuaFile(AddOn addOn)
    {
        AddOn = addOn;
    }

    public AddOn AddOn { get; }

    public string AddOnDirectoryName => AddOn.AddOnDirectoryName;

    public IEnumerable<SoundFile> SoundFiles => AddOn.SoundFiles;

    public string GetLsmKey(SoundFile sound) => $"WoWVoxPacks {AddOn.TtsSettings.Voice}: {sound.DisplayName}";
}
```

- [ ] **Step 3: Retarget `LabelsFile.partial.cs`**

```csharp
using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.ExBoss;

public partial class LabelsFile
{
    public LabelsFile(AddOn addOn)
    {
        AddOn = addOn;
    }

    private AddOn AddOn { get; }

    public IEnumerable<SoundFile> SoundFiles => AddOn.SoundFiles;

    public string GetLsmKey(SoundFile sound)
    {
        return $"[ExBoss WoWVoxPacks {AddOn.TtsSettings.Voice}]{sound.DisplayName}";
    }

    public string AddOnDirectoryName => AddOn.AddOnDirectoryName;
}
```

- [ ] **Step 4: Rewrite `SharedMediaAbilitiesAddOnService.cs`**

```csharp
using System.Text.Json;

using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Abilities;

public sealed class SharedMediaAbilitiesAddOnService(IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService
{
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("SharedMedia_Abilities");

    public Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        string file = Path.Combine(AppContext.BaseDirectory, "SharedMedia_Abilities_Sounds.json");

        AddOn addOn = new AddOnBuilder(AddOnSettings, ttsSettings)
            .WithTitle($"SharedMedia Abilities WoWVoxPacks {ttsSettings.Voice}")
            .WithDisplayTitle($"SharedMedia |cffff7f3f+|r|cffffffffAbilities: WoWVoxPacks ({ttsSettings.Voice})|r")
            .AddSoundFiles(LoadSoundsFromJson(file), overwrite: true)
            .AddFile("Core.lua", addon => new AbilitiesLuaFile(addon).TransformText())
            .Build(outputDirectoryBase);

        return Task.FromResult(addOn);
    }

    private static IEnumerable<SoundFile> LoadSoundsFromJson(string filePath)
    {
        List<SoundFile> soundFiles =
            JsonSerializer.Deserialize<List<SoundFile>>(File.ReadAllText(filePath),
                SoundFileJsonContext.Default.ListSoundFile) ?? [];

        return soundFiles.Select(sf =>
            sf.Ssml is null && sf.Text?.Contains('=') == true
                ? new SoundFile(sf.FileName, ssml: SoundFile.GetSsml(sf.Text), displayName: sf.DisplayName,
                    formattedDisplayName: sf.FormattedDisplayName)
                : sf);
    }
}
```

- [ ] **Step 5: Rewrite `src/WoWVoxPack.AddOns.ExBoss/SharedMediaAbilitiesAddOnService.cs` (the `ExBossAddOnService` class)**

```csharp
using System.Text.Json;

using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.ExBoss;

public sealed class ExBossAddOnService(IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService
{
    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("ExBoss");

    public Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        string file = Path.Combine(AppContext.BaseDirectory, "Labels.json");

        AddOn addOn = new AddOnBuilder(AddOnSettings, ttsSettings)
            .WithTitle($"ExBoss WoWVoxPacks {ttsSettings.Voice}")
            .WithDisplayTitle($"ExBoss WoWVoxPacks ({ttsSettings.Voice})")
            .AddSoundFiles(LoadSoundsFromJson(file), overwrite: true)
            .AddFile("Core.lua", addon => new LabelsFile(addon).TransformText())
            .Build(outputDirectoryBase);

        return Task.FromResult(addOn);
    }

    private static IEnumerable<SoundFile> LoadSoundsFromJson(string filePath)
    {
        List<SoundFile> soundFiles =
            JsonSerializer.Deserialize<List<SoundFile>>(File.ReadAllText(filePath),
                SoundFileJsonContext.Default.ListSoundFile) ?? [];

        return soundFiles.Select(sf =>
            sf.Ssml is null && sf.Text?.Contains('=') == true
                ? new SoundFile(sf.FileName, ssml: SoundFile.GetSsml(sf.Text), displayName: sf.DisplayName,
                    formattedDisplayName: sf.FormattedDisplayName)
                : sf);
    }
}
```

- [ ] **Step 6: Verify both addon projects build on their own**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet build src/WoWVoxPack.AddOns.SharedMedia_Abilities/WoWVoxPack.AddOns.SharedMedia_Abilities.csproj -m:1 /nr:false
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet build src/WoWVoxPack.AddOns.ExBoss/WoWVoxPack.AddOns.ExBoss.csproj -m:1 /nr:false
```
Expected: both `Build succeeded`. (`WoWVoxPack.Builder` is still broken — expected until Task 6.)

- [ ] **Step 7: Commit**

```bash
git add -A src/WoWVoxPack.AddOns.SharedMedia_Abilities src/WoWVoxPack.AddOns.ExBoss
git commit -m "refactor: build SharedMedia_Abilities and ExBoss addons via AddOnBuilder"
```

---

### Task 6: `Worker`, `IAddOnService`, and full solution verification

**Files:**
- Modify: `src/WoWVoxPack.Core/AddOns/IAddOnService.cs`
- Modify: `src/WoWVoxPack.Builder/Worker.cs`

- [ ] **Step 1: Delete `IAddOnService<T>` from `IAddOnService.cs`**

```csharp
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns;

public interface IAddOnService
{
    Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Update `Worker.StartAsync` to use `AddOnFileWriter` and `SoundFileManifest`**

Replace the body of `StartAsync` in `src/WoWVoxPack.Builder/Worker.cs`:

```csharp
    public async Task StartAsync(CancellationToken cancellationToken)
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

        Logger.LogInformation("Finished building add-ons, stopping");
        ApplicationLifetime.StopApplication();
    }
```

The rest of `Worker.cs` (constructor, properties, `Matrix`, `StopAsync`) is unchanged.

- [ ] **Step 3: Build the full solution**

Run:
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet build -m:1 /nr:false
```
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Run the full test suite**

Run (per this repo's documented sandbox workaround — needs escalated permissions for the VSTest socket):
```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBuildEnableWorkloadResolver=false dotnet test --no-restore -m:1 /nr:false
```
Expected: `WoWVoxPack.UnitTests`: all tests passed (3 pre-existing `SoundFileTests` + 4 `SoundFileManifestTests` + 5 `AddOnBuilderTests` + 1 `AddOnFileWriterTests` = 13 passed). `WoWVoxPack.IntegrationTests`: Passed 1, Failed 0 (unchanged, lives behind credentials).

- [ ] **Step 5: Commit**

```bash
git add src/WoWVoxPack.Core/AddOns/IAddOnService.cs src/WoWVoxPack.Builder/Worker.cs
git commit -m "refactor: wire Worker to AddOnFileWriter and SoundFileManifest, drop IAddOnService<T>"
```

---

## Spec coverage check

- `AddOn` pure-data, no subclasses, no virtual `AddOnDirectoryName`/`SoundDirectoryName` — Task 2.
- `AddOnBuilder` fluent assembly, resolves the file-factory self-reference — Task 2.
- Four `IAddOnService.BuildAddOnAsync` implementations build via `AddOnBuilder`; `IAddOnService<T>` deleted — Tasks 4, 5, 6.
- `AddOnFileWriter` extracted — Task 3.
- `SoundFileManifest` extracted, diff semantics preserved exactly (including the "not in manifest → treated as same" fallback) — Task 1.
- `Worker` orchestrates the separated pieces — Task 6.
- Per-addon Lua generation gets test coverage via `AddOnBuilderTests.AddFile_FactoryReceivesFullyAssembledAddOn` (exercises the same `Func<AddOn,string>` factory mechanism every addon service uses) and `AddOnFileWriterTests` (exercises actual `.toc`/file writing end to end).
- `SoundFiles.json` diff logic gets direct unit coverage — `SoundFileManifestTests`, previously impossible without a real `AddOn` subclass.
