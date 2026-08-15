using WoWVoxPack.AddOns;
using WoWVoxPack.AddOns.Callouts;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class NorthernSkyRaidToolsLuaFileTests
{
    private static readonly CalloutRegistration[] Registrations =
    [
        new(new SoundFile("drop_pool.ogg", text: "Drop Pool", displayName: "Drop Pool"), ["DropPool"]),
        new(new SoundFile("one.ogg", text: "One", displayName: "One"), ["1"]),
        new(new SoundFile("quoted.ogg", text: "Quoted", displayName: "Quoted"), ["Say \"Now\""])
    ];

    [Fact]
    public void Render_RegistersLiteralKeysAtThePerVoicePath()
    {
        string lua = Render();

        Assert.Contains(
            "local LSM = LibStub and LibStub(\"LibSharedMedia-3.0\", true)", lua,
            StringComparison.Ordinal);
        Assert.Contains(
            "local path = \"Interface\\\\AddOns\\\\WoWVoxPacks_NorthernSkyRaidTools_Neural2_C\\\\Sounds\\\\\"",
            lua, StringComparison.Ordinal);
        Assert.Contains(
            "LSM:Register(\"sound\", \"DropPool\", path .. \"drop_pool.ogg\")", lua,
            StringComparison.Ordinal);
        Assert.Contains(
            "LSM:Register(\"sound\", \"Say \\\"Now\\\"\", path .. \"quoted.ogg\")", lua,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Drop Pool\"", lua, StringComparison.Ordinal);
        Assert.DoesNotContain("WVP ", lua, StringComparison.Ordinal);
        Assert.DoesNotContain("|c", lua, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_GuardsAgainstASecondVoiceBeforeRegistering()
    {
        string lua = Render(VoiceName.Studio_O);

        int conflict = lua.IndexOf(
            "another Northern Sky Raid Tools voice pack is already active", StringComparison.Ordinal);
        int registration = lua.IndexOf("LSM:Register", StringComparison.Ordinal);

        Assert.Contains("Studio_O", lua, StringComparison.Ordinal);
        Assert.True(conflict >= 0);
        Assert.True(conflict < registration);
    }

    [Fact]
    public void Render_KeepsDuplicateLiteralMediaKeys()
    {
        CalloutRegistration registration = new(
            new SoundFile("soak.ogg", text: "Soak", displayName: "Soak"),
            ["Soak", "Soak", "soak"]);

        string lua = Render(registrations: [registration]);

        Assert.Equal(2, lua.Split("LSM:Register(\"sound\", \"Soak\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("LSM:Register(\"sound\", \"soak\", path .. \"soak.ogg\")", lua,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ContainsNoSharedAddonBehavior()
    {
        string lua = Render();

        string[] forbidden =
        [
            "Settings.", "SavedVariables", "WoWVoxPacksNorthernSkyRaidToolsDB", "NSAPI",
            "TTSOverSoundfile", "CreateFrame", "RegisterCallback", "ADDON_LOADED", "PLAYER_LOGIN",
            "HashTable", "Fetch(", "SLASH_", "C_VoiceChat"
        ];

        foreach (string value in forbidden)
        {
            Assert.DoesNotContain(value, lua, StringComparison.Ordinal);
        }
    }

    private static string Render(
        VoiceName voice = VoiceName.Neural2_C,
        IEnumerable<CalloutRegistration>? registrations = null)
    {
        registrations ??= Registrations;
        AddOn addOn = new AddOnBuilder(
                new AddOnSettings { Title = "unused", Version = "12.0.7", Author = "Tester" },
                new TtsSettings { Voice = voice })
            .WithTitle($"WoWVoxPacks_NorthernSkyRaidTools_{voice}")
            .AddSoundFiles(registrations.Select(registration => registration.SoundFile))
            .Build("output");

        return NorthernSkyRaidToolsLuaFile.Render(addOn, registrations);
    }
}
