using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public sealed class BigWigsVoiceAddon : AddOn
{
    public BigWigsVoiceAddon(string outputDirectory, AddOnSettings settings, IEnumerable<SoundFile> soundFiles) : base(
        outputDirectory, settings)
    {
        AddCoreDotLuaFile();
        AddCountdownLuaFile();
        AddSoundFiles(soundFiles);
    }

    private void AddCoreDotLuaFile()
    {
        AddFile("Core.lua", addon =>
        {
            CoreLuaFile coreLuaFile = new(addon);
            return coreLuaFile.TransformText();
        });
    }

    private void AddCountdownLuaFile()
    {
        AddFile("Countdown.lua", addon =>
        {
            CountdownLuaFile countdownLuaFile = new(addon);
            return countdownLuaFile.TransformText();
        });
    }
}
