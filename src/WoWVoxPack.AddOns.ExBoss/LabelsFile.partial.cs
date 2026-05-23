using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.ExBoss;

public partial class LabelsFile
{
    public LabelsFile(ExBossAddOn addOn)
    {
        AddOn = addOn;
    }

    private ExBossAddOn AddOn { get; }

    public IEnumerable<SoundFile> SoundFiles => AddOn.SoundFiles;

    public string GetLsmKey(SoundFile sound)
    {
        return $"[ExBoss WoWVoxPacks {AddOn.TtsSettings.Voice}]{sound.DisplayName}";
    }

    public string AddOnDirectoryName => AddOn.AddOnDirectoryName;
}
