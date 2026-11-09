using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.Core.Builder;

public static class RetailRecordings
{
    public static IReadOnlyList<SoundFile> Matching(AddOn retail, AddOn target)
    {
        Dictionary<string, SoundFile> retailByFileName = retail.SoundFiles
            .DistinctBy(sound => sound.FileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(sound => sound.FileName, StringComparer.OrdinalIgnoreCase);

        return target.SoundFiles
            .DistinctBy(sound => sound.FileName, StringComparer.OrdinalIgnoreCase)
            .Where(sound =>
                retailByFileName.TryGetValue(sound.FileName, out SoundFile? recorded) &&
                SoundFileContentEqualityComparer.Default.Equals(recorded, sound) &&
                File.Exists(Path.Combine(retail.SoundDirectory, sound.FileName)))
            .ToArray();
    }

    /// <summary>Returns every matching name: each is in the target folder with retail's bytes.</summary>
    public static IReadOnlyList<string> CopyMatching(AddOn retail, AddOn target)
    {
        IReadOnlyList<SoundFile> matching = Matching(retail, target);
        if (matching.Count == 0)
        {
            return [];
        }

        Directory.CreateDirectory(target.SoundDirectory);
        foreach (SoundFile sound in matching)
        {
            string source = Path.Combine(retail.SoundDirectory, sound.FileName);
            string destination = Path.Combine(target.SoundDirectory, sound.FileName);
            if (!File.Exists(destination) ||
                !File.ReadAllBytes(source).AsSpan().SequenceEqual(File.ReadAllBytes(destination)))
            {
                File.Copy(source, destination, overwrite: true);
            }
        }

        return matching.Select(sound => sound.FileName).ToArray();
    }
}
