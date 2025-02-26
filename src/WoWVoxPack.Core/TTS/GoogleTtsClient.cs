using Google.Api.Gax.Grpc.Rest;
using Google.Cloud.TextToSpeech.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging;

using RateLimiter;

namespace WoWVoxPack.TTS;

public class GoogleTtsClient
{
    private readonly TextToSpeechClient _client;
    private readonly ILogger<GoogleTtsClient> _logger;

    private readonly TimeLimiter _rateLimiter =
        TimeLimiter.GetFromMaxCountByInterval(500, TimeSpan.FromMinutes(1));

    public GoogleTtsClient(ILogger<GoogleTtsClient> logger)
    {
        _logger = logger;
        _client = CreateClient();
    }

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

    public Task<ByteString> SynthesizeText(
        string text,
        VoiceName voice = VoiceName.Default,
        string languageCode = "en-US",
        float speakingRate = 1.0f,
        float pitch = 0.0f,
        int sampleRateHertz = 44100,
        AudioEncoding audioEncoding = AudioEncoding.Linear16,
        CancellationToken cancellationToken = default)
    {
        return _rateLimiter.Enqueue(() =>
            SynthesizeTextCore(text, voice, languageCode, speakingRate, pitch, sampleRateHertz, audioEncoding,
                cancellationToken), cancellationToken);
    }

    public Task<ByteString> SynthesizeSsml(
        string ssml,
        VoiceName voice = VoiceName.Default,
        string languageCode = "en-US",
        float speakingRate = 1.0f,
        float pitch = 0.0f,
        int sampleRateHertz = 44100,
        AudioEncoding audioEncoding = AudioEncoding.Linear16,
        CancellationToken cancellationToken = default)
    {
        return _rateLimiter.Enqueue(() =>
            SynthesizeSsmlCore(ssml, voice, languageCode, speakingRate, pitch, sampleRateHertz, audioEncoding,
                cancellationToken), cancellationToken);
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
        _logger.LogInformation("Synthesizing text: {Text}", text);
        SynthesizeSpeechResponse? result = await _client.SynthesizeSpeechAsync(
            new SynthesizeSpeechRequest
            {
                Input = new SynthesisInput { Text = text },
                Voice = new VoiceSelectionParams
                {
                    LanguageCode = languageCode, Name = Voices.GetVoiceName(voice, languageCode)
                },
                AudioConfig = new AudioConfig
                {
                    AudioEncoding = audioEncoding,
                    EffectsProfileId = { "headphone-class-device" },
                    SpeakingRate = speakingRate,
                    SampleRateHertz = sampleRateHertz,
                    Pitch = pitch
                }
            }, cancellationToken);

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
        _logger.LogInformation("Synthesizing SSML: {Ssml}", ssml);
        SynthesizeSpeechResponse? result = await _client.SynthesizeSpeechAsync(
            new SynthesizeSpeechRequest
            {
                Input = new SynthesisInput { Ssml = ssml },
                Voice = new VoiceSelectionParams
                {
                    LanguageCode = languageCode, Name = Voices.GetVoiceName(voice, languageCode)
                },
                AudioConfig = new AudioConfig
                {
                    AudioEncoding = audioEncoding,
                    EffectsProfileId = { "headphone-class-device" },
                    SpeakingRate = speakingRate,
                    SampleRateHertz = sampleRateHertz,
                    Pitch = pitch
                }
            }, cancellationToken);

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
            GrpcAdapter = RestGrpcAdapter.Default, Settings = TextToSpeechSettings.GetDefault(), Logger = _logger
        }.Build();
    }
}
