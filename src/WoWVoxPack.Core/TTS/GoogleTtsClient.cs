using System.Threading.RateLimiting;

using Google.Api.Gax.Grpc.Rest;
using Google.Cloud.TextToSpeech.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging;

namespace WoWVoxPack.TTS;

public sealed class GoogleTtsClient(ILogger<GoogleTtsClient> logger, TextToSpeechClient client)
    : IDisposable, IAsyncDisposable
{
    private readonly TokenBucketRateLimiter _rateLimiter = new(new TokenBucketRateLimiterOptions
    {
        AutoReplenishment = true,
        QueueLimit = 50_000,
        TokenLimit = 1000,
        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
        TokensPerPeriod = 500
    });

    public Task<ByteString> SynthesizeText(
        string text,
        TtsSettings ttsSettings,
        AudioEncoding audioEncoding = AudioEncoding.Linear16,
        CancellationToken cancellationToken = default)
    {
        return SynthesizeText(text, ttsSettings.Voice!.Value, ttsSettings.LanguageCode, ttsSettings.SpeakingRate,
            ttsSettings.Pitch, ttsSettings.SampleRateHertz, audioEncoding, cancellationToken);
    }

    public Task<ByteString> SynthesizeSsml(
        string ssml,
        TtsSettings ttsSettings,
        AudioEncoding audioEncoding = AudioEncoding.Linear16,
        CancellationToken cancellationToken = default)
    {
        return SynthesizeSsml(ssml, ttsSettings.Voice!.Value, ttsSettings.LanguageCode, ttsSettings.SpeakingRate,
            ttsSettings.Pitch, ttsSettings.SampleRateHertz, audioEncoding, cancellationToken);
    }

    public async Task<ByteString> SynthesizeText(
        string text,
        VoiceName voice = VoiceName.Default,
        string languageCode = "en-US",
        float speakingRate = 1.0f,
        float pitch = 0.0f,
        int sampleRateHertz = 44100,
        AudioEncoding audioEncoding = AudioEncoding.Linear16,
        CancellationToken cancellationToken = default)
    {
        using RateLimitLease limit = await _rateLimiter.AcquireAsync(1, cancellationToken);
        return await SynthesizeTextCore(text, voice, languageCode, speakingRate, pitch, sampleRateHertz, audioEncoding,
            cancellationToken);
    }

    public async Task<ByteString> SynthesizeSsml(
        string ssml,
        VoiceName voice = VoiceName.Default,
        string languageCode = "en-US",
        float speakingRate = 1.0f,
        float pitch = 0.0f,
        int sampleRateHertz = 44100,
        AudioEncoding audioEncoding = AudioEncoding.Linear16,
        CancellationToken cancellationToken = default)
    {
        using RateLimitLease limit = await _rateLimiter.AcquireAsync(1, cancellationToken);
        return await SynthesizeSsmlCore(ssml, voice, languageCode, speakingRate, pitch, sampleRateHertz, audioEncoding,
            cancellationToken);
    }

    private async Task<ByteString> SynthesizeTextCore(
        string text,
        VoiceName voice,
        string languageCode,
        float speakingRate,
        float pitch,
        int sampleRateHertz,
        AudioEncoding audioEncoding,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Synthesizing text: {Text}", text);
        SynthesizeSpeechResponse? result = await client.SynthesizeSpeechAsync(
            new SynthesizeSpeechRequest
            {
                Input = new SynthesisInput { Text = text },
                Voice = new VoiceSelectionParams { LanguageCode = languageCode, Name = voice.GetVoiceName() },
                AudioConfig = new AudioConfig
                {
                    AudioEncoding = audioEncoding,
                    EffectsProfileId = { "headphone-class-device" },
                    SpeakingRate = speakingRate,
                    SampleRateHertz = sampleRateHertz,
                    Pitch = pitch
                }
            }, cancellationToken).ConfigureAwait(false);

        if (result.AudioContent is null)
        {
            throw new Exception("No audio content returned from Google Cloud Text-to-Speech API.");
        }

        return result.AudioContent;
    }

    private async Task<ByteString> SynthesizeSsmlCore(
        string ssml,
        VoiceName voice,
        string languageCode,
        float speakingRate,
        float pitch,
        int sampleRateHertz,
        AudioEncoding audioEncoding,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Synthesizing SSML: {Ssml}", ssml);
        SynthesizeSpeechResponse? result = await client.SynthesizeSpeechAsync(
            new SynthesizeSpeechRequest
            {
                Input = new SynthesisInput { Ssml = ssml },
                Voice = new VoiceSelectionParams { LanguageCode = languageCode, Name = voice.GetVoiceName() },
                AudioConfig = new AudioConfig
                {
                    AudioEncoding = audioEncoding,
                    EffectsProfileId = { "headphone-class-device" },
                    SpeakingRate = speakingRate,
                    SampleRateHertz = sampleRateHertz,
                    Pitch = pitch
                }
            }, cancellationToken).ConfigureAwait(false);

        if (result.AudioContent is null)
        {
            throw new Exception("No audio content returned from Google Cloud Text-to-Speech API.");
        }

        return result.AudioContent;
    }

    private TextToSpeechClient CreateClient()
    {
        return new TextToSpeechClientBuilder
        {
            GrpcAdapter = RestGrpcAdapter.Default, Settings = TextToSpeechSettings.GetDefault(), Logger = logger
        }.Build();
    }

    public void Dispose()
    {
        _rateLimiter.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _rateLimiter.DisposeAsync();
    }
}
