using System.Text;

using WoWVoxPack.AddOns;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

/// <summary>
/// Generates the voice pack's <c>Core.lua</c>: one handler for BigWigs' <c>BigWigs_Voice</c>
/// message that plays <c>Sounds\{key}.ogg</c>, where the key is the spell ID. A pack that has no
/// recording for a key hands the callout back to BigWigs to sound itself.
/// </summary>
public static class CoreLuaFile
{
    public static string Render(AddOn addOn)
    {
        string path = $"Interface\\\\AddOns\\\\{addOn.AddOnDirectoryName}";
        string rule = new('-', 80);

        StringBuilder lua = new();
        lua.Append('\n');
        lua.Append("local _, addon = ...\n");
        lua.Append('\n');
        lua.Append($"{rule}\n");
        lua.Append("-- Locals\n");
        lua.Append("--\n");
        lua.Append('\n');
        lua.Append("local tostring = tostring\n");
        lua.Append("local format = format\n");
        lua.Append("addon.SendMessage = BigWigsLoader.SendMessage\n");
        lua.Append('\n');
        lua.Append($"{rule}\n");
        lua.Append("-- Event Handlers\n");
        lua.Append("--\n");
        lua.Append($"local path = \"{path}\\\\Sounds\\\\%s.ogg\"\n");
        lua.Append($"local pathYou = \"{path}\\\\Sounds\\\\%sy.ogg\"\n");
        lua.Append("-- Nothing renders the y variants yet, so an on-me callout that goes straight to\n");
        lua.Append("-- BigWigs' own sound would lose the spell name on the alerts that matter most.\n");
        lua.Append("local function handler(_, module, key, sound, isOnMe)\n");
        lua.Append("\tlocal success = isOnMe and PlaySoundFile(format(pathYou, tostring(key)), \"Master\")\n");
        lua.Append("\tif not success then\n");
        lua.Append("\t\tsuccess = PlaySoundFile(format(path, tostring(key)), \"Master\")\n");
        lua.Append("\tend\n");
        lua.Append("\tif not success then\n");
        lua.Append("\t\taddon:SendMessage(\"BigWigs_Sound\", module, key, sound)\n");
        lua.Append("\tend\n");
        lua.Append("end\n");
        lua.Append('\n');
        lua.Append("BigWigsLoader.RegisterMessage(addon, \"BigWigs_Voice\", handler)\n");
        lua.Append("BigWigsAPI.RegisterVoicePack(\"temp\")");

        return lua.ToString();
    }
}
