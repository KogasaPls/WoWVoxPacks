using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class BuildRecipeTests : IDisposable
{
    private readonly string _tempDirectory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

    private string RecipePath => Path.Combine(_tempDirectory, "Test_AddOn.recipe.json");

    [Fact]
    public async Task LoadAsync_ReturnsNull_WhenNoRecipeWasEverWritten()
    {
        Assert.Null(await BuildRecipe.LoadAsync(RecipePath));
    }

    [Fact]
    public async Task LoadAsync_Throws_WhenTheRecipeIsUnreadable()
    {
        // Absent means "built before recipes existed" and re-renders nothing. A recipe that is
        // there but unreadable must not borrow that answer for audio nothing can vouch for.
        await File.WriteAllTextAsync(RecipePath, "{ this is not json");

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildRecipe.LoadAsync(RecipePath));
    }

    [Fact]
    public async Task SaveAsync_WritesARecipe_LoadAsyncReadsBackUnchanged()
    {
        BuildRecipe recipe = BuildRecipe.From(new TtsSettings
        {
            Voice = VoiceName.Studio_Q,
            SpeakingRate = 1.1f,
            Pitch = -2f
        });

        await recipe.SaveAsync(RecipePath);

        Assert.Equal(recipe, await BuildRecipe.LoadAsync(RecipePath));
    }

    [Theory]
    [InlineData(VoiceName.Neural2_C, 1.2f, 0f, 44100)]
    [InlineData(VoiceName.Studio_Q, 1.0f, 0f, 44100)]
    [InlineData(VoiceName.Studio_Q, 1.2f, 1f, 44100)]
    [InlineData(VoiceName.Studio_Q, 1.2f, 0f, 24000)]
    public void From_DiffersFromTheBaseline_WhenAnyRenderingSettingDiffers(
        VoiceName voice, float speakingRate, float pitch, int sampleRateHertz)
    {
        BuildRecipe baseline = BuildRecipe.From(new TtsSettings
        {
            Voice = VoiceName.Studio_Q,
            SpeakingRate = 1.2f,
            Pitch = 0f,
            SampleRateHertz = 44100
        });

        BuildRecipe other = BuildRecipe.From(new TtsSettings
        {
            Voice = voice,
            SpeakingRate = speakingRate,
            Pitch = pitch,
            SampleRateHertz = sampleRateHertz
        });

        Assert.NotEqual(baseline, other);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }
}
