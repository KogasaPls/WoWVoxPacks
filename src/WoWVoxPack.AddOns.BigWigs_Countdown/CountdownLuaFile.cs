using System.Text;

using WoWVoxPack.AddOns;

namespace WoWVoxPack.AddOns.BigWigs_Countdown;

/// <summary>
/// Generates the countdown pack's <c>Countdown.lua</c>: one <c>BigWigsAPI:RegisterCountdown</c>
/// call naming the ten numbered files. BigWigs is a hard <c>## Dependencies</c>, so
/// <c>BigWigsAPI</c> is guaranteed present. Nothing here touches LibSharedMedia.
/// </summary>
public static class CountdownLuaFile
{
    public static string Render(AddOn addOn)
    {
        StringBuilder lua = new();
        lua.Append($"local key = \"{GetVoicePackName(addOn)}\"\n");
        lua.Append($"local path = \"{GetSoundsPath(addOn)}\\\\countdown_%d.ogg\"\n");
        lua.Append(new string('-', 80));
        lua.Append('\n');
        lua.Append('\n');
        lua.Append("BigWigsAPI:RegisterCountdown(key, {\n");

        for (int i = 1; i <= 10; i++)
        {
            lua.Append($"    path:format({i}),\n");
        }

        lua.Append("})\n");
        return lua.ToString();
    }

    public static string GetVoicePackName(AddOn addOn) => $"WoWVoxPacks: {addOn.TtsSettings.Voice}";

    private static string GetSoundsPath(AddOn addOn) =>
        $"Interface\\\\AddOns\\\\{addOn.AddOnDirectoryName}\\\\{addOn.SoundDirectoryName}";
}
