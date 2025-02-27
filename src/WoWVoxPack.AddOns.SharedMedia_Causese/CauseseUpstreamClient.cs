using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json.Serialization;

using Octokit;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.SharedMedia_Causese;

public sealed class CauseseUpstreamClient(
    HttpClient httpClient)
    : ICauseseUpstreamClient, IDisposable
{
    private HttpClient HttpClient { get; } = httpClient;

    public async Task<IEnumerable<SoundFile>> GetSoundFilesAsync(
        CancellationToken cancellationToken = default)
    {
        GitHubClient githubClient = new(new ProductHeaderValue("KogasaPls_WoWVoxPack"));
        Release? release = await githubClient.Repository.Release.GetLatest("curseforge-mirror", "SharedMedia_Causese");

        if (release is null)
        {
            throw new Exception("Failed to get latest release from GitHub.");
        }

        ReleaseAsset? asset = release.Assets.Single(asset => asset.Name == "SharedMedia_Causese.zip");

        await using Stream assetStream = await HttpClient.GetStreamAsync(asset.BrowserDownloadUrl, cancellationToken);
        using ZipArchive archive = new(assetStream, ZipArchiveMode.Read);

        ConcurrentBag<SoundFile> soundFiles = new();

        foreach (ZipArchiveEntry entry in archive.Entries.Where(entry =>
                     entry.FullName.StartsWith("SharedMedia_Causese/sound/") && entry.Name.EndsWith(".ogg")))
        {
            string baseName;
            SoundFile soundFile;

            if (entry.Name.Equals("BITE.ogg", StringComparison.OrdinalIgnoreCase))
            {
                string tmpPath = Path.GetTempFileName();
                await using Stream entryStream = entry.Open();
                await using FileStream fileStream = File.OpenWrite(tmpPath);
                await entryStream.CopyToAsync(fileStream, cancellationToken);

                baseName = Path.GetFileNameWithoutExtension(entry.Name);
                soundFile = new SoundFile(entry.Name,
                    formattedDisplayName: $"|cFFFF0000{baseName}|r") { CopyFromPath = tmpPath };
            }
            else
            {
                baseName = Path.GetFileNameWithoutExtension(entry.Name);
                soundFile = new SoundFile(entry.Name, baseName, displayName: baseName,
                    formattedDisplayName: $"|cFFFF0000{baseName}|r");
            }

            soundFiles.Add(soundFile);
        }

        return soundFiles;
    }

    public void Dispose()
    {
        HttpClient.Dispose();
    }


    private class GitHubFile
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("sha")]
        public string Sha { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }

        [JsonPropertyName("git_url")]
        public string GitUrl { get; set; }

        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }
}
