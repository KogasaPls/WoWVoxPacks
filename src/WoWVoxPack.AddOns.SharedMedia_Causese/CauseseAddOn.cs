using System.Text.Json;

namespace WoWVoxPack.AddOns.SharedMedia_Causese;

public class CauseseAddOn : AddOn
{
    private readonly Lazy<List<SoundFile>> _soundFiles;

    public CauseseAddOn(AddOnSettings settings) : base(settings)
    {
        _soundFiles = new Lazy<List<SoundFile>>(() =>
        {
            string file = Path.Combine(AppContext.BaseDirectory, "SharedMedia_Causese_Sounds.json");
            return JsonSerializer.Deserialize<List<SoundFile>>(File.ReadAllText(file)) ?? [];
        });

        AddFile("Soundpaths.lua", addon =>
        {
            SoundpathsLuaFile sharedMediaCauseseLuaFile = new((CauseseAddOn)addon);
            return sharedMediaCauseseLuaFile.TransformText();
        });

        AddFile(@"libs\LibStub\LibStub.lua");
        AddFile(@"libs\CallbackHandler-1.0\CallbackHandler-1.0.lua");
        AddFile("embeds.xml");
    }

    public List<SoundFile> SoundFiles => _soundFiles.Value;

    public string SoundPath => $@"Interface\\AddOns\\{Title.Replace(" ", "_")}\\Sounds";

    public override async Task WriteAllFilesAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        await base.WriteAllFilesAsync(outputDirectory, cancellationToken);

        CopyDirectory(Path.Combine(AppContext.BaseDirectory, "SharedMedia"), outputDirectory, true, true);
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
