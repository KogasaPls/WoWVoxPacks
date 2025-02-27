using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using WoWVoxPack.AddOns;
using WoWVoxPack.AddOns.BigWigs_Voice;
using WoWVoxPack.AddOns.SharedMedia_Causese;
using WoWVoxPack.Builder;
using WoWVoxPack.TTS;

IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args)
    .UseConsoleLifetime()
    .ConfigureServices((_, services) =>
    {
        services.AddScoped<GoogleTtsClient>();
        services.AddSingleton<ITtsProvider, GoogleTtsProvider>();
        services.AddSingleton<ISoundFileService, SoundFileService>();
        services.AddHttpClient<IBigWigsVoiceUpstreamClient, BigWigsVoiceUpstreamClient>();
        services.AddHttpClient<ICauseseUpstreamClient, CauseseUpstreamClient>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAddOnService, BigWigsVoiceAddOnService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAddOnService, CauseseAddOnService>());
        services.AddOptionsWithValidateOnStart<TtsSettings>().BindConfiguration("TtsSettings");
        services.AddOptionsWithValidateOnStart<AddOnSettings>("BigWigs_Voice").BindConfiguration("AddOn:BigWigs_Voice")
            .BindConfiguration("AddOn");
        services.AddOptionsWithValidateOnStart<AddOnSettings>("SharedMedia_Causese")
            .BindConfiguration("AddOn:SharedMedia_Causese")
            .BindConfiguration("AddOn");
        services.AddHostedService<Worker>();
    }).ConfigureLogging((_, logging) =>
    {
        logging.AddConsole();
        logging.AddDebug();
    })
    .ConfigureAppConfiguration((hostContext, config) =>
    {
        config.AddJsonFile(Path.Combine("appsettings.json"), false);
        config.AddJsonFile(
            $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", true);

        config.AddCommandLine(args);
    });

using IHost host = hostBuilder.Build();
await host.RunAsync();
