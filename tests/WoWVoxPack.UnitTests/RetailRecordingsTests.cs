using WoWVoxPack.AddOns;
using WoWVoxPack.Core.Builder;
using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class RetailRecordingsTests : IDisposable
{
    private readonly string _tempDirectory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

    [Fact]
    public void CopyMatching_CopiesContentEqualFile()
    {
        AddOn retail = BuildAddOn("output", new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"));
        AddOn forever = BuildAddOn("output-forever", new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"));
        WriteRecording(retail, "alert.ogg", [1, 2, 3]);

        IReadOnlyList<string> copied = RetailRecordings.CopyMatching(retail, forever);

        Assert.Equal(["alert.ogg"], copied);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(forever.SoundDirectory, "alert.ogg")));
    }

    [Fact]
    public void CopyMatching_OverwritesStaleTargetFile()
    {
        AddOn retail = BuildAddOn("output", new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"));
        AddOn forever = BuildAddOn("output-forever", new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"));
        WriteRecording(retail, "alert.ogg", [1, 2, 3]);
        WriteRecording(forever, "alert.ogg", [9, 9]);

        RetailRecordings.CopyMatching(retail, forever);

        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(forever.SoundDirectory, "alert.ogg")));
    }

    [Fact]
    public void CopyMatching_SkipsContentMismatch()
    {
        AddOn retail = BuildAddOn("output", new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"));
        AddOn forever = BuildAddOn("output-forever",
            new SoundFile("alert.ogg", text: "Alert, reworded", displayName: "Alert"));
        WriteRecording(retail, "alert.ogg", [1, 2, 3]);

        IReadOnlyList<string> copied = RetailRecordings.CopyMatching(retail, forever);

        Assert.Empty(copied);
        Assert.False(File.Exists(Path.Combine(forever.SoundDirectory, "alert.ogg")));
    }

    [Fact]
    public void CopyMatching_SkipsFileMissingFromRetailDisk()
    {
        AddOn retail = BuildAddOn("output", new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"));
        AddOn forever = BuildAddOn("output-forever", new SoundFile("alert.ogg", text: "Alert", displayName: "Alert"));

        IReadOnlyList<string> copied = RetailRecordings.CopyMatching(retail, forever);

        Assert.Empty(copied);
        Assert.False(File.Exists(Path.Combine(forever.SoundDirectory, "alert.ogg")));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }

    private static void WriteRecording(AddOn addOn, string fileName, byte[] bytes)
    {
        Directory.CreateDirectory(addOn.SoundDirectory);
        File.WriteAllBytes(Path.Combine(addOn.SoundDirectory, fileName), bytes);
    }

    private AddOn BuildAddOn(string outputDirectoryName, SoundFile soundFile)
    {
        AddOnSettings settings = new()
        {
            Title = "Test_AddOn",
            Version = "12.0.7",
            Author = "Tester",
            Notes = "A test addon."
        };

        return new AddOnBuilder(settings, new TtsSettings { Voice = VoiceName.Neural2_C })
            .AddSoundFile(soundFile)
            .Build(Path.Combine(_tempDirectory, outputDirectoryName, "Neural2_C"));
    }
}
