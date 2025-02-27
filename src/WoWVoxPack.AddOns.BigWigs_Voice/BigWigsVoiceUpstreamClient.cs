using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Octokit;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public sealed class BigWigsVoiceUpstreamClient(
    HttpClient httpClient)
    : IBigWigsVoiceUpstreamClient, IDisposable
{
    private HttpClient HttpClient { get; } = httpClient;

    public async Task<IEnumerable<BigWigsVoiceSoundFile>> GetSoundFilesAsync(
        CancellationToken cancellationToken = default)
    {
        ConcurrentBag<BigWigsVoiceSoundFile> soundFiles = [];

        await foreach (SpellListFile spellListFile in GetSpellListFilesAsync(cancellationToken))
        {
            foreach (BigWigsVoiceSoundFile soundFile in spellListFile.SoundFiles)
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

    private async IAsyncEnumerable<SpellListFile> GetSpellListFilesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        GitHubClient github = new(new ProductHeaderValue("KogasaPls_WoWVoxPack"));

        IReadOnlyList<RepositoryContent>? toolDirectoryContent =
            await github.Repository.Content.GetAllContents("BigWigsMods", "BigWigs_Voice", "Tools/");
        if (toolDirectoryContent is null)
        {
            throw new Exception("Failed to get directory content from GitHub.");
        }

        IEnumerable<RepositoryContent> spellFiles = toolDirectoryContent.Where(content =>
            content.Name.StartsWith("spells") && content.Name.EndsWith(".txt"));
        foreach (RepositoryContent spellFile in spellFiles)
        {
            string content = await HttpClient.GetStringAsync(spellFile.DownloadUrl, cancellationToken);
            SpellListFile spellListFile = new(spellFile.Name, content);

            yield return spellListFile;
        }
    }
}
