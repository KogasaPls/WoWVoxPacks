using Ardalis.GuardClauses;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.Core.Builder;

public class AddOnBuildOrchestrator(
    ILogger<AddOnBuildOrchestrator> logger,
    IEnumerable<IAddOnService> addOnServices,
    IOptions<BuildMatrix> buildMatrix,
    ISoundFileService soundFileService,
    string outputDirectoryBase)
{
    // The TTS client's own token bucket is the real throttle; this keeps thousands of tasks from
    // piling up behind it, each holding a request body and a response buffer.
    private const int MaxConcurrentRenders = 32;
    private const int RenderAttempts = 3;

    private ILogger<AddOnBuildOrchestrator> Logger { get; } = logger;
    private List<IAddOnService> AddOnServices { get; } = addOnServices.ToList();
    private BuildMatrix BuildMatrix { get; } = buildMatrix.Value;
    private ISoundFileService SoundFileService { get; } = soundFileService;
    private string OutputDirectoryBase { get; } = outputDirectoryBase;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        foreach ((IAddOnService addOnService, TtsSettings ttsSettings, string outputDirectory) in Matrix())
        {
            AddOn addOn = await addOnService.BuildAddOnAsync(outputDirectory, ttsSettings, cancellationToken);

            Logger.LogInformation("Building {AddOnName} addon in directory {OutputDirectory}", addOn.Title,
                addOn.AddOnDirectory);

            string soundOutputDirectory = addOn.SoundDirectory;

            if (!BuildMatrix.DryRun)
            {
                await AddOnFileWriter.WriteAllFilesAsync(addOn, cancellationToken);
                Directory.CreateDirectory(soundOutputDirectory);
            }

            SoundFileManifest manifest =
                await SoundFileManifest.LoadAsync(addOn.SoundFilesJsonPath, cancellationToken);

            BuildRecipe recipe = BuildRecipe.From(ttsSettings);
            BuildRecipe? previousRecipe = await BuildRecipe.LoadAsync(addOn.BuildRecipePath, cancellationToken);
            bool recipeChanged = previousRecipe is not null && previousRecipe != recipe;
            if (recipeChanged)
            {
                Logger.LogWarning(
                    "{AddOnName} was rendered as {PreviousRecipe} and is now {Recipe}; re-rendering every sound",
                    addOn.Title, previousRecipe, recipe);
            }

            SoundFile[] soundFilesToCreate =
                manifest.FilesToCreate(addOn.SoundFiles, soundOutputDirectory, recipeChanged,
                    BuildMatrix.AllowFullRerender).ToArray();

            // Asked before anything is rendered: a pack that lost most of its vocabulary is a
            // failed build, and finding that out after paying for thousands of files is worse.
            string[] soundFilesToRemove = manifest.FilesToRemove(addOn.SoundFiles).ToArray();

            if (BuildMatrix.DryRun)
            {
                ReportPlan(addOn, soundFilesToCreate, soundFilesToRemove);
                continue;
            }

            await CreateSoundFilesAsync(soundFilesToCreate, soundOutputDirectory, ttsSettings, cancellationToken);

            RemoveRetiredSoundFiles(soundFilesToRemove, addOn, soundOutputDirectory);

            await manifest.SaveAsync(addOn.SoundFilesJsonPath, addOn.SoundFiles, cancellationToken);
            await recipe.SaveAsync(addOn.BuildRecipePath, cancellationToken);

            Logger.LogInformation("Finished building addon: {AddOnName}", addOn.Title);
        }

        Logger.LogInformation("Finished building add-ons");
    }

    /// <summary>
    /// What a real run would spend, before it spends it. Two runs in a row have re-rendered a
    /// whole pack because a field the manifest compares changed shape, so the plan is worth
    /// reading rather than predicting.
    /// </summary>
    private void ReportPlan(AddOn addOn, IReadOnlyCollection<SoundFile> toCreate, IReadOnlyCollection<string> toRemove)
    {
        Logger.LogInformation("[dry run] {AddOnName}: {CreateCount} to render, {RemoveCount} to remove",
            addOn.Title, toCreate.Count, toRemove.Count);

        foreach (SoundFile soundFile in toCreate)
        {
            Logger.LogInformation("[dry run]   render {FileName} {Spoken}", soundFile.FileName,
                soundFile.Ssml ?? soundFile.Text);
        }

        foreach (string fileName in toRemove)
        {
            Logger.LogInformation("[dry run]   remove {FileName}", fileName);
        }
    }

    private IEnumerable<(IAddOnService AddOnService, TtsSettings TtsSettings, string OutputDirectory)> Matrix()
    {
        foreach (IAddOnService addOnService in AddOnServices)
        {
            foreach (TtsSettings ttsSettings in BuildMatrix.TtsSettings)
            {
                string outputDirectory =
                    Path.Combine(OutputDirectoryBase, Guard.Against.Null(ttsSettings.Voice).ToString());
                yield return (addOnService, ttsSettings, outputDirectory);
            }
        }
    }

    /// <summary>
    /// A run renders thousands of files against a paid API. Handing every one of them to the
    /// thread pool at once only queues them behind the client's rate limiter, and the first
    /// transient failure abandons the rest, so the work is bounded and each file is given a
    /// second and third chance before it takes the build down with it.
    /// </summary>
    private Task CreateSoundFilesAsync(IReadOnlyCollection<SoundFile> soundFiles, string soundOutputDirectory,
        TtsSettings ttsSettings, CancellationToken cancellationToken)
    {
        if (soundFiles.Count == 0)
        {
            return Task.CompletedTask;
        }

        Logger.LogInformation("Rendering {Count} sound files into {OutputDirectory}", soundFiles.Count,
            soundOutputDirectory);

        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = MaxConcurrentRenders,
            CancellationToken = cancellationToken
        };

        return Parallel.ForEachAsync(soundFiles, options,
            (soundFile, token) => CreateSoundFileWithRetryAsync(soundFile, soundOutputDirectory, ttsSettings, token));
    }

    private async ValueTask CreateSoundFileWithRetryAsync(SoundFile soundFile, string soundOutputDirectory,
        TtsSettings ttsSettings, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await SoundFileService.CreateSoundFileAsync(soundFile, soundOutputDirectory, ttsSettings,
                    cancellationToken);
                return;
            }

            // A client-side deadline arrives as TaskCanceledException, and a timed-out render is
            // the most ordinary thing there is to retry. What ends the attempts is the token:
            // the caller giving up, or a sibling exhausting its own, which cancels the loop and
            // stops the rest paying into a build that is already lost.
            catch (Exception exception) when (attempt < RenderAttempts && !cancellationToken.IsCancellationRequested)
            {
                TimeSpan delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                Logger.LogWarning(exception, "Rendering {FileName} failed (attempt {Attempt}); retrying in {Delay}",
                    soundFile.FileName, attempt, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private void RemoveRetiredSoundFiles(IEnumerable<string> fileNames, AddOn addOn, string soundOutputDirectory)
    {
        foreach (string fileName in fileNames)
        {
            string path = Path.Combine(soundOutputDirectory, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            Logger.LogInformation("Removing {FileName}: {AddOnName} no longer registers it", fileName, addOn.Title);
            File.Delete(path);
        }
    }
}
