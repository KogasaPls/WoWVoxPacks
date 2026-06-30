using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.ExBoss;

public partial class LabelsFile
{
    public LabelsFile(AddOn addOn)
    {
        AddOn = addOn;
    }

    private AddOn AddOn { get; }

    public IEnumerable<SoundFile> SoundFiles => AddOn.SoundFiles;

    public string GetLsmKey(SoundFile sound)
    {
        return $"[ExBoss WoWVoxPacks {AddOn.TtsSettings.Voice}]{sound.DisplayName}";
    }

    public string AddOnDirectoryName => AddOn.AddOnDirectoryName;
}
