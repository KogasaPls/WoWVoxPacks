using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoWVoxPack;
using WoWVoxPack.AddOns;
using WoWVoxPack.AddOns.BigWigs_Countdown;
using WoWVoxPack.AddOns.BigWigs_Voice;
using WoWVoxPack.AddOns.Callouts;
using WoWVoxPack.AddOns.ExBoss;
using WoWVoxPack.Builder;
using WoWVoxPack.Core.Builder;
using WoWVoxPack.TTS;

static string ResolveOutputDirectoryBase()
{
    string solutionFile =
        Assembly.GetExecutingAssembly().GetCustomAttribute<SolutionFileAttribute>()?.SolutionFile ??
        throw new Exception("Solution file not found.");
    return Path.Combine(
        Path.GetDirectoryName(solutionFile) ?? throw new Exception("Solution file not found."),
        "output");
}

IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args)
    .UseConsoleLifetime()
    .ConfigureServices((_, services) =>
    {
        services.AddTextToSpeechClient();
        services.AddSingleton<GoogleTtsClient>();
        services.AddSingleton<ITtsProvider, GoogleTtsProvider>();
        services.AddSingleton<ISoundFileService, SoundFileService>();
        services.AddHttpClient<IBigWigsVoiceUpstreamClient, BigWigsVoiceUpstreamClient>();
        services.AddSingleton(_ => new CalloutsVocabularyProvider(
            Path.Combine(AppContext.BaseDirectory, "Callouts_Sounds.json"),
            Path.Combine(AppContext.BaseDirectory, "CalloutPronunciations.json"),
            Path.Combine(Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().GetCustomAttribute<SolutionFileAttribute>()!.SolutionFile)!,
                "lorrgs-vocabulary.txt"),
            Path.Combine(AppContext.BaseDirectory, "RetiredCallouts.json")));
        services.AddSingleton(_ => new NorthernSkyRaidToolsVocabularyProvider(
            Path.Combine(Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().GetCustomAttribute<SolutionFileAttribute>()!.SolutionFile)!,
                "nsrt-vocabulary.txt"),
            Path.Combine(AppContext.BaseDirectory, "CalloutPronunciations.json")));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAddOnService, BigWigsVoiceAddOnService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAddOnService, BigWigsCountdownAddOnService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAddOnService, CalloutsMediaAddOnService>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAddOnService, NorthernSkyRaidToolsAddOnService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAddOnService, ExBossAddOnService>());
        services.AddOptionsWithValidateOnStart<BuildMatrix>().BindConfiguration("Matrix");

        // Each addon binds its own AddOn:{Name} section and then the AddOn root. The binder
        // appends to a List<T>, so Interfaces is root-only: a per-addon list would be
        // concatenated with the root's, not replace it.
        services.AddOptionsWithValidateOnStart<AddOnSettings>("BigWigs_Voice").BindConfiguration("AddOn:BigWigs_Voice")
            .BindConfiguration("AddOn");
        services.AddOptionsWithValidateOnStart<AddOnSettings>("BigWigs_Countdown")
            .BindConfiguration("AddOn:BigWigs_Countdown")
            .BindConfiguration("AddOn");
        services.AddOptionsWithValidateOnStart<AddOnSettings>("Callouts")
            .BindConfiguration("AddOn:Callouts")
            .BindConfiguration("AddOn");
        services.AddOptionsWithValidateOnStart<AddOnSettings>("NorthernSkyRaidTools")
            .BindConfiguration("AddOn:NorthernSkyRaidTools")
            .BindConfiguration("AddOn");
        services.AddOptionsWithValidateOnStart<AddOnSettings>("ExBoss")
            .BindConfiguration("AddOn:ExBoss")
            .BindConfiguration("AddOn");
        services.AddSingleton(sp => new AddOnBuildOrchestrator(
            sp.GetRequiredService<ILogger<AddOnBuildOrchestrator>>(),
            sp.GetRequiredService<IEnumerable<IAddOnService>>(),
            sp.GetRequiredService<IOptions<BuildMatrix>>(),
            sp.GetRequiredService<ISoundFileService>(),
            ResolveOutputDirectoryBase()));
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
