using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Countdown;

public sealed class BigWigsCountdownAddOnService(IOptionsSnapshot<AddOnSettings> addOnOptions)
    : IAddOnService
{
    private static readonly Lazy<List<SoundFile>> CountdownSoundFiles = new(GetCountdownSoundFiles);

    private AddOnSettings AddOnSettings { get; } = addOnOptions.Get("BigWigs_Countdown");

    public Task<AddOn> BuildAddOnAsync(string outputDirectoryBase, TtsSettings ttsSettings,
        CancellationToken cancellationToken = default)
    {
        AddOn addOn = new AddOnBuilder(AddOnSettings, ttsSettings)
            .WithTitle($"BigWigs Countdown WoWVoxPacks {ttsSettings.Voice}")
            .WithDisplayTitle($"BigWigs |cffff7f3f+|r|cffffffffCountdown: WoWVoxPacks ({ttsSettings.Voice})|r")
            .AddSoundFiles(CountdownSoundFiles.Value)
            .AddFile("Countdown.lua", addon => new CountdownLuaFile(addon).TransformText())
            .Build(outputDirectoryBase);

        return Task.FromResult(addOn);
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
