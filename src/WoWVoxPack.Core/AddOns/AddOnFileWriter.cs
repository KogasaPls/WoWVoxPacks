namespace WoWVoxPack.AddOns;

public static class AddOnFileWriter
{
    public static async Task WriteAllFilesAsync(AddOn addOn, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(addOn.AddOnDirectory);

        await WriteTocFileAsync(addOn, cancellationToken).ConfigureAwait(false);
        await WriteAddOnFilesAsync(addOn, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteTocFileAsync(AddOn addOn, CancellationToken cancellationToken)
    {
        AddOnTocFile tocFile = new(addOn);
        string tocFilePath = Path.Combine(addOn.AddOnDirectory, addOn.TocFileName);
        await File.WriteAllTextAsync(tocFilePath, (string?)tocFile.TransformText(), cancellationToken);
    }

    private static async Task WriteAddOnFilesAsync(AddOn addOn, CancellationToken cancellationToken)
    {
        foreach ((string fileName, string content) in addOn.FileContents)
        {
            string path = Path.Combine(addOn.AddOnDirectory, fileName);
            await File.WriteAllTextAsync(path, content, cancellationToken);
        }
    }
}
