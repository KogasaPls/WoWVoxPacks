using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public sealed class BigWigsVoiceAddOn : AddOn
{
    public BigWigsVoiceAddOn(string outputDirectory, AddOnSettings settings, TtsSettings ttsSettings,
        IEnumerable<SoundFile> soundFiles) : base(
        outputDirectory, settings, ttsSettings)
    {
        DisplayTitle = $"BigWigs |cffff7f3f+|r|cffffffffVoice: WoWVoxPacks ({ttsSettings.Voice})|r";

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
