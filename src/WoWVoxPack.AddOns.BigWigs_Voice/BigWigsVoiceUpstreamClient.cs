using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public sealed class BigWigsVoiceUpstreamClient(
    HttpClient httpClient)
    : IBigWigsVoiceUpstreamClient, IDisposable
{
    private HttpClient HttpClient { get; } = httpClient;

    public async Task<IEnumerable<SpellListFile>> GetSpellListFiles(bool loadContent = true)
    {
        const string toolsUrl = "https://api.github.com/repos/BigWigsMods/BigWigs_Voice/contents/Tools";

        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("request");

        List<GitHubFile>? response = await HttpClient.GetFromJsonAsync<List<GitHubFile>>(toolsUrl);
        if (response == null)
        {
            throw new Exception("Failed to get spell list files from GitHub.");
        }

        List<SpellListFile> files = response.Where(file => file.Name.StartsWith("spells") && file.Name.EndsWith(".txt"))
            .Select(file => new SpellListFile(file.Name)).ToList();

        if (loadContent)
        {
            await Task.WhenAll(files.Select(file => _ = file.GetContentAsync()));
        }

        return files;
    }

    public void Dispose()
    {
        HttpClient.Dispose();
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
