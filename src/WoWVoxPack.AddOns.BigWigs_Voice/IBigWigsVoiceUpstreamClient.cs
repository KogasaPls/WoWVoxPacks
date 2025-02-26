namespace WoWVoxPack.AddOns.BigWigs_Voice;

public interface IBigWigsVoiceUpstreamClient
{
    Task<IEnumerable<SpellListFile>> GetSpellListFiles(bool loadContent = true);
}
