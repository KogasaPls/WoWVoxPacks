using Microsoft.Extensions.Hosting;

namespace WoWVoxPack.Builder;

public class Worker(AddOnBuildOrchestrator orchestrator, IHostApplicationLifetime applicationLifetime)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await orchestrator.RunAsync(cancellationToken);
        applicationLifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
