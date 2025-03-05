using System.Collections.Frozen;
using System.IO.Compression;
using System.Text.RegularExpressions;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Causese;

public partial class ParsedSoundpathsLuaFile(string content)
{
    private static readonly string[] NonTtsSoundFiles = ["BITE.ogg", "Duck.ogg"];

    [GeneratedRegex("""LSM:Register\("sound", "(?<FormattedDisplayName>[^"]+)", \[\[(?<FileName>[^]]+)]]""")]
    private static partial Regex SoundFileRegex { get; }

    private IEnumerable<SoundFileRegexMatch> GetSoundFileRegexMatches()
    {
        foreach (Match match in SoundFileRegex.Matches(content))
        {
            yield return new SoundFileRegexMatch(match.Groups["FileName"].Value,
                match.Groups["FormattedDisplayName"].Value);
        }
    }

    private IEnumerable<PartialSoundFile> ParseSoundFiles()
    {
        foreach ((string? fileName, string? formattedDisplayName) in GetSoundFileRegexMatches())
        {
            yield return new PartialSoundFile
            {
                FileName = Path.GetFileName(fileName.Replace("\\", "/")),
                DisplayName = Path.GetFileNameWithoutExtension(fileName),
                FormattedDisplayName = formattedDisplayName
            };
        }
    }

    private static async Task<SoundFile> GetNonTtsSoundFileAsync(ZipArchiveEntry entry,
        CancellationToken cancellationToken = default)
    {
        string tmpPath = Path.GetTempFileName();
        await using Stream entryStream = entry.Open();
        await using FileStream fileStream = File.OpenWrite(tmpPath);
        await entryStream.CopyToAsync(fileStream, cancellationToken);

        string baseName = Path.GetFileNameWithoutExtension(entry.Name);
        return new SoundFile(entry.Name,
            formattedDisplayName: $"|cFFFF0000{baseName}|r") { CopyFromPath = tmpPath };
    }

    public async Task<IEnumerable<SoundFile>> GetSoundFilesAsync(ZipArchive archive,
        CancellationToken cancellationToken = default)
    {
        List<SoundFile> soundFiles = [];

        FrozenDictionary<string, PartialSoundFile> parsedSoundFilesByFileName =
            ParseSoundFiles().ToFrozenDictionary(x => x.FileName);

        foreach (ZipArchiveEntry entry in archive.Entries.Where(entry =>
                     entry.FullName.StartsWith("SharedMedia_Causese/sound/") && entry.Name.EndsWith(".ogg")))
        {
            SoundFile soundFile;

            if (NonTtsSoundFiles.Contains(entry.Name, StringComparer.OrdinalIgnoreCase))
            {
                soundFile = await GetNonTtsSoundFileAsync(entry, cancellationToken);
            }
            else
            {
                string baseName = Path.GetFileNameWithoutExtension(entry.Name);

                PartialSoundFile? partialSoundFile = parsedSoundFilesByFileName.GetValueOrDefault(entry.Name);
                if (partialSoundFile is not null)
                {
                    soundFile = new SoundFile($"{baseName}.ogg", displayName: partialSoundFile.DisplayName,
                        formattedDisplayName: partialSoundFile.FormattedDisplayName, text: entry.Name);
                }
                else
                {
                    soundFile = new SoundFile($"{baseName}.ogg", displayName: baseName,
                        formattedDisplayName: $"|cFFFF0000{baseName}|r", text: entry.Name);
                }
            }

            soundFiles.Add(soundFile);
        }

        return soundFiles;
    }

    private record struct SoundFileRegexMatch(string FileName, string FormattedDisplayName);

    private class PartialSoundFile
    {
        public string FileName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string FormattedDisplayName { get; set; } = string.Empty;
    }
}
