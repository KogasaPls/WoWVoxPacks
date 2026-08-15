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
/// throwing at login with no LibSharedMedia installed. It also warns, once, if the pre-rename
/// <c>SharedMedia_Abilities_WoWVoxPacks_{Voice}</c> folder is still enabled. The two cannot
/// collide: that folder registers <c>WoWVoxPacks {Voice}: {Name}</c> and this one registers the
/// colour-wrapped <c>WVP</c> form. What it does is add a second, plain-text copy of all 132
/// entries to every sound dropdown, sorted above this pack's block, plus the disk the audio
/// takes. It is also the only thing still resolving the keys the user's existing profiles
/// store, so the warning says it is safe to delete once they have re-picked their sounds
/// rather than telling them to delete it now.
/// </para>
/// </summary>
public static class CalloutsLuaFile
{
    public static string Render(AddOn addOn)
    {
        StringBuilder lua = new();
        lua.Append("local function Warn(message)\n");
        lua.Append("    print(\"|cffff7f3fWoWVoxPacks|r \" .. message)\n");
        lua.Append("end\n");
        lua.Append('\n');
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

        lua.Append('\n');
        lua.Append(
            "-- The pre-rename folder registers the same sounds under plain-text keys, which sort above\n");
        lua.Append(
            "-- this pack's block. It cannot collide with these keys, and it is what still serves the\n");
        lua.Append(
            "-- names the user's existing profiles store, so this only reports it.\n");
        lua.Append("local warned = false\n");
        lua.Append("local function WarnStaleFolder()\n");
        lua.Append("    if warned then return end\n");
        lua.Append($"    local name = \"{GetPreRenameFolderName(addOn)}\"\n");
        lua.Append("    if not (C_AddOns and C_AddOns.IsAddOnLoaded and C_AddOns.IsAddOnLoaded(name)) then return end\n");
        lua.Append("    warned = true\n");
        lua.Append(
            "    Warn(name .. \" is still installed and adds a duplicate set of these sounds near the top \"\n");
        lua.Append(
            "        .. \"of every sound dropdown. It is safe to delete once you have re-picked your sounds.\")\n");
        lua.Append("end\n");
        lua.Append('\n');
        lua.Append("local frame = CreateFrame(\"Frame\")\n");
        lua.Append("frame:RegisterEvent(\"PLAYER_LOGIN\")\n");
        lua.Append("frame:SetScript(\"OnEvent\", WarnStaleFolder)\n");

        return lua.ToString();
    }

    public static string GetLsmKey(AddOn addOn, SoundFile sound)
    {
        string voice = addOn.TtsSettings.Voice?.ToString() ?? string.Empty;
        return CalloutsLsmKey.Format(voice, sound.DisplayName);
    }

    /// <summary>
    /// The folder name this media pack shipped under before the SharedMedia_Abilities to
    /// Callouts rename. A user who kept it enabled has it registering the same sounds under the
    /// old <c>WoWVoxPacks {Voice}: {Name}</c> keys. Those are different strings from the keys
    /// here, so nothing is shadowed; they just duplicate every entry, sorted above this pack's
    /// colour-wrapped block.
    /// </summary>
    private static string GetPreRenameFolderName(AddOn addOn)
    {
        string voice = addOn.TtsSettings.Voice?.ToString() ?? string.Empty;
        return $"SharedMedia_Abilities_WoWVoxPacks_{voice}";
    }
}
