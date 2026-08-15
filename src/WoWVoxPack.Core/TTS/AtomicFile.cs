namespace WoWVoxPack.TTS;

/// <summary>
/// Writes that a build can be killed in the middle of without leaving a file that reads as
/// something else. The manifest and the recipe are both read back on the next run and both decide
/// what gets re-rendered, and an unreadable recipe now fails the build rather than being ignored,
/// so a half-written one would need a human to clear it.
/// </summary>
internal static class AtomicFile
{
    public static async Task WriteAllTextAsync(string path, string contents,
        CancellationToken cancellationToken = default)
    {
        string pending = Path.Combine(
            Path.GetDirectoryName(path) ?? ".",
            $".wvp-{Path.GetFileName(path)}-{Guid.NewGuid():N}");

        try
        {
            await File.WriteAllTextAsync(pending, contents, cancellationToken);
            File.Move(pending, path, true);
        }
        finally
        {
            if (File.Exists(pending))
            {
                File.Delete(pending);
            }
        }
    }
}
