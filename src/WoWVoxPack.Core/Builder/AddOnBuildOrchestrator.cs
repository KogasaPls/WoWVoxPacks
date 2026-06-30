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
    private ILogger<AddOnBuildOrchestrator> Logger { get; } = logger;
    private List<IAddOnService> AddOnServices { get; } = addOnServices.ToList();
    private BuildMatrix BuildMatrix { get; } = buildMatrix.Value;
    private ISoundFileService SoundFileService { get; } = soundFileService;
    private string OutputDirectoryBase { get; } = outputDirectoryBase;

    private IEnumerable<(IAddOnService addOnService, TtsSettings ttsSettings)> Matrix =>
        from addOnService in AddOnServices
        from ttsSettings in BuildMatrix.TtsSettings
        select (addOnService, ttsSettings);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        foreach ((IAddOnService addOnService, TtsSettings ttsSettings) in Matrix)
        {
            string outputDirectory =
                Path.Combine(OutputDirectoryBase, Guard.Against.Null(ttsSettings.Voice).ToString());
            AddOn addOn = await addOnService.BuildAddOnAsync(outputDirectory, ttsSettings, cancellationToken);

            Logger.LogInformation("Building {AddOnName} addon in directory {OutputDirectory}", addOn.Title,
                addOn.AddOnDirectory);

            await AddOnFileWriter.WriteAllFilesAsync(addOn, cancellationToken);

            string soundOutputDirectory = addOn.SoundDirectory;
            Directory.CreateDirectory(soundOutputDirectory);

            SoundFileManifest manifest =
                await SoundFileManifest.LoadAsync(addOn.SoundFilesJsonPath, cancellationToken);
            SoundFile[] soundFilesToCreate =
                manifest.FilesToCreate(addOn.SoundFiles, soundOutputDirectory).ToArray();

            Task[] createSoundFileTasks = soundFilesToCreate.Select(
                soundFile =>
                    SoundFileService.CreateSoundFileAsync(soundFile, soundOutputDirectory, ttsSettings,
                        cancellationToken)).ToArray();

            await Task.WhenAll(createSoundFileTasks);
            await manifest.SaveAsync(addOn.SoundFilesJsonPath, addOn.SoundFiles, cancellationToken);

            Logger.LogInformation("Finished building addon: {AddOnName}", addOn.Title);
        }

        Logger.LogInformation("Finished building add-ons");
    }
}
