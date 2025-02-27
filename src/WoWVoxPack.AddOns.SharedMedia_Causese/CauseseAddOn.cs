using System.Text.Json;

namespace WoWVoxPack.AddOns.SharedMedia_Causese;

public class CauseseAddOn : AddOn
{
    public CauseseAddOn(string outputDirectory, AddOnSettings settings) : base(outputDirectory, settings)
    {
        AddFile(@"libs\LibStub\LibStub.lua");
        AddFile(@"libs\CallbackHandler-1.0\CallbackHandler-1.0.lua");
        AddFile("embeds.xml");
        AddFile("Soundpaths.lua", addon =>
        {
            SoundpathsLuaFile sharedMediaCauseseLuaFile = new((CauseseAddOn)addon);
            return sharedMediaCauseseLuaFile.TransformText();
        });

        string file = Path.Combine(AppContext.BaseDirectory, "SharedMedia_Causese_Sounds.json");

        List<SoundFile>? soundFiles =
            JsonSerializer.Deserialize<List<SoundFile>>(File.ReadAllText(file),
                SoundFileJsonContext.Default.ListSoundFile);

        if (soundFiles is null)
        {
            throw new InvalidOperationException("Failed to deserialize sound files.");
        }

        AddSoundFiles(soundFiles);
    }

    protected override string AddOnDirectoryName => "SharedMedia_Causese";
    protected override string SoundDirectoryName => "sound";

    public string SoundPath => $@"Interface\\AddOns\\{AddOnDirectoryName}\\{SoundDirectoryName}";

    public override async Task WriteAllFilesAsync(CancellationToken cancellationToken = default)
    {
        await base.WriteAllFilesAsync(cancellationToken);

        CopyDirectory(Path.Combine(AppContext.BaseDirectory, "SharedMedia"), AddOnDirectory, true, true);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive, bool overwrite)
    {
        // Get information about the source directory
        DirectoryInfo dir = new(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        }

        // Cache directories before we start copying
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, overwrite);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true, overwrite);
            }
        }
    }
}
