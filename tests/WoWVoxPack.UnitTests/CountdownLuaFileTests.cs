using WoWVoxPack.AddOns;
using WoWVoxPack.AddOns.BigWigs_Countdown;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class CountdownLuaFileTests
{
    private static AddOnSettings Settings => new()
    {
        Title = "BigWigs_Countdown_WoWVoxPacks",
        Version = "12.0.7",
        Author = "KogasaPls",
        Notes = "Test."
    };

    [Fact]
    public void Render_MatchesTheEstablishedFormatByteForByte()
    {
        const string expected =
            "local key = \"WoWVoxPacks: Neural2_C\"\n" +
            "local path = \"Interface\\\\AddOns\\\\BigWigs_Countdown_WoWVoxPacks_Neural2_C\\\\Sounds\\\\countdown_%d.ogg\"\n" +
            "--------------------------------------------------------------------------------\n" +
            "\n" +
            "BigWigsAPI:RegisterCountdown(key, {\n" +
            "    path:format(1),\n" +
            "    path:format(2),\n" +
            "    path:format(3),\n" +
            "    path:format(4),\n" +
            "    path:format(5),\n" +
            "    path:format(6),\n" +
            "    path:format(7),\n" +
            "    path:format(8),\n" +
            "    path:format(9),\n" +
            "    path:format(10),\n" +
            "})\n";

        Assert.Equal(expected, CountdownLuaFile.Render(BuildAddOn()));
    }

    [Fact]
    public void Render_NeverCallsLibStub()
    {
        string lua = CountdownLuaFile.Render(BuildAddOn());

        // This pack declares no LibSharedMedia dependency at all and never used the handle it
        // asked for, so the plain LibStub(...) call only ever produced a login error for users
        // with no provider installed.
        Assert.DoesNotContain("LibStub", lua);
        Assert.DoesNotContain("LibSharedMedia", lua);
    }

    [Fact]
    public void Render_BindsNoUnusedLocals()
    {
        string lua = CountdownLuaFile.Render(BuildAddOn());

        // BigWigsAPI:GetLocale was assigned to an L nothing referenced.
        Assert.DoesNotContain("GetLocale", lua);

        foreach (string local in lua.Split('\n')
                     .Where(line => line.StartsWith("local ", StringComparison.Ordinal))
                     .Select(line => line["local ".Length..].Split(' ')[0]))
        {
            Assert.Contains(local, lua.Replace($"local {local} =", string.Empty),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GetVoicePackName_KeepsTheShippedKeyFormat()
    {
        // BigWigs stores this string in the user's profile as the chosen countdown.
        Assert.Equal("WoWVoxPacks: Neural2_C", CountdownLuaFile.GetVoicePackName(BuildAddOn()));
    }

    private static AddOn BuildAddOn() =>
        new AddOnBuilder(Settings, new TtsSettings { Voice = VoiceName.Neural2_C })
            .WithTitle("BigWigs Countdown WoWVoxPacks Neural2_C")
            .Build("/tmp/output");
}
