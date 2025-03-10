namespace WoWVoxPack.AddOns.BigWigs_Voice;

public partial class CoreLuaFile
{
    public CoreLuaFile(AddOn addOn)
    {
        AddOn = addOn;
    }

    public AddOn AddOn { get; }

    public string AddOnDirectoryPath => $@"Interface\\AddOns\\{AddOn.AddOnDirectoryName}";
}
