using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public class BigWigsVoiceSoundFile : SoundFile
{
    public BigWigsVoiceSoundFile(string spellId, string spellName)
        : this(spellId, spellName, ParseIpaHints(spellName))
    {
    }

    private BigWigsVoiceSoundFile(string spellId, string spellName,
        (string Text, IReadOnlyList<Pronunciation> Pronunciations) spoken)
        : base($"{spellId}.ogg", text: spoken.Text, displayName: spoken.Text,
            pronunciations: spoken.Pronunciations)
    {
        SpellId = spellId;
        SpellName = spellName;

        // The spell ID, not the name: 507 names belong to more than one spell, and the ID is
        // what names the file and what BigWigs plays at runtime.
        ExplicitKey = spellId;
    }

    public string SpellId { get; }

    public string SpellName { get; }
}
