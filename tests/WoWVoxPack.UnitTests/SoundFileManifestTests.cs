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
