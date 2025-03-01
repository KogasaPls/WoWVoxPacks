using Ardalis.GuardClauses;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public sealed class BigWigsVoiceAddon : AddOn
{
    public BigWigsVoiceAddon(string outputDirectory, AddOnSettings settings,TtsSettings ttsSettings, IEnumerable<SoundFile> soundFiles) : base(
        outputDirectory, settings, ttsSettings)
    {
        DisplayTitle = $"BigWigs |cffff7f3f+|r|cffffffffVoice: VoxPacks {ttsSettings.Voice}|r";

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
