namespace BigWigsVoiceGCP.AddOns.BigWigsVoice;

public interface IBigWigsVoiceUpstreamClient
{
    Task<IEnumerable<SpellListFile>> GetSpellListFiles(bool loadContent = true);
}
