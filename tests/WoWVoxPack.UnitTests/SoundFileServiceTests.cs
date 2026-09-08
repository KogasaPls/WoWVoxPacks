using System.Globalization;
using System.Text;

using FFMpegCore;

using Microsoft.Extensions.Logging.Abstractions;

using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class SoundFileServiceTests : IDisposable
{
    private readonly string _tempDirectory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

    private static TtsSettings Settings => new() { Voice = VoiceName.Neural2_C };

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

    /// <summary>
    /// The sources carry a tenth of a second of leading silence, as synthesised speech does, so
    /// the onsets prove that the silence is trimmed and the speech lands where the composition
    /// says, not merely that the files were joined. "One" outlasts the 1.55 s left after its
    /// onset, so the total length also proves a long source is cut rather than extending the clip.
    /// </summary>
    [FfmpegFact]
    public async Task CreateSoundFileAsync_ComposesTheClip_FromItsSourcesAtTheirOnsets()
    {
        await WriteTone(Path.Combine(_tempDirectory, "three.ogg"), seconds: 0.3);
        await WriteTone(Path.Combine(_tempDirectory, "two.ogg"), seconds: 0.3);
        await WriteTone(Path.Combine(_tempDirectory, "one.ogg"), seconds: 2.0);

        SoundFileService service = new(new EchoTtsProvider(), NullLogger<SoundFileService>.Instance);
        SoundFile countdown = new("5seconds321.ogg", displayName: "5seconds321",
            composition: new SoundComposition(5.7,
            [
                new SoundCompositionPart("three.ogg", 2.15),
                new SoundCompositionPart("two.ogg", 3.15),
                new SoundCompositionPart("one.ogg", 4.15)
            ]));

        await service.CreateSoundFileAsync(countdown, _tempDirectory, Settings);

        (double length, double[] onsets) = await Onsets(Path.Combine(_tempDirectory, "5seconds321.ogg"));
        Assert.Equal(5.7, length, 0.02);
        Assert.Equal(3, onsets.Length);
        Assert.Equal(2.15, onsets[0], 0.03);
        Assert.Equal(3.15, onsets[1], 0.03);
        Assert.Equal(4.15, onsets[2], 0.03);
        Assert.Empty(Directory.GetFiles(_tempDirectory, ".wvp-*"));
    }

    [Fact]
    public async Task CreateSoundFileAsync_RefusesToCompose_FromASourceThatWasNeverRendered()
    {
        SoundFileService service = new(new EchoTtsProvider(), NullLogger<SoundFileService>.Instance);
        SoundFile countdown = new("countdown.ogg", displayName: "Countdown",
            composition: new SoundComposition(2, [new SoundCompositionPart("missing.ogg", 1)]));

        FileNotFoundException refusal = await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.CreateSoundFileAsync(countdown, _tempDirectory, Settings));

        Assert.Contains("missing.ogg", refusal.Message);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }

    private static Task WriteTone(string path, double seconds) =>
        FFMpegArguments
            .FromFileInput(
                $"sine=frequency=440:sample_rate=44100:duration={seconds.ToString("0.0", CultureInfo.InvariantCulture)}",
                false,
                options => options.ForceFormat("lavfi"))
            .OutputToFile(path, true, options =>
            {
                options.WithCustomArgument("-af adelay=100:all=1");
                options.WithAudioCodec("libvorbis");
            })
            .ProcessAsynchronously();

    /// <summary>Length in seconds, and the second each stretch of sound begins after silence.</summary>
    private async Task<(double Length, double[] Onsets)> Onsets(string path)
    {
        const int sampleRate = 16000;
        string pcm = Path.Combine(_tempDirectory, "decoded.pcm");
        await FFMpegArguments.FromFileInput(path)
            .OutputToFile(pcm, true, options =>
            {
                options.ForceFormat("s16le");
                options.WithCustomArgument($"-ac 1 -ar {sampleRate}");
            })
            .ProcessAsynchronously();

        byte[] bytes = await File.ReadAllBytesAsync(pcm);
        List<double> onsets = [];
        bool loud = false;
        int quietSince = 0;
        for (int sample = 0; sample < bytes.Length / 2; sample++)
        {
            bool above = Math.Abs(BitConverter.ToInt16(bytes, sample * 2)) > short.MaxValue * 0.05;
            if (above && !loud && sample - quietSince > sampleRate / 10)
            {
                onsets.Add(sample / (double)sampleRate);
            }

            if (!above && loud)
            {
                quietSince = sample;
            }

            loud = above;
        }

        return (bytes.Length / 2.0 / sampleRate, onsets.ToArray());
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
