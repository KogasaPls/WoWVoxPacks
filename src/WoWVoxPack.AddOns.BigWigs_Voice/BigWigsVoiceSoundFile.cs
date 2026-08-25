using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public class BigWigsVoiceSoundFile : SoundFile
{
    public BigWigsVoiceSoundFile(string spellId, string spellName)
        : this(spellId, spellName, "upstream:unknown")
    {
    }

    public BigWigsVoiceSoundFile(string spellId, string spellName, string origin)
        : this(spellId, spellName, ParseIpaHints(spellName), origin)
    {
    }

    private BigWigsVoiceSoundFile(string spellId, string spellName,
        (string Text, IReadOnlyList<Pronunciation> Pronunciations) spoken,
        string origin)
        : base($"{spellId}.ogg", text: spoken.Text, displayName: spoken.Text)
    {
        SpellId = spellId;
        SpellName = spellName;

        // The spell ID, not the name: 507 names belong to more than one spell, and the ID is
        // what names the file and what BigWigs plays at runtime.
        ExplicitKey = spellId;
        ImportedPronunciations = spoken.Pronunciations
            .Select(pronunciation => new PronunciationHint(pronunciation, origin))
            .ToArray();
    }

    public string SpellId { get; }

    public string SpellName { get; }
}
