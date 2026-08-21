using System.Text;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.Callouts;

/// <summary>
/// <para>
/// Generates the media pack's <c>Core.lua</c>: one <c>LSM:Register</c> call per sound, keyed
/// by <c>|cffff7f3fWVP {Voice}: {DisplayName}|r</c>. The colour code pushes our entries below
/// the plain-text keys in LibSharedMedia's sound dropdown (the convention Northern Sky Raid
/// Tools uses for its own sounds; BigWigs registers plain keys) instead of alphabetically ahead
/// of all of them. The "WVP" prefix is still required because LibSharedMedia will not overwrite
/// a key, so the first voice pack to load would otherwise own it.
/// </para>
/// <para>
/// <c>LibStub</c> is only guaranteed to be on the load order by <c>## OptionalDeps</c>, not
/// guaranteed present, so the file calls it through the silent form and bails out rather than
/// throwing at login with no LibSharedMedia installed. Registering is all it does: it creates
/// no frame, listens for no event, and prints nothing.
/// </para>
/// </summary>
public static class CalloutsLuaFile
{
    public static string Render(AddOn addOn)
    {
        StringBuilder lua = new();
        lua.Append("-- LibStub(name, true) is the silent form: nil instead of an error.\n");
        lua.Append("local LSM = LibStub and LibStub(\"LibSharedMedia-3.0\", true)\n");
        lua.Append("if not LSM then return end\n");
        lua.Append('\n');
        lua.Append($"local path = \"Interface\\\\AddOns\\\\{addOn.AddOnDirectoryName}\\\\Sounds\\\\\"\n");

        foreach (SoundFile sound in addOn.SoundFiles)
        {
            lua.Append(
                $"LSM:Register(\"sound\", \"{GetLsmKey(addOn, sound)}\", path .. \"{sound.FileName}\")\n");
        }

        return lua.ToString();
    }

    public static string GetLsmKey(AddOn addOn, SoundFile sound)
    {
        string voice = addOn.TtsSettings.Voice?.ToString() ?? string.Empty;
        return CalloutsLsmKey.Format(voice, sound.DisplayName);
    }
}
