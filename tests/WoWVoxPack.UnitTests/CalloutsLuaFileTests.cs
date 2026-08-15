using WoWVoxPack.AddOns;
using WoWVoxPack.AddOns.Callouts;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class CalloutsLuaFileTests
{
    private static AddOnSettings Settings => new()
    {
        Title = "WoWVoxPacks_Callouts",
        Version = "12.0.7",
        Author = "KogasaPls",
        Notes = "Test."
    };

    [Fact]
    public void Render_MatchesTheEstablishedFormatByteForByte()
    {
        AddOn addOn = BuildAddOn(
            new SoundFile("tranquility.ogg", text: "Tranquility", displayName: "Tranquility"),
            new SoundFile("convoke_the_spirits.ogg", text: "Convoke the Spirits",
                displayName: "Convoke the Spirits"));

        const string expected =
            "local function Warn(message)\n" +
            "    print(\"|cffff7f3fWoWVoxPacks|r \" .. message)\n" +
            "end\n" +
            "\n" +
            "-- LibStub(name, true) is the silent form: nil instead of an error.\n" +
            "local LSM = LibStub and LibStub(\"LibSharedMedia-3.0\", true)\n" +
            "if not LSM then return end\n" +
            "\n" +
            "local path = \"Interface\\\\AddOns\\\\WoWVoxPacks_Callouts_Neural2_C\\\\Sounds\\\\\"\n" +
            "LSM:Register(\"sound\", \"|cffff7f3fWVP Neural2_C: Tranquility|r\", path .. \"tranquility.ogg\")\n" +
            "LSM:Register(\"sound\", \"|cffff7f3fWVP Neural2_C: Convoke the Spirits|r\", path .. \"convoke_the_spirits.ogg\")\n" +
            "\n" +
            "-- The pre-rename folder registers the same sounds under plain-text keys, which sort above\n" +
            "-- this pack's block. It cannot collide with these keys, and it is what still serves the\n" +
            "-- names the user's existing profiles store, so this only reports it.\n" +
            "local warned = false\n" +
            "local function WarnStaleFolder()\n" +
            "    if warned then return end\n" +
            "    local name = \"SharedMedia_Abilities_WoWVoxPacks_Neural2_C\"\n" +
            "    if not (C_AddOns and C_AddOns.IsAddOnLoaded and C_AddOns.IsAddOnLoaded(name)) then return end\n" +
            "    warned = true\n" +
            "    Warn(name .. \" is still installed and adds a duplicate set of these sounds near the top \"\n" +
            "        .. \"of every sound dropdown. It is safe to delete once you have re-picked your sounds.\")\n" +
            "end\n" +
            "\n" +
            "local frame = CreateFrame(\"Frame\")\n" +
            "frame:RegisterEvent(\"PLAYER_LOGIN\")\n" +
            "frame:SetScript(\"OnEvent\", WarnStaleFolder)\n";

        Assert.Equal(expected, CalloutsLuaFile.Render(addOn));
    }

    [Fact]
    public void Render_GuardsLibStubAndReturnsEarlyWhenMissing()
    {
        AddOn addOn = BuildAddOn(new SoundFile("soak.ogg", text: "Soak", displayName: "Soak"));

        string lua = CalloutsLuaFile.Render(addOn);

        // The plain LibStub(...) call errors at login with no LibSharedMedia provider; the
        // silent form returns nil instead, and the file bails out rather than continuing.
        Assert.Contains("local LSM = LibStub and LibStub(\"LibSharedMedia-3.0\", true)", lua);
        Assert.Contains("if not LSM then return end", lua);
        Assert.DoesNotContain("local LSM = LibStub(\"LibSharedMedia-3.0\")\n", lua);
    }

    [Fact]
    public void Render_WarnsWhenThePreRenameFolderIsStillLoaded()
    {
        AddOn addOn = BuildAddOn(new SoundFile("soak.ogg", text: "Soak", displayName: "Soak"));

        string lua = CalloutsLuaFile.Render(addOn);

        Assert.Contains("SharedMedia_Abilities_WoWVoxPacks_Neural2_C", lua);
        Assert.Contains("C_AddOns.IsAddOnLoaded", lua);

        // The old folder cannot shadow these keys, and it is the only thing still resolving
        // the names the user's profiles store, so deleting it now is what breaks them.
        Assert.Contains("safe to delete once you have re-picked your sounds", lua);
        Assert.DoesNotContain("Delete it.", lua);

        // Fires once per session, not on every event.
        Assert.Contains("local warned = false", lua);
        Assert.Contains("if warned then return end", lua);

        // Never disable anything on the user's behalf.
        Assert.DoesNotContain("DisableAddOn", lua);
    }

    [Fact]
    public void GetLsmKey_ProducesTheColourWrappedShortenedKey()
    {
        AddOn addOn = BuildAddOn();
        SoundFile sound = new("soak.ogg", text: "Soak", displayName: "Soak");

        string key = CalloutsLuaFile.GetLsmKey(addOn, sound);

        Assert.Equal("|cffff7f3fWVP Neural2_C: Soak|r", key);
        Assert.Equal(CalloutsLsmKey.Format("Neural2_C", "Soak"), key);
    }

    [Fact]
    public void Render_ContainsNoBareLegacyKey()
    {
        AddOn addOn = BuildAddOn(new SoundFile("soak.ogg", text: "Soak", displayName: "Soak"));

        string lua = CalloutsLuaFile.Render(addOn);

        Assert.DoesNotContain("\"Soak\"", lua);
    }

    private static AddOn BuildAddOn(params SoundFile[] soundFiles) =>
        new AddOnBuilder(Settings, new TtsSettings { Voice = VoiceName.Neural2_C })
            .WithTitle("WoWVoxPacks Callouts Neural2_C")
            .AddSoundFiles(soundFiles)
            .Build("/tmp/output");
}
