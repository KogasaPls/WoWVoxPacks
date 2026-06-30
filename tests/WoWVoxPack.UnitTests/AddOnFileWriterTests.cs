using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class AddOnFileWriterTests : IDisposable
{
    private readonly string _tempDirectory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

    [Fact]
    public async Task WriteAllFilesAsync_WritesTocFileAndRegisteredFiles()
    {
        AddOnSettings settings = new()
        {
            Title = "Test_AddOn",
            Version = "12.0.7",
            Author = "Tester",
            Notes = "A test addon."
        };
        TtsSettings ttsSettings = new() { Voice = VoiceName.Neural2_C };

        AddOn addOn = new AddOnBuilder(settings, ttsSettings)
            .AddFile("Core.lua", _ => "-- generated lua")
            .Build(_tempDirectory);

        await AddOnFileWriter.WriteAllFilesAsync(addOn);

        string tocPath = Path.Combine(addOn.AddOnDirectory, addOn.TocFileName);
        string luaPath = Path.Combine(addOn.AddOnDirectory, "Core.lua");

        Assert.True(File.Exists(tocPath));
        Assert.Contains("## Title: Test_AddOn", await File.ReadAllTextAsync(tocPath));
        Assert.Equal("-- generated lua", await File.ReadAllTextAsync(luaPath));
    }

    [Fact]
    public async Task WriteAllFilesAsync_WritesOnlyTocFile_WhenFileContentsIsEmpty()
    {
        AddOnSettings settings = new()
        {
            Title = "Test_AddOn",
            Version = "12.0.7",
            Author = "Tester",
            Notes = "A test addon."
        };
        TtsSettings ttsSettings = new() { Voice = VoiceName.Neural2_C };

        AddOn addOn = new AddOnBuilder(settings, ttsSettings).Build(_tempDirectory);

        await AddOnFileWriter.WriteAllFilesAsync(addOn);

        string tocPath = Path.Combine(addOn.AddOnDirectory, addOn.TocFileName);

        Assert.True(File.Exists(tocPath));
        Assert.Single(Directory.GetFiles(addOn.AddOnDirectory));
    }

    [Fact]
    public async Task WriteAllFilesAsync_OverwritesExistingFile_OnRerun()
    {
        AddOnSettings settings = new()
        {
            Title = "Test_AddOn",
            Version = "12.0.7",
            Author = "Tester",
            Notes = "A test addon."
        };
        TtsSettings ttsSettings = new() { Voice = VoiceName.Neural2_C };

        AddOn firstAddOn = new AddOnBuilder(settings, ttsSettings)
            .AddFile("Core.lua", _ => "-- first content")
            .Build(_tempDirectory);
        await AddOnFileWriter.WriteAllFilesAsync(firstAddOn);

        AddOn secondAddOn = new AddOnBuilder(settings, ttsSettings)
            .AddFile("Core.lua", _ => "-- second content")
            .Build(_tempDirectory);
        await AddOnFileWriter.WriteAllFilesAsync(secondAddOn);

        string luaPath = Path.Combine(secondAddOn.AddOnDirectory, "Core.lua");

        Assert.Equal("-- second content", await File.ReadAllTextAsync(luaPath));
    }

    [Fact]
    public async Task WriteAllFilesAsync_WritesFilesBeforeAFailingFactory_RatherThanNone()
    {
        AddOnSettings settings = new()
        {
            Title = "Test_AddOn",
            Version = "12.0.7",
            Author = "Tester",
            Notes = "A test addon."
        };
        TtsSettings ttsSettings = new() { Voice = VoiceName.Neural2_C };

        AddOn addOn = new AddOnBuilder(settings, ttsSettings)
            .AddFile("First.lua", _ => "-- first content")
            .AddFile("Second.lua", _ => throw new InvalidOperationException("template failed"))
            .Build(_tempDirectory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => AddOnFileWriter.WriteAllFilesAsync(addOn));

        string firstPath = Path.Combine(addOn.AddOnDirectory, "First.lua");
        Assert.True(File.Exists(firstPath));
        Assert.Equal("-- first content", await File.ReadAllTextAsync(firstPath));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }
}
