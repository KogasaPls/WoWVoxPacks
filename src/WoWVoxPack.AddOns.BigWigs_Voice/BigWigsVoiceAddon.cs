using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public sealed class BigWigsVoiceAddon : AddOn
{
    public BigWigsVoiceAddon(string outputDirectory, AddOnSettings settings, IEnumerable<SoundFile> soundFiles) : base(
        outputDirectory, settings)
    {
        AddCoreDotLuaFile();

        string file = Path.Combine(AppContext.BaseDirectory, "BigWigsVoice_Sounds.json");
        AddSoundFileJson(file);

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
}
