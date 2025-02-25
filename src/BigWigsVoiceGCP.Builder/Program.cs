using System.Reflection;

using BigWigsVoiceGCP;
using BigWigsVoiceGCP.AddOns;
using BigWigsVoiceGCP.AddOns.BigWigsVoice;
using BigWigsVoiceGCP.AddOns.SharedMedia_Causese;
using BigWigsVoiceGCP.Builder;
using BigWigsVoiceGCP.TTS;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;



IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args)
    .UseConsoleLifetime()
    .ConfigureServices((_, services) =>
    {
        services.AddScoped<GoogleTtsClient>();
        services.AddHttpClient<IBigWigsVoiceUpstreamClient, BigWigsVoiceUpstreamClient>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAddOnService, BigWigsVoiceAddOnService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAddOnService, CauseseAddOnService>());
        services.AddOptionsWithValidateOnStart<TtsSettings>().BindConfiguration("Tts");
        services.AddOptionsWithValidateOnStart<AddOnSettings>("BigWigs_Voice").BindConfiguration("AddOn:BigWigs_Voice")
            .BindConfiguration("AddOn");
        services.AddOptionsWithValidateOnStart<AddOnSettings>("SharedMedia_Causese").BindConfiguration("AddOn:ShardMedia_Causese")
            .BindConfiguration("AddOn");
        services.AddHostedService<Worker>();
    }).ConfigureLogging((_, logging) =>
    {
        logging.AddConsole();
        logging.AddDebug();
    })
    .ConfigureAppConfiguration((hostContext, config) =>
    {
        config.AddJsonFile(Path.Combine( "appsettings.json"), false);
        config.AddJsonFile(
            $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", true);

        config.AddCommandLine(args);
    });

using IHost host = hostBuilder.Build();
await host.RunAsync();
