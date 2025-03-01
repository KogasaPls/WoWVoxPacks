using System.Reflection;

using Ardalis.GuardClauses;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.Builder;

public class Worker : IHostedService
{
    public Worker(ILogger<Worker> logger, IEnumerable<IAddOnService> addOnServices, IOptions<BuildMatrix> buildMatrix,
        IHostApplicationLifetime applicationLifetime, ISoundFileService soundFileService)
    {
        Logger = logger;
        AddOnServices = addOnServices.ToList();
        BuildMatrix = buildMatrix.Value;
        ApplicationLifetime = applicationLifetime;
        SoundFileService = soundFileService;

        string solutionFile =
            Assembly.GetExecutingAssembly().GetCustomAttribute<SolutionFileAttribute>()?.SolutionFile ??
            throw new Exception("Solution file not found.");
        OutputDirectoryBase = Path.Combine(
            Path.GetDirectoryName(solutionFile) ?? throw new Exception("Solution file not found."),
            "output");
    }

    private ILogger<Worker> Logger { get; }
    private List<IAddOnService> AddOnServices { get; }
    private BuildMatrix BuildMatrix { get; }
    private IHostApplicationLifetime ApplicationLifetime { get; }
    private ISoundFileService SoundFileService { get; }

    private string OutputDirectoryBase { get; }

    private IEnumerable<(IAddOnService addOnService, TtsSettings ttsSettings)> Matrix =>
        from addOnService in AddOnServices
        from ttsSettings in BuildMatrix.TtsSettings
        select (addOnService, ttsSettings);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach ((IAddOnService addOnService, TtsSettings ttsSettings) in Matrix)
        {
            string? outputDirectory =
                Path.Combine(OutputDirectoryBase, Guard.Against.Null(ttsSettings.Voice).ToString());
            AddOn addOn = await addOnService.BuildAddOnAsync(outputDirectory, ttsSettings, cancellationToken);

            Logger.LogInformation("Building {AddOnName} addon in directory {OutputDirectory}", addOn.Title,
                addOn.AddOnDirectory);

            await addOn.WriteAllFilesAsync(cancellationToken);

            string soundOutputDirectory = addOn.SoundDirectory;
            Directory.CreateDirectory(soundOutputDirectory);

            IEnumerable<Task>? tasks = addOn.SoundFiles.Select(soundFile =>
                SoundFileService.CreateSoundFileIfNotExistsAsync(soundFile, soundOutputDirectory, ttsSettings,
                    cancellationToken));

            await Task.WhenAll(tasks.ToArray());

            Logger.LogInformation("Finished building addon: {AddOnName}", addOn.Title);
        }

        Logger.LogInformation("Finished building add-ons, stopping");
        ApplicationLifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
