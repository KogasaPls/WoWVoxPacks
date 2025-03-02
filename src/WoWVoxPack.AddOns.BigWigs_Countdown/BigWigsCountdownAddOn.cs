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
            new SoundFile("countdown_1.ogg", "1"),
            new SoundFile("countdown_2.ogg", "2"),
            new SoundFile("countdown_3.ogg", "3"),
            new SoundFile("countdown_4.ogg", "4"),
            new SoundFile("countdown_5.ogg", "5"),
            new SoundFile("countdown_6.ogg", "6"),
            new SoundFile("countdown_7.ogg", "7"),
            new SoundFile("countdown_8.ogg", "8"),
            new SoundFile("countdown_9.ogg", "9"),
            new SoundFile("countdown_10.ogg", "10")
        ];
    }
}
