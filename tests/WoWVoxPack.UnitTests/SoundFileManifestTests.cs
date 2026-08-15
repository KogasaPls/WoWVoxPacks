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
}
