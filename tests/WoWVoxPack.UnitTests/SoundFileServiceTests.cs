using Microsoft.Extensions.Logging.Abstractions;

using System.Text;

using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class SoundFileServiceTests : IDisposable
{
    private readonly string _tempDirectory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

    [Fact]
    public async Task CreateSoundFileAsync_WritesTheAudioItWasGiven()
    {
        SoundFileService service = new(new EchoTtsProvider(), NullLogger<SoundFileService>.Instance);
        SoundFile soundFile = new("alert.ogg", text: "Alert", displayName: "Alert");

        await service.CreateSoundFileAsync(soundFile, _tempDirectory, Settings);

        Assert.Equal("Alert", await File.ReadAllTextAsync(Path.Combine(_tempDirectory, "alert.ogg")));
    }

    /// <summary>
    /// A render works through a temporary file, and a build runs dozens at once. Sharing one
    /// temporary path between them puts one sound's audio in another sound's file, which the
    /// manifest then records as current, so no later build re-renders it: a pack that says the
    /// wrong words with nothing to notice. A single render cannot show this.
    /// </summary>
    [Fact]
    public async Task CreateSoundFileAsync_GivesEachSoundItsOwnAudio_WhenManyRenderAtOnce()
    {
        SoundFileService service = new(new EchoTtsProvider(), NullLogger<SoundFileService>.Instance);
        SoundFile[] soundFiles = Enumerable.Range(0, 200)
            .Select(i => new SoundFile($"{i}.ogg", text: $"Spell {i}", displayName: $"Spell {i}"))
            .ToArray();

        await Parallel.ForEachAsync(soundFiles, new ParallelOptions { MaxDegreeOfParallelism = 32 },
            (soundFile, token) => new ValueTask(
                service.CreateSoundFileAsync(soundFile, _tempDirectory, Settings, token)));

        foreach (SoundFile soundFile in soundFiles)
        {
            string path = Path.Combine(_tempDirectory, soundFile.FileName);
            Assert.True(File.Exists(path), $"{soundFile.FileName} was never written");
            Assert.Equal(soundFile.Text, await File.ReadAllTextAsync(path));
        }

        Assert.Empty(Directory.GetFiles(_tempDirectory, ".wvp-*"));
    }

    private static TtsSettings Settings => new() { Voice = VoiceName.Neural2_C };

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }

    /// <summary>Answers with the words themselves, so a file can be checked against what asked for it.</summary>
    private sealed class EchoTtsProvider : ITtsProvider
    {
        public async Task<TtsResponse> GetAudioContentAsync(SoundFile soundFile, TtsSettings settings,
            CancellationToken cancellationToken = default)
        {
            // Enough of a gap for the next render to overwrite a shared temporary file.
            await Task.Delay(1, cancellationToken);

            return new TtsResponse(Encoding.UTF8.GetBytes(soundFile.Text ?? string.Empty), AudioFormat.OggOpus);
        }
    }
}
