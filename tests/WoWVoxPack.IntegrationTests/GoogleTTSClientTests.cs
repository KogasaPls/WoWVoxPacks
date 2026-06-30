using Google.Protobuf;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using WoWVoxPack.TTS;

using Xunit.Abstractions;

namespace WoWVoxPack.IntegrationTests;

public class GoogleTTSClientTests
{
    public GoogleTTSClientTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private ITestOutputHelper Output { get; }

    [Fact]
    public async Task SynthesizeText_Should_ReturnAudioContent()
    {
        if (!HasGoogleCredentials())
        {
            Output.WriteLine("Google credentials not configured; skipping live TTS integration test.");
            return;
        }

        IServiceCollection services = new ServiceCollection()
            .AddLogging(builder => builder.AddXUnit(Output))
            .AddTextToSpeechClient()
            .AddSingleton<GoogleTtsClient>();

        GoogleTtsClient client = services.BuildServiceProvider().GetRequiredService<GoogleTtsClient>();

        ByteString audioContent = await client.SynthesizeText("Hello, World!");

        Assert.NotNull(audioContent);

        Output.WriteLine($"Audio content length: {audioContent.Length}");
        Output.WriteLine(audioContent.ToBase64());
    }

    private static bool HasGoogleCredentials()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")))
        {
            return true;
        }

        string applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return File.Exists(Path.Combine(applicationDataPath, "gcloud", "application_default_credentials.json"));
    }
}
