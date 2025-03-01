using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Countdown;

public class BigWigsCountdownAddon : AddOn
{
    private static readonly Lazy<List<SoundFile>> CountdownSoundFiles = new(GetCountdownSoundFiles);

    public BigWigsCountdownAddon(string outputDirectory, AddOnSettings settings) : base(
        outputDirectory, settings)
    {
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
