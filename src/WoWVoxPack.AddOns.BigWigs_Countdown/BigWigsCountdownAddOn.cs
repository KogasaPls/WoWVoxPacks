using Ardalis.GuardClauses;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Countdown;

public class BigWigsCountdownAddOn : AddOn
{
    private static readonly Lazy<List<SoundFile>> CountdownSoundFiles = new(GetCountdownSoundFiles);

    public BigWigsCountdownAddOn(string outputDirectory, AddOnSettings settings, TtsSettings ttsSettings) : base(
        outputDirectory, settings, ttsSettings)
    {
        DisplayTitle = $"BigWigs |cffff7f3f+|r|cffffffffCountdown: VoxPacks {ttsSettings.Voice}|r";

        AddCountdownLuaFile();
        AddSoundFiles(CountdownSoundFiles.Value);
    }

    private void AddCountdownLuaFile()
    {
        AddFile("Countdown.lua", addon =>
        {
            CountdownLuaFile countdownLuaFile = new(addon);
            return countdownLuaFile.TransformText();
        });
    }

    private static List<SoundFile> GetCountdownSoundFiles()
    {
        return
        [
            new SoundFile("countdown_1", "1"),
            new SoundFile("countdown_2", "2"),
            new SoundFile("countdown_3", "3"),
            new SoundFile("countdown_4", "4"),
            new SoundFile("countdown_5", "5"),
            new SoundFile("countdown_6", "6"),
            new SoundFile("countdown_7", "7"),
            new SoundFile("countdown_8", "8"),
            new SoundFile("countdown_9", "9"),
            new SoundFile("countdown_10", "10")
        ];
    }
}
