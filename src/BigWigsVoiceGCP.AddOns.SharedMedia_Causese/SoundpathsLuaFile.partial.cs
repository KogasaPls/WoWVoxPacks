namespace BigWigsVoiceGCP.AddOns.SharedMedia_Causese;

public partial class SoundpathsLuaFile
{
    public CauseseAddOn AddOn { get; }
    public List<SoundFile> SoundFiles => AddOn.SoundFiles;

    public SoundpathsLuaFile(CauseseAddOn addOn)
    {
        AddOn = addOn;
    }

    public string GetSoundPath(SoundFile file)
    {
        return $@"{AddOn.SoundPath.Replace(@"\\", "\\")}\{file.FileName}";
    }
}
