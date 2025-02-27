using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Causese;

public partial class SoundpathsLuaFile
{
    public SoundpathsLuaFile(CauseseAddOn addOn)
    {
        AddOn = addOn;
    }

    public CauseseAddOn AddOn { get; }

    public string GetSoundPath(SoundFile file)
    {
        return $@"{AddOn.SoundPath.Replace(@"\\", @"\")}\{file.FileName}";
    }
}
