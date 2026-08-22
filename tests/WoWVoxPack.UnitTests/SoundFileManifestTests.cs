using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

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
    public async Task FilesToCreate_RefusesToRerenderMostOfAPack_UnlessAsked()
    {
        SoundFile[] rendered = Recordings(1000, "take one").ToArray();
        await (await SoundFileManifest.LoadAsync(ManifestPath)).SaveAsync(ManifestPath, rendered);
        foreach (SoundFile soundFile in rendered)
        {
            await File.WriteAllTextAsync(Path.Combine(_tempDirectory, soundFile.FileName), "audio");
        }

        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);
        SoundFile[] everythingChanged = Recordings(1000, "take two").ToArray();

        InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(
            () => manifest.FilesToCreate(everythingChanged, _tempDirectory).ToArray());
        Assert.Contains("--Matrix:AllowFullRerender", refusal.Message);

        Assert.Equal(1000,
            manifest.FilesToCreate(everythingChanged, _tempDirectory, allowFullRerender: true).Count());
    }

    [Fact]
    public async Task FilesToCreate_AllowsAnOrdinaryRoundOfEdits()
    {
        SoundFile[] rendered = Recordings(1000, "take one").ToArray();
        await (await SoundFileManifest.LoadAsync(ManifestPath)).SaveAsync(ManifestPath, rendered);
        foreach (SoundFile soundFile in rendered)
        {
            await File.WriteAllTextAsync(Path.Combine(_tempDirectory, soundFile.FileName), "audio");
        }

        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);
        SoundFile[] current = rendered
            .Select((f, i) => i < 150 ? new SoundFile(f.FileName, text: "reworded", displayName: f.DisplayName) : f)
            .ToArray();

        Assert.Equal(150, manifest.FilesToCreate(current, _tempDirectory).Count());
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

    [Fact]
    public async Task FilesToCreate_ExcludesEntries_SharingADisplayNameUnderDifferentKeys()
    {
        SoundFile first = new("111.ogg", ssml: "<speak>Shadow Bolt</speak>", displayName: "Shadow Bolt")
        {
            ExplicitKey = "111"
        };
        SoundFile second = new("222.ogg", ssml: "<speak>Shadow Bolt</speak>", displayName: "Shadow Bolt")
        {
            ExplicitKey = "222"
        };
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, [first, second]);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, first.FileName), "fake audio");
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, second.FileName), "fake audio");

        Assert.Empty(manifest.FilesToCreate([first, second], _tempDirectory));
    }

    [Fact]
    public async Task FilesToCreate_IncludesEntry_WhenItsDisplayNameChangedUnderTheSameKey()
    {
        SoundFile original = new("111.ogg", ssml: "<speak>Shadow Bolt</speak>", displayName: "Shadow Bolt")
        {
            ExplicitKey = "111"
        };
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, [original]);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, original.FileName), "fake audio");

        SoundFile renamed = new("111.ogg", ssml: "<speak>Shadow Blast</speak>", displayName: "Shadow Blast")
        {
            ExplicitKey = "111"
        };

        Assert.Contains(renamed, manifest.FilesToCreate([renamed], _tempDirectory));
    }

    [Fact]
    public async Task FilesToCreate_IncludesEntry_WhenItsWordsChangedUnderANewKey()
    {
        // Callouts re-keys a name it retires while keeping its file, so a key is not what makes a
        // recording current. Keyed lookup called this unchanged, skipped it, then saved the new
        // words over the old audio, which no later build had any reason to re-render.
        SoundFile original = new("chi_ji.ogg", ssml: "<speak>Chi-Ji</speak>", displayName: "Invoke Chi-Ji");
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, [original]);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, original.FileName), "fake audio");

        SoundFile reKeyed = new("chi_ji.ogg", ssml: "<speak>Chee Jee</speak>", displayName: "Invoke Chi-ji")
        {
            ExplicitKey = "retired:Invoke Chi-ji"
        };

        Assert.Contains(reKeyed, manifest.FilesToCreate([reKeyed], _tempDirectory));
    }

    [Fact]
    public async Task FilesToCreate_ExcludesEntries_ThatOnlyRenamedTheirSharedRecording()
    {
        // ExBoss points several labels at one file. Their names are registrations, not speech, so
        // renaming one must not re-render the recording they share on every build.
        SoundFile first = new("adds.ogg", text: "Adds", displayName: "注意小怪");
        SoundFile second = new("adds.ogg", text: "Adds", displayName: "点名小怪");
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, [first, second]);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "adds.ogg"), "fake audio");

        SoundFile renamed = new("adds.ogg", text: "Adds", displayName: "躲开小怪");

        Assert.Empty(manifest.FilesToCreate([first, renamed], _tempDirectory));
    }

    [Fact]
    public async Task FilesToCreate_IncludesEverySound_WhenTheRecipeChanged()
    {
        SoundFile first = new("alert.ogg", text: "Alert", displayName: "Alert");
        SoundFile second = new("adds.ogg", text: "Adds", displayName: "Adds");
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, [first, second]);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, first.FileName), "fake audio");
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, second.FileName), "fake audio");

        Assert.Empty(manifest.FilesToCreate([first, second], _tempDirectory));
        Assert.Equal(2, manifest.FilesToCreate([first, second], _tempDirectory, recipeChanged: true).Count());
    }

    [Fact]
    public async Task FilesToCreate_RendersOneFile_WhenTheRecipeChangedAndEntriesShareIt()
    {
        SoundFile first = new("adds.ogg", text: "Adds", displayName: "Adds");
        SoundFile alias = new("adds.ogg", text: "Adds", displayName: "More adds");
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);

        Assert.Single(manifest.FilesToCreate([first, alias], _tempDirectory, recipeChanged: true));
    }

    [Fact]
    public async Task FilesToRemove_ReturnsRecordings_TheAddOnNoLongerRegisters()
    {
        SoundFile kept = new("alert.ogg", text: "Alert", displayName: "Alert");
        SoundFile dropped = new("dropped.ogg", text: "Dropped", displayName: "Dropped");
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, [kept, dropped]);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);

        Assert.Equal(["dropped.ogg"], manifest.FilesToRemove([kept]));
    }

    [Fact]
    public async Task FilesToRemove_KeepsARecording_StillClaimedUnderAnotherKey()
    {
        SoundFile live = new("chi_ji.ogg", text: "Chi-Ji", displayName: "Invoke Chi-Ji");
        SoundFile retired = new("chi_ji.ogg", text: "Chi-Ji", displayName: "Invoke Chi-ji")
        {
            ExplicitKey = "retired:Invoke Chi-ji"
        };
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, [live, retired]);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);

        Assert.Empty(manifest.FilesToRemove([live]));
    }

    [Fact]
    public async Task FilesToRemove_Throws_WhenMostOfThePackWouldDisappear()
    {
        SoundFile[] shipped = Enumerable.Range(0, 100)
            .Select(i => new SoundFile($"{i}.ogg", text: $"Spell {i}", displayName: $"Spell {i}"))
            .ToArray();
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, shipped);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => manifest.FilesToRemove(shipped.Take(1)).ToArray());

        Assert.Contains("99", exception.Message);
    }

    [Fact]
    public async Task FilesToRemove_AllowsTheBiggestShrink_TheUpdateWorkflowLetsThrough()
    {
        // update.yml refuses an NSRT vocabulary that fell below half and has no removal cap of
        // its own, so 67 callouts coming back as 33 reaches the builder as a real build. That is
        // 34 removals, one past an unrounded half: refusing it here would fail the sync job and
        // take the BigWigs update down with it.
        SoundFile[] shipped = Enumerable.Range(0, 67)
            .Select(i => new SoundFile($"{i}.ogg", text: $"Callout {i}", displayName: $"Callout {i}"))
            .ToArray();
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, shipped);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);

        Assert.Equal(34, manifest.FilesToRemove(shipped.Take(33)).Count());
    }

    [Fact]
    public async Task FilesToRemove_AllowsASmallPack_ToRetireSeveralRecordingsAtOnce()
    {
        SoundFile[] shipped = Enumerable.Range(0, 12)
            .Select(i => new SoundFile($"{i}.ogg", text: $"Callout {i}", displayName: $"Callout {i}"))
            .ToArray();
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, shipped);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);

        Assert.Equal(8, manifest.FilesToRemove(shipped.Take(4)).Count());
    }

    [Fact]
    public async Task FilesToRemove_IgnoresRecordings_TheManifestNeverKnewAbout()
    {
        SoundFile kept = new("alert.ogg", text: "Alert", displayName: "Alert");
        SoundFileManifest savedManifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await savedManifest.SaveAsync(ManifestPath, [kept]);
        SoundFileManifest manifest = await SoundFileManifest.LoadAsync(ManifestPath);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "alerty.ogg"), "hand placed audio");

        Assert.Empty(manifest.FilesToRemove([kept]));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }

    private static IEnumerable<SoundFile> Recordings(int count, string text)
    {
        return Enumerable.Range(0, count)
            .Select(i => new SoundFile($"sound{i}.ogg", text: text, displayName: $"Sound {i}"));
    }
}
