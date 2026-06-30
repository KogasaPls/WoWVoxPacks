using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Abilities;

public partial class AbilitiesLuaFile
{
    public AbilitiesLuaFile(AddOn addOn)
    {
        AddOn = addOn;
    }

    public AddOn AddOn { get; }

    public string AddOnDirectoryName => AddOn.AddOnDirectoryName;

    public IEnumerable<SoundFile> SoundFiles => AddOn.SoundFiles;

    public string GetLsmKey(SoundFile sound) => $"WoWVoxPacks {AddOn.TtsSettings.Voice}: {sound.DisplayName}";
}
