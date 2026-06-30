namespace WoWVoxPack.UnitTests;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

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

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }
}
