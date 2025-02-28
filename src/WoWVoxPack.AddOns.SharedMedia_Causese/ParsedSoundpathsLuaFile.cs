using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.RegularExpressions;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Causese;

public partial class ParsedSoundpathsLuaFile(string content)
{
    [GeneratedRegex("""LSM:Register\("sound", "(?<FormattedDisplayName>[^"]+)", \[\[(?<FileName>[^]]+)]]""")]
    public partial Regex SoundFileRegex { get; }
    private class PartialSoundFile
    {
        public string FileName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string FormattedDisplayName { get; set; } = string.Empty;
    }


    public async Task<IEnumerable<SoundFile>> GetSoundFilesAsync(ZipArchive archive,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, PartialSoundFile> parsedSoundFilesByFileName = new();

        // LSM:Register("sound", "|cFFFF0000Bark|r", [[Interface\AddOns\SharedMedia_Causese\sound\Bark.ogg]])
        foreach (Match match in SoundFileRegex.Matches(content))
        {
            string fileName = Path.GetFileName(match.Groups["FileName"].Value.Replace("\\", "/"));
            string formattedDisplayName = match.Groups["FormattedDisplayName"].Value;

            parsedSoundFilesByFileName[fileName] = new PartialSoundFile
            {
                FileName = fileName,
                DisplayName = Path.GetFileNameWithoutExtension(fileName),
                FormattedDisplayName = formattedDisplayName
            };
        }


        ConcurrentBag<SoundFile> soundFiles = new();

        foreach (ZipArchiveEntry entry in archive.Entries.Where(entry =>
                     entry.FullName.StartsWith("SharedMedia_Causese/sound/") && entry.Name.EndsWith(".ogg")))
        {
            string baseName;
            SoundFile soundFile;

            if (entry.Name.Equals("BITE.ogg", StringComparison.OrdinalIgnoreCase) ||
                entry.Name.Equals("Duck.ogg", StringComparison.OrdinalIgnoreCase))
            {
                string tmpPath = Path.GetTempFileName();
                await using Stream entryStream = entry.Open();
                await using FileStream fileStream = File.OpenWrite(tmpPath);
                await entryStream.CopyToAsync(fileStream, cancellationToken);

                baseName = Path.GetFileNameWithoutExtension(entry.Name);
                soundFile = new SoundFile(entry.Name,
                    formattedDisplayName: $"|cFFFF0000{baseName}|r") { CopyFromPath = tmpPath };
            }
            else
            {
                var partialSoundFile = parsedSoundFilesByFileName.GetValueOrDefault(entry.Name);
                if (partialSoundFile is not null)
                {
                    soundFile = new SoundFile(entry.Name, displayName: partialSoundFile.DisplayName,
                        formattedDisplayName: partialSoundFile.FormattedDisplayName);
                }
                else
                {
                    baseName = Path.GetFileNameWithoutExtension(entry.Name);
                    soundFile = new SoundFile(entry.Name, baseName, displayName: baseName,
                        formattedDisplayName: $"|cFFFF0000{baseName}|r");
                }
            }

            soundFiles.Add(soundFile);
        }

        return soundFiles;
    }
}
