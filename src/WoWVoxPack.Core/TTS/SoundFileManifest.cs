using System.Text.Json;

namespace WoWVoxPack.TTS;

public sealed class SoundFileManifest
{
    private readonly IReadOnlyDictionary<string, SoundFile> _soundFilesByKey;

    private SoundFileManifest(IReadOnlyDictionary<string, SoundFile> soundFilesByKey)
    {
        _soundFilesByKey = soundFilesByKey;
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
            soundFiles.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase));
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

    /// <summary>
    /// A key the manifest has never seen counts as unchanged: with the file already on disk there
    /// is nothing to render, and a re-key of an addon therefore costs nothing.
    /// </summary>
    private bool IsSameContentAsManifestEntry(SoundFile soundFile)
    {
        if (!_soundFilesByKey.TryGetValue(soundFile.Key, out SoundFile? existing))
        {
            return true;
        }

        return SoundFileContentEqualityComparer.Default.Equals(soundFile, existing);
    }
}
