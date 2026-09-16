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
        string tocFilePath = Path.Combine(addOn.AddOnDirectory, addOn.TocFileName);
        await File.WriteAllTextAsync(tocFilePath, AddOnTocFile.Render(addOn), cancellationToken);
    }

    private static async Task WriteAddOnFilesAsync(AddOn addOn, CancellationToken cancellationToken)
    {
        IEnumerable<Task> writeTasks = addOn.Files.Select(fileName =>
        {
            string content = addOn.GetFileContent(fileName);
            string path = Path.Combine(addOn.AddOnDirectory, fileName);
            return File.WriteAllTextAsync(path, content, cancellationToken);
        });

        await Task.WhenAll(writeTasks).ConfigureAwait(false);
    }
}
