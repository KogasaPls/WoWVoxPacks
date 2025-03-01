namespace WoWVoxPack.AddOns.BigWigs_Countdown;

public partial class CountdownLuaFile
{
    public CountdownLuaFile(AddOn addOn)
    {
        AddOn = addOn;
    }

    public AddOn AddOn { get; }

    public string SoundsPath => $@"Interface\\AddOns\\{AddOn.AddOnDirectoryName}\\{AddOn.SoundDirectoryName}";
}
