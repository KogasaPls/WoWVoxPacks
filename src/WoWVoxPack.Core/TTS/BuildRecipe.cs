using System.Text.Json;
using System.Text.Json.Serialization;

using Ardalis.GuardClauses;

namespace WoWVoxPack.TTS;

/// <summary>
/// How an addon's audio was rendered, as opposed to what was said. The manifest compares text,
/// SSML and file name, so nothing else about a recording is part of its identity: change a
/// voice's speaking rate, pitch or sample rate and every existing file counts as up to date,
/// leaving a pack that is half one recipe and half another with no way to tell.
/// </summary>
public sealed record BuildRecipe(
    string Voice,
    string LanguageCode,
    float SpeakingRate,
    float Pitch,
    int SampleRateHertz)
{
    public static BuildRecipe From(TtsSettings settings)
    {
        return new BuildRecipe(
            Guard.Against.Null(settings.Voice).ToString(),
            settings.LanguageCode,
            settings.SpeakingRate,
            settings.Pitch,
            settings.SampleRateHertz);
    }

    /// <summary>
    /// A pack built before recipes were recorded has none, and its audio is by definition the
    /// current recipe's: re-rendering thousands of paid files on the strength of a missing file
    /// is the wrong guess. Only a recipe that is present and different means the audio is stale.
    /// </summary>
    public static async Task<BuildRecipe?> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken);

        // A recipe that is present but unreadable is not the same as one that was never written.
        // Reading it as "no recipe" would answer "nothing to re-render" for a pack whose audio
        // nothing can vouch for.
        try
        {
            return JsonSerializer.Deserialize(json, BuildRecipeJsonContext.Default.BuildRecipe) ??
                   throw new InvalidOperationException($"{path} holds no recipe.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"{path} could not be read.", exception);
        }
    }

    public Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        string json = JsonSerializer.Serialize(this, BuildRecipeJsonContext.Default.BuildRecipe);
        return AtomicFile.WriteAllTextAsync(path, json, cancellationToken);
    }
}
