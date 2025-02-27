using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public sealed class BigWigsVoiceUpstreamClient(
    HttpClient httpClient)
    : IBigWigsVoiceUpstreamClient, IDisposable
{
    private HttpClient HttpClient { get; } = httpClient;

    public async Task<IEnumerable<BigWigsVoiceSoundFile>> GetSoundFilesAsync(
        CancellationToken cancellationToken = default)
    {
        var soundFiles = new ConcurrentBag<BigWigsVoiceSoundFile>();

        await foreach (var spellListFile in GetSpellListFilesAsync(cancellationToken: cancellationToken))
        {
            foreach (var soundFile in spellListFile.SoundFiles)
            {
                soundFiles.Add(soundFile);
            }
        }

        return soundFiles;
    }

    public void Dispose()
    {
        HttpClient.Dispose();
    }

    private async Task<SpellListFile> GetSpellListFileAsync(string fileName,
        CancellationToken cancellationToken = default)
    {
        const string baseUrl = "https://github.com/BigWigsMods/BigWigs_Voice/raw/master/Tools/";

        var fullUrl = Path.Combine(baseUrl, fileName);
        var content = await HttpClient.GetStringAsync(fullUrl, cancellationToken);

        return new SpellListFile(fileName, content);
    }


    private async IAsyncEnumerable<SpellListFile> GetSpellListFilesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string toolsUrl = "https://api.github.com/repos/BigWigsMods/BigWigs_Voice/contents/Tools";

        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("request");

        List<GitHubFile>? response =
            await HttpClient.GetFromJsonAsync<List<GitHubFile>>(toolsUrl, cancellationToken: cancellationToken);
        if (response == null)
        {
            throw new Exception("Failed to get spell list files from GitHub.");
        }

        foreach (var file in response.Where(file => file.Name.StartsWith("spells") && file.Name.EndsWith(".txt")))
        {
            yield return await GetSpellListFileAsync(file.Name, cancellationToken);
        }
    }

    private class GitHubFile
    {
        [JsonPropertyName("name")] public string Name { get; set; }

        [JsonPropertyName("path")] public string Path { get; set; }

        [JsonPropertyName("sha")] public string Sha { get; set; }

        [JsonPropertyName("size")] public int Size { get; set; }

        [JsonPropertyName("url")] public string Url { get; set; }

        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; }

        [JsonPropertyName("git_url")] public string GitUrl { get; set; }

        [JsonPropertyName("download_url")] public string DownloadUrl { get; set; }

        [JsonPropertyName("type")] public string Type { get; set; }
    }
}
