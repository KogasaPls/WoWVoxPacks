using System.Reflection;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WoWVoxPack.AddOns;
using WoWVoxPack.TTS;

namespace WoWVoxPack.Builder;

public class Worker : IHostedService
{
    public Worker(ILogger<Worker> logger, IEnumerable<IAddOnService> addOnServices, IOptions<TtsSettings> ttsSettings,
        IHostApplicationLifetime applicationLifetime, ISoundFileService soundFileService)
    {
        Logger = logger;
        AddOnServices = addOnServices.ToList();
        TtsSettings = ttsSettings.Value;
        ApplicationLifetime = applicationLifetime;
        SoundFileService = soundFileService;

        string solutionFile =
            Assembly.GetExecutingAssembly().GetCustomAttribute<SolutionFileAttribute>()?.SolutionFile ??
            throw new Exception("Solution file not found.");
        OutputDirectoryBase = Path.Combine(
            Path.GetDirectoryName(solutionFile) ?? throw new Exception("Solution file not found."),
            "output", TtsSettings.Voice?.ToString() ?? "");
    }

    private ILogger<Worker> Logger { get; }
    private List<IAddOnService> AddOnServices { get; }
    private TtsSettings TtsSettings { get; }
    private IHostApplicationLifetime ApplicationLifetime { get; }
    private ISoundFileService SoundFileService { get; }

    private string OutputDirectoryBase { get; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (IAddOnService addOnService in AddOnServices)
        {
            AddOn addOn = await addOnService.BuildAddOnAsync(OutputDirectoryBase, cancellationToken);

            Logger.LogInformation("Building {AddOnName} addon in directory {OutputDirectory}", addOn.Title,
                addOn.AddOnDirectory);

            await addOn.WriteAllFilesAsync(cancellationToken);

            string soundOutputDirectory = addOn.SoundDirectory;
            Directory.CreateDirectory(soundOutputDirectory);

            foreach (SoundFile soundFile in addOn.SoundFiles)
            {
                await SoundFileService.CreateSoundFileIfNotExistsAsync(soundFile, soundOutputDirectory,
                    TtsSettings, cancellationToken);
            }

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
