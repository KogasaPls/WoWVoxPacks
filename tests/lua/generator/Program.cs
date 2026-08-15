// Writes the Lua the C# generators emit, one folder per addon, so the Lua harness executes the
// real output instead of a copy pasted into the test tree.
//
// It deliberately does not read output/: that folder is only refreshed by a full build, which
// costs paid TTS calls, so it lags the source it came from. The regression this harness exists
// to catch lives in the source.
//
// usage: dotnet run --project tests/lua/generator -- <repo-root> <output-directory>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using WoWVoxPack.AddOns;
using WoWVoxPack.AddOns.BigWigs_Countdown;
using WoWVoxPack.AddOns.BigWigs_Voice;
using WoWVoxPack.AddOns.Callouts;
using WoWVoxPack.AddOns.ExBoss;
using WoWVoxPack.TTS;

if (args.Length != 2)
{
    await Console.Error.WriteLineAsync(
        "usage: WoWVoxPack.LuaFixtures <repo-root> <output-directory>");
    return 1;
}

string repoRoot = Path.GetFullPath(args[0]);
string outputRoot = Path.GetFullPath(args[1]);

IConfigurationRoot configuration = new ConfigurationBuilder()
    .SetBasePath(repoRoot)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

ServiceCollection services = new();
services.AddSingleton<IConfiguration>(configuration);
services.AddOptions();

// Same two-step binding the Builder uses: the per-addon section, then the AddOn root.
foreach (string name in (string[])
         ["Callouts", "NorthernSkyRaidTools", "ExBoss", "BigWigs_Countdown", "BigWigs_Voice"])
{
    services.AddOptions<AddOnSettings>(name)
        .BindConfiguration($"AddOn:{name}")
        .BindConfiguration("AddOn");
}

services.AddSingleton(_ => new CalloutsVocabularyProvider(
    Path.Combine(AppContext.BaseDirectory, "Callouts_Sounds.json"),
    Path.Combine(AppContext.BaseDirectory, "CalloutPronunciations.json"),
    Path.Combine(repoRoot, "lorrgs-vocabulary.txt"),
    Path.Combine(AppContext.BaseDirectory, "RetiredCallouts.json")));
services.AddSingleton(_ => new NorthernSkyRaidToolsVocabularyProvider(
    Path.Combine(repoRoot, "nsrt-vocabulary.txt"),
    Path.Combine(AppContext.BaseDirectory, "CalloutPronunciations.json")));

// The BigWigs pack's Lua is the same whatever the spell list holds, and fetching the real one
// would put GitHub between the harness and a run.
services.AddSingleton<IBigWigsVoiceUpstreamClient, NoSpellsUpstreamClient>();
services.AddScoped<BigWigsVoiceAddOnService>();
services.AddScoped<CalloutsMediaAddOnService>();
services.AddScoped<NorthernSkyRaidToolsAddOnService>();
services.AddScoped<ExBossAddOnService>();
services.AddScoped<BigWigsCountdownAddOnService>();

await using ServiceProvider provider = services.BuildServiceProvider();
using IServiceScope scope = provider.CreateScope();
IServiceProvider sp = scope.ServiceProvider;

if (Directory.Exists(outputRoot))
{
    Directory.Delete(outputRoot, recursive: true);
}

await Emit(sp.GetRequiredService<BigWigsVoiceAddOnService>(), VoiceName.Neural2_C);
await Emit(sp.GetRequiredService<NorthernSkyRaidToolsAddOnService>(), VoiceName.Neural2_C);
await Emit(sp.GetRequiredService<NorthernSkyRaidToolsAddOnService>(), VoiceName.Studio_O);
await Emit(sp.GetRequiredService<CalloutsMediaAddOnService>(), VoiceName.Neural2_C);
await Emit(sp.GetRequiredService<CalloutsMediaAddOnService>(), VoiceName.Studio_O);
await Emit(sp.GetRequiredService<ExBossAddOnService>(), VoiceName.Neural2_C);
await Emit(sp.GetRequiredService<BigWigsCountdownAddOnService>(), VoiceName.Neural2_C);

return 0;

async Task Emit(IAddOnService service, VoiceName voice)
{
    AddOn addOn = await service.BuildAddOnAsync(outputRoot, new TtsSettings { Voice = voice });
    Directory.CreateDirectory(addOn.AddOnDirectory);

    foreach (string file in addOn.Files)
    {
        string path = Path.Combine(addOn.AddOnDirectory, file);
        await File.WriteAllTextAsync(path, addOn.GetFileContent(file));
        Console.WriteLine(Path.GetRelativePath(outputRoot, path));
    }
}

internal sealed class NoSpellsUpstreamClient : IBigWigsVoiceUpstreamClient
{
    public Task<IEnumerable<BigWigsVoiceSoundFile>> GetSoundFilesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<BigWigsVoiceSoundFile>>([]);
    }
}
