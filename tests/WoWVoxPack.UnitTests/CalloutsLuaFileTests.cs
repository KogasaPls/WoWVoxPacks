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
            "-- LibStub(name, true) is the silent form: nil instead of an error.\n" +
            "local LSM = LibStub and LibStub(\"LibSharedMedia-3.0\", true)\n" +
            "if not LSM then return end\n" +
            "\n" +
            "local path = \"Interface\\\\AddOns\\\\WoWVoxPacks_Callouts_Neural2_C\\\\Sounds\\\\\"\n" +
            "LSM:Register(\"sound\", \"|cffff7f3fWVP Neural2_C: Tranquility|r\", path .. \"tranquility.ogg\")\n" +
            "LSM:Register(\"sound\", \"|cffff7f3fWVP Neural2_C: Convoke the Spirits|r\", path .. \"convoke_the_spirits.ogg\")\n";

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
    public void Render_RegistersSoundsAndNothingElse()
    {
        AddOn addOn = BuildAddOn(new SoundFile("soak.ogg", text: "Soak", displayName: "Soak"));

        string lua = CalloutsLuaFile.Render(addOn);

        Assert.DoesNotContain("CreateFrame", lua);
        Assert.DoesNotContain("RegisterEvent", lua);
        Assert.DoesNotContain("print(", lua);
        Assert.DoesNotContain("DisableAddOn", lua);
        Assert.DoesNotContain("SharedMedia_Abilities", lua);
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
