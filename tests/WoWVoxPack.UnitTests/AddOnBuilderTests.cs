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
    public void LoadSoundFileJsonWithSsmlFallback_BackfillsSsml_OnlyForEntriesWithIpaEscape()
    {
        string path = WriteTempJson("""
            [
              {"FileName":"plain.ogg","DisplayName":"Plain","Text":"incoming"},
              {"FileName":"taivan.ogg","DisplayName":"Taivan","Text":"Taivan=t1 incoming"}
            ]
            """);
        try
        {
            List<SoundFile> soundFiles = AddOnBuilder.LoadSoundFileJsonWithSsmlFallback(path);

            SoundFile plain = soundFiles.Single(f => f.DisplayName == "Plain");
            SoundFile taivan = soundFiles.Single(f => f.DisplayName == "Taivan");

            Assert.Null(plain.Ssml);
            Assert.Equal(SoundFile.GetSsml("Taivan=t1 incoming"), taivan.Ssml);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempJson(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
