using System.Reflection;

using BigWigsVoiceGCP.AddOns;

using Microsoft.Extensions.Hosting;

namespace BigWigsVoiceGCP.Builder;

public class Worker : IHostedService
{
    public Worker(IEnumerable<IAddOnService> addOnServices, IHostApplicationLifetime applicationLifetime)
    {
        AddOnServices = addOnServices.ToList();
        string solutionFile =
            Assembly.GetExecutingAssembly().GetCustomAttribute<SolutionFileAttribute>()?.SolutionFile ??
            throw new Exception("Solution file not found.");
        OutputDirectory = Path.Combine(
            Path.GetDirectoryName(solutionFile) ?? throw new Exception("Solution file not found."),
            "output");
        ApplicationLifetime = applicationLifetime;
    }

    private IHostApplicationLifetime ApplicationLifetime { get; }


    public List<IAddOnService> AddOnServices { get; }
    public string OutputDirectory { get; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (IAddOnService addOnService in AddOnServices)
        {
            await addOnService.BuildAddOnAsync(OutputDirectory, cancellationToken);
        }

        ApplicationLifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
