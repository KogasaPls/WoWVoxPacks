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
        IServiceCollection services = new ServiceCollection()
            .AddLogging(builder => builder.AddXUnit(Output))
            .AddSingleton<GoogleTtsClient>();

        GoogleTtsClient client = services.BuildServiceProvider().GetRequiredService<GoogleTtsClient>();

        ByteString audioContent = await client.SynthesizeText("Hello, World!");

        Assert.NotNull(audioContent);

        Output.WriteLine($"Audio content length: {audioContent.Length}");
        Output.WriteLine(audioContent.ToBase64());
    }
}
