using System.Text.Json;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Abilities;

public sealed class SharedMediaAbilitiesAddOn : AddOn
{
    public SharedMediaAbilitiesAddOn(string outputDirectory, AddOnSettings settings, TtsSettings ttsSettings)
        : base(outputDirectory, settings, ttsSettings)
    {
        Title = $"SharedMedia Abilities WoWVoxPacks {ttsSettings.Voice}";
        DisplayTitle = $"SharedMedia |cffff7f3f+|r|cffffffffAbilities: WoWVoxPacks ({ttsSettings.Voice})|r";

        string file = Path.Combine(AppContext.BaseDirectory, "SharedMedia_Abilities_Sounds.json");
        LoadSoundsFromJson(file);

        AddAbilitiesLuaFile();
    }

    private void LoadSoundsFromJson(string filePath)
    {
        List<SoundFile> soundFiles =
            JsonSerializer.Deserialize<List<SoundFile>>(File.ReadAllText(filePath),
                SoundFileJsonContext.Default.ListSoundFile) ?? [];

        AddSoundFiles(soundFiles.Select(sf =>
            sf.Ssml is null && sf.Text?.Contains('=') == true
                ? new SoundFile(sf.FileName, ssml: SoundFile.GetSsml(sf.Text), displayName: sf.DisplayName,
                    formattedDisplayName: sf.FormattedDisplayName)
                : sf), true);
    }

    private void AddAbilitiesLuaFile()
    {
        AddFile("Core.lua", addon => new AbilitiesLuaFile((SharedMediaAbilitiesAddOn)addon).TransformText());
    }
}
