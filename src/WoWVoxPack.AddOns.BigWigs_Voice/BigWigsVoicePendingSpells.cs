namespace WoWVoxPack.AddOns.BigWigs_Voice;

/// <summary>
/// Spells BigWigs already announces that BigWigs_Voice has not synced yet, kept in its
/// Tools/spells-*.txt format. The pack adds them after the upstream list, so upstream wins for
/// any ID it lists.
/// </summary>
public sealed class BigWigsVoicePendingSpells
{
    public const string FileName = "bigwigs-voice-pending-spells.txt";

    private BigWigsVoicePendingSpells(IReadOnlyList<BigWigsVoiceSoundFile> soundFiles)
    {
        SoundFiles = soundFiles;
    }

    public static BigWigsVoicePendingSpells Empty { get; } = new([]);

    public IReadOnlyList<BigWigsVoiceSoundFile> SoundFiles { get; }

    public static BigWigsVoicePendingSpells Load(string path)
    {
        return Parse(Path.GetFileName(path), File.ReadAllText(path));
    }

    public static BigWigsVoicePendingSpells Parse(string fileName, string content)
    {
        return new BigWigsVoicePendingSpells(new SpellListFile(fileName, content, "pending").SoundFiles.ToArray());
    }
}
