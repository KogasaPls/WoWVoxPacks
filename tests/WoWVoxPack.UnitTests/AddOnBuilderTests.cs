using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class AddOnBuilderTests
{
    private static AddOnSettings DefaultSettings => new()
    {
        Title = "Test_AddOn",
        Version = "12.0.7",
        Author = "Tester",
        Notes = "A test addon.",
        AdditionalProperties = new Dictionary<string, string> { ["X-License"] = "Apache-2.0" }
    };

    private static TtsSettings DefaultTtsSettings => new() { Voice = VoiceName.Neural2_C };

    [Fact]
    public void Build_UsesSettingsForMetadata_WhenNotOverridden()
    {
        AddOn addOn = new AddOnBuilder(DefaultSettings, DefaultTtsSettings).Build("/tmp/output");

        Assert.Equal("Test_AddOn", addOn.Title);
        Assert.Equal("Test_AddOn", addOn.DisplayTitle);
        Assert.Equal("12.0.7", addOn.Version);
        Assert.Equal("Tester", addOn.Author);
        Assert.Equal("120007", addOn.Interfaces.Single());
        Assert.Equal("A test addon.", addOn.PrimaryNote?.Text);
        Assert.Equal("Apache-2.0", addOn.AdditionalProperties["X-License"]);
    }

    [Fact]
    public void Build_UsesConfiguredInterfaces_AndFallsBackToVersionDerivedInterface_WhenNotConfigured()
    {
        AddOnSettings configured = DefaultSettings;
        configured.Interfaces = ["120007", "120100"];
        AddOn withConfiguredInterfaces = new AddOnBuilder(configured, DefaultTtsSettings).Build("/tmp/output");
        Assert.Equal(["120007", "120100"], withConfiguredInterfaces.Interfaces);

        AddOn withoutConfiguredInterfaces = new AddOnBuilder(DefaultSettings, DefaultTtsSettings).Build("/tmp/output");
        Assert.Equal(["120007"], withoutConfiguredInterfaces.Interfaces);
    }

    [Fact]
    public void Build_DeduplicatesInterfaces()
    {
        // Every addon binds its own AddOn:{Name} section and then the AddOn root, and the
        // configuration binder appends to a list rather than replacing it.
        AddOnSettings configured = DefaultSettings;
        configured.Interfaces = ["120007", "120100", "120007"];

        AddOn addOn = new AddOnBuilder(configured, DefaultTtsSettings).Build("/tmp/output");

        Assert.Equal(["120007", "120100"], addOn.Interfaces);
    }

    [Theory]
    [InlineData("12.0.7")]
    [InlineData("1200o7")]
    [InlineData("1200")]
    [InlineData("1200077")]
    [InlineData("")]
    public void Build_RejectsAnInterfaceThatIsNotATocInterfaceNumber(string @interface)
    {
        // WoW has no error path for a malformed '## Interface:' line: it treats the addon as
        // unsupported and says nothing.
        AddOnSettings configured = DefaultSettings;
        configured.Interfaces = [@interface];

        Assert.Throws<InvalidOperationException>(
            () => new AddOnBuilder(configured, DefaultTtsSettings).Build("/tmp/output"));
    }

    [Fact]
    public void Build_PrefersExplicitTitleAndDisplayTitle_OverSettings()
    {
        AddOn addOn = new AddOnBuilder(DefaultSettings, DefaultTtsSettings)
            .WithTitle("Overridden Title")
            .WithDisplayTitle("Overridden Display")
            .Build("/tmp/output");

        Assert.Equal("Overridden Title", addOn.Title);
        Assert.Equal("Overridden Display", addOn.DisplayTitle);
    }

    [Fact]
    public void AddSoundFile_DoesNotOverwriteExisting_UnlessOverwriteIsTrue()
    {
        SoundFile original = new("alert.ogg", text: "Original", displayName: "Alert");
        SoundFile replacement = new("alert.ogg", text: "Replacement", displayName: "Alert");

        AddOn addOn = new AddOnBuilder(DefaultSettings, DefaultTtsSettings)
            .AddSoundFile(original)
            .AddSoundFile(replacement)
            .Build("/tmp/output");

        Assert.Equal("Original", Assert.Single(addOn.SoundFiles).Text);
    }

    [Fact]
    public void AddSoundFile_Overwrites_WhenOverwriteIsTrue()
    {
        SoundFile original = new("alert.ogg", text: "Original", displayName: "Alert");
        SoundFile replacement = new("alert.ogg", text: "Replacement", displayName: "Alert");

        AddOn addOn = new AddOnBuilder(DefaultSettings, DefaultTtsSettings)
            .AddSoundFile(original)
            .AddSoundFile(replacement, overwrite: true)
            .Build("/tmp/output");

        Assert.Equal("Replacement", Assert.Single(addOn.SoundFiles).Text);
    }

    [Fact]
    public void AddSoundFile_KeepsBoth_WhenTheyShareADisplayNameButNotAKey()
    {
        SoundFile first = new("111.ogg", text: "Shadow Bolt", displayName: "Shadow Bolt") { ExplicitKey = "111" };
        SoundFile second = new("222.ogg", text: "Shadow Bolt", displayName: "Shadow Bolt") { ExplicitKey = "222" };

        AddOn addOn = new AddOnBuilder(DefaultSettings, DefaultTtsSettings)
            .AddSoundFile(first)
            .AddSoundFile(second)
            .Build("/tmp/output");

        Assert.Equal(["111.ogg", "222.ogg"], addOn.SoundFiles.Select(f => f.FileName).Order());
    }

    [Fact]
    public void AddFile_FactoryReceivesFullyAssembledAddOn()
    {
        SoundFile soundFile = new("alert.ogg", text: "Alert", displayName: "Alert");

        AddOn addOn = new AddOnBuilder(DefaultSettings, DefaultTtsSettings)
            .AddSoundFile(soundFile)
            .AddFile("Core.lua", built => string.Join(",", built.SoundFiles.Select(f => f.DisplayName)))
            .Build("/tmp/output");

        Assert.Equal("Alert", addOn.GetFileContent("Core.lua"));
    }

    [Fact]
    public void LoadSoundFileJson_DeserializesSoundFilesFromDisk()
    {
        string path = WriteTempJson(
            """[{"FileName":"alert.ogg","DisplayName":"Alert","Text":"incoming"}]""");
        try
        {
            List<SoundFile> soundFiles = AddOnBuilder.LoadSoundFileJson(path);

            SoundFile soundFile = Assert.Single(soundFiles);
            Assert.Equal("alert.ogg", soundFile.FileName);
            Assert.Equal("Alert", soundFile.DisplayName);
            Assert.Equal("incoming", soundFile.Text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadSoundFileJsonWithIpaHints_LiftsHints_OnlyForEntriesWithIpaEscape()
    {
        string path = WriteTempJson("""
            [
              {"FileName":"plain.ogg","DisplayName":"Plain","Text":"incoming"},
              {"FileName":"taivan.ogg","DisplayName":"Taivan","Text":"Taivan=t1 incoming"}
            ]
            """);
        try
        {
            List<SoundFile> soundFiles = AddOnBuilder.LoadSoundFileJsonWithIpaHints(path);

            SoundFile plain = soundFiles.Single(f => f.DisplayName == "Plain");
            SoundFile taivan = soundFiles.Single(f => f.DisplayName == "Taivan");

            Assert.Null(plain.Pronunciations);
            Assert.Equal("incoming", plain.Text);
            Assert.Null(taivan.Ssml);
            Assert.Equal("Taivan incoming", taivan.Text);
            Assert.Equal([new Pronunciation("Taivan", "t1")], taivan.Pronunciations);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Build_AllowsSeveralEntries_ToShareOneRecordingTheyAgreeOn()
    {
        AddOn addOn = new AddOnBuilder(DefaultSettings, DefaultTtsSettings)
            .AddSoundFile(new SoundFile("adds.ogg", text: "Adds", displayName: "注意小怪"))
            .AddSoundFile(new SoundFile("adds.ogg", text: "Adds", displayName: "点名小怪"))
            .Build("/tmp/output");

        Assert.Equal(2, addOn.SoundFiles.Count());
    }

    [Fact]
    public void Build_Throws_WhenEntriesSharingARecordingDisagreeOnWhatItSays()
    {
        AddOnBuilder builder = new AddOnBuilder(DefaultSettings, DefaultTtsSettings)
            .AddSoundFile(new SoundFile("frontal.ogg", text: "FRONTAL", displayName: "注意头前"))
            .AddSoundFile(new SoundFile("frontal.ogg", text: "Frontal", displayName: "点名头前"));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => builder.Build("/tmp/output"));

        Assert.Contains("frontal.ogg", exception.Message);
        Assert.Contains("注意头前", exception.Message);
    }

    private static string WriteTempJson(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
