using Google.Protobuf;

namespace WoWVoxPack.TTS;

public class GoogleTtsProvider(GoogleTtsClient client) : ITtsProvider
{
    private GoogleTtsClient Client { get; } = client;

    public async Task<TtsResponse> GetAudioContentAsync(SoundFile soundFile, TtsSettings settings,
        CancellationToken cancellationToken = default)
    {
        ByteString audioContent;
        if (soundFile.Ssml is { } ssml)
        {
            audioContent = await Client.SynthesizeSsml(ssml, settings,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else if (soundFile.Text is { } text)
        {
            audioContent = await Client.SynthesizeText(text, settings,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new ArgumentException("SoundFile must have either SSML or Text");
        }

        return new TtsResponse(audioContent.ToByteArray(), AudioFormat.Wav);
    }
}
