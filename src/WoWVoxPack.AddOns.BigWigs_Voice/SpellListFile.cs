using System.Xml.Linq;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

internal class SpellListFile
{
    private readonly Lazy<BigWigsVoiceSoundFile[]> _soundFiles;

    public SpellListFile(string fileName, string content)
    {
        FileName = fileName;
        Content = content;

        _soundFiles = new Lazy<BigWigsVoiceSoundFile[]>(ParseSoundFilesToArray);
    }

    public string FileName { get; }
    public string Content { get; }

    public IEnumerable<BigWigsVoiceSoundFile> SoundFiles => _soundFiles.Value;

    private BigWigsVoiceSoundFile[] ParseSoundFilesToArray() => ParseSoundFiles().ToArray();

    private IEnumerable<BigWigsVoiceSoundFile> ParseSoundFiles()
    {
        using StringReader reader = new(Content);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith(';') || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] parts = line.Split('\t');
            if (parts.Length != 2)
            {
                continue;
            }


            string spellId = parts[0];
            string spellName = parts[1];

            yield return new BigWigsVoiceSoundFile(spellId, spellName);
        }
    }
}
