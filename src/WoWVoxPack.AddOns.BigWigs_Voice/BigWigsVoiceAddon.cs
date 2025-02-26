namespace WoWVoxPack.AddOns.BigWigs_Voice;

internal sealed class BigWigsVoiceAddon : AddOn
{
    public BigWigsVoiceAddon(AddOnSettings settings) : base(settings)
    {
        AddFile("Core.lua", addon =>
        {
            CoreLuaFile coreLuaFile = new(addon);
            return coreLuaFile.TransformText();
        });
    }
}
