using System.Text;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.ExBoss;

/// <summary>
/// <para>
/// Generates the ExBoss pack's <c>Core.lua</c>: one <c>LSM:Register</c> call per label, keyed
/// by <c>[ExBoss WoWVoxPacks {Voice}]{DisplayName}</c>. The prefix is required because
/// LibSharedMedia will not overwrite a key, so the first voice pack to load would otherwise
/// own it.
/// </para>
/// <para>
/// <c>LibStub</c> is only put on the load order by <c>## OptionalDeps</c>, not guaranteed
/// present, so the file calls it through the silent form and bails out rather than throwing at
/// login with no LibSharedMedia installed.
/// </para>
/// </summary>
public static class LabelsFile
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

    /// <summary>
    /// User-visible: this string is what a saved ExBoss/LibSharedMedia pick stores, so changing
    /// its shape silently drops every sound the user already chose.
    /// </summary>
    public static string GetLsmKey(AddOn addOn, SoundFile sound) =>
        $"[ExBoss WoWVoxPacks {addOn.TtsSettings.Voice}]{sound.DisplayName}";
}
