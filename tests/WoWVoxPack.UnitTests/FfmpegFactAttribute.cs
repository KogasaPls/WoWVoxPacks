namespace WoWVoxPack.UnitTests;

/// <summary>
/// A fact that needs the ffmpeg binary. Skipped on a machine without it, but never in CI, where
/// a skip would pass a build whose composed recordings cannot be made.
/// </summary>
public sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute()
    {
        if (!OnPath("ffmpeg") && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
        {
            Skip = "ffmpeg is not on PATH";
        }
    }

    private static bool OnPath(string executable) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Any(directory => File.Exists(Path.Combine(directory, executable))
                          || File.Exists(Path.Combine(directory, executable + ".exe")));
}
