using System.Text.Json;

using Ardalis.GuardClauses;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Causese;

public class CauseseAddOn : AddOn
{
    public CauseseAddOn(string outputDirectory, AddOnSettings settings, IEnumerable<SoundFile> soundFiles) : base(
        outputDirectory, settings)
    {
        soundFiles = soundFiles.ToArray();

        AddFile(@"libs\LibStub\LibStub.lua");
        AddFile(@"libs\CallbackHandler-1.0\CallbackHandler-1.0.lua");
        AddFile("embeds.xml");
        AddFile("Soundpaths.lua", addon =>
        {
            SoundpathsLuaFile sharedMediaCauseseLuaFile = new((CauseseAddOn)addon);
            return sharedMediaCauseseLuaFile.TransformText();
        });

        string file = Path.Combine(AppContext.BaseDirectory, "SharedMedia_Causese_Sounds.json");

        List<SoundFile> manualSoundFiles =
            Guard.Against.Null(JsonSerializer.Deserialize<List<SoundFile>>(File.ReadAllText(file),
                SoundFileJsonContext.Default.ListSoundFile));

        AddSoundFiles(manualSoundFiles);

        List<SoundFile> newSoundFiles = soundFiles
            .ExceptBy(manualSoundFiles.Select(s => s.FileName), s => s.FileName).ToList();
        AddSoundFiles(newSoundFiles);
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
