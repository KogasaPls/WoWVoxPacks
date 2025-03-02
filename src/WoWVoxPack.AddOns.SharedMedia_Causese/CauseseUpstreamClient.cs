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


        ZipArchiveEntry? soundpathsLuaFile = archive.GetEntry("SharedMedia_Causese/Soundpaths.lua");
        if (soundpathsLuaFile is null)
        {
            throw new Exception("Failed to get Soundpaths.lua from SharedMedia_Causese.zip");
        }

        await using Stream soundpathsLuaStream = soundpathsLuaFile.Open();
        using StreamReader reader = new(soundpathsLuaStream);
        string soundpathsLua = await reader.ReadToEndAsync(cancellationToken);
        ParsedSoundpathsLuaFile parsedSoundpathsLuaFile = new(soundpathsLua);

        IEnumerable<SoundFile> soundFiles =
            await parsedSoundpathsLuaFile.GetSoundFilesAsync(archive, cancellationToken);

        return soundFiles;
    }

    public void Dispose()
    {
        HttpClient.Dispose();
    }
}
