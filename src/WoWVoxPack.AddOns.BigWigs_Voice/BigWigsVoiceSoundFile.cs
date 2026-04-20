using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public class BigWigsVoiceSoundFile(string spellId, string spellName)
    : SoundFile($"{spellId}.ogg", ssml: GetSsml(spellName), displayName: spellName)
{
    public string SpellId { get; } = spellId;

    public string SpellName { get; } = spellName;
}
