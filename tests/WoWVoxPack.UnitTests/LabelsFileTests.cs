using WoWVoxPack.AddOns;
using WoWVoxPack.AddOns.ExBoss;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class LabelsFileTests
{
    private static AddOnSettings Settings => new()
    {
        Title = "ExBoss",
        Version = "12.0.7",
        Author = "KogasaPls",
        Notes = "Test."
    };

    private static AddOn BuildAddOn(params SoundFile[] soundFiles) =>
        new AddOnBuilder(Settings, new TtsSettings { Voice = VoiceName.Neural2_C })
            .WithTitle("ExBoss WoWVoxPacks Neural2_C")
            .AddSoundFiles(soundFiles)
            .Build("/tmp/output");

    [Fact]
    public void Render_MatchesTheEstablishedFormatByteForByte()
    {
        AddOn addOn = BuildAddOn(
            new SoundFile("aoe-inc.ogg", text: "AOE", displayName: "准备AOE"),
            new SoundFile("bait.ogg", text: "Bait", displayName: "准备引线"));

        const string expected =
            "-- LibStub(name, true) is the silent form: nil instead of an error.\n" +
            "local LSM = LibStub and LibStub(\"LibSharedMedia-3.0\", true)\n" +
            "if not LSM then return end\n" +
            "\n" +
            "local path = \"Interface\\\\AddOns\\\\ExBoss_WoWVoxPacks_Neural2_C\\\\Sounds\\\\\"\n" +
            "LSM:Register(\"sound\", \"[ExBoss WoWVoxPacks Neural2_C]准备AOE\", path .. \"aoe-inc.ogg\")\n" +
            "LSM:Register(\"sound\", \"[ExBoss WoWVoxPacks Neural2_C]准备引线\", path .. \"bait.ogg\")\n";

        Assert.Equal(expected, LabelsFile.Render(addOn));
    }

    [Fact]
    public void Render_GuardsLibStubAndReturnsEarlyWhenMissing()
    {
        AddOn addOn = BuildAddOn(new SoundFile("bait.ogg", text: "Bait", displayName: "准备引线"));

        string lua = LabelsFile.Render(addOn);

        // LibSharedMedia-3.0 is only OptionalDeps, so a plain LibStub(...) call errors at login
        // with no provider installed and puts this addon's name in a BugSack popup. The silent
        // form returns nil instead, and the file bails out rather than continuing.
        Assert.Contains("local LSM = LibStub and LibStub(\"LibSharedMedia-3.0\", true)", lua);
        Assert.Contains("if not LSM then return end", lua);
        Assert.DoesNotContain("local LSM = LibStub(\"LibSharedMedia-3.0\")\n", lua);
    }

    [Fact]
    public void GetLsmKey_KeepsTheShippedKeyFormat()
    {
        AddOn addOn = BuildAddOn();
        SoundFile sound = new("bait.ogg", text: "Bait", displayName: "准备引线");

        // This string is what a saved ExBoss/LibSharedMedia pick stores. Changing its shape
        // silently drops every sound the user already chose, so it is pinned here.
        Assert.Equal("[ExBoss WoWVoxPacks Neural2_C]准备引线", LabelsFile.GetLsmKey(addOn, sound));
    }

    [Fact]
    public void Render_NeverDisablesAnythingOrWarnsAboutAFolder()
    {
        AddOn addOn = BuildAddOn(new SoundFile("bait.ogg", text: "Bait", displayName: "准备引线"));

        string lua = LabelsFile.Render(addOn);

        Assert.DoesNotContain("DisableAddOn", lua);
        Assert.DoesNotContain("IsAddOnLoaded", lua);
    }
}
