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

    /// <summary>The entries whose recording this build has to render.</summary>
    /// <param name="recipeChanged">
    /// True when the audio on disk was rendered by a different voice, rate, pitch or sample rate
    /// than this build asks for. None of that reaches the per-entry comparison, so without this
    /// the whole pack counts as up to date and keeps the old recipe's audio forever.
    /// </param>
    public IEnumerable<SoundFile> FilesToCreate(IEnumerable<SoundFile> currentSoundFiles, string soundDirectory,
        bool recipeChanged = false)
    {
        List<SoundFile> current = currentSoundFiles.ToList();

        if (recipeChanged)
        {
            return current.DistinctBy(f => f.FileName, StringComparer.OrdinalIgnoreCase);
        }

        IEnumerable<SoundFile> missing =
            current.Where(f => !File.Exists(Path.Combine(soundDirectory, f.FileName)));
        IEnumerable<SoundFile> changed = current.Where(f => !IsSameContentAsManifestEntry(f));

        return missing.UnionBy(changed, f => f.FileName);
    }

    /// <summary>
    /// Files this build no longer names but the last one did. An upstream that drops a spell
    /// leaves its recording behind otherwise, and it keeps shipping in every archive: 365 per
    /// BigWigs voice had accumulated by the 12.1.0 sync. Only files the previous manifest knew
    /// about are returned, so anything placed in the folder by hand is left alone.
    /// </summary>
    public IEnumerable<string> FilesToRemove(IEnumerable<SoundFile> currentSoundFiles)
    {
        HashSet<string> keep = new(currentSoundFiles.Select(f => f.FileName), StringComparer.OrdinalIgnoreCase);

        string[] removals = _soundFilesByKey.Values
            .Select(f => f.FileName)
            .Where(fileName => !keep.Contains(fileName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (removals.Length > RemovalLimit())
        {
            throw new InvalidOperationException(
                $"The build registers {keep.Count} recordings where the last one had " +
                $"{_soundFilesByKey.Count}, which would delete {removals.Length}. That is a " +
                "vocabulary that failed to load, not a pack that shrank.");
        }

        return removals;
    }

    /// <summary>
    /// How much of a pack may disappear in one build. The spell list comes from an unauthenticated
    /// listing of an upstream directory, and a rename there yields no entries and no error, which
    /// would otherwise delete a whole voice pack and commit it. Half, because that is the same line
    /// the update workflow draws on a vocabulary that suddenly shrank, and a builder that refuses
    /// earlier than the workflow does would fail the sync it is supposed to be protecting. Small
    /// packs get a flat allowance so that retiring a handful of callouts still works.
    /// </summary>
    private int RemovalLimit()
    {
        const int alwaysAllowed = 10;

        return Math.Max(alwaysAllowed, _soundFilesByKey.Count / 2);
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
