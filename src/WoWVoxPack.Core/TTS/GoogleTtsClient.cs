using System.Threading.RateLimiting;

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
        TokenLimit = 400,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        TokensPerPeriod = 7,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    });

    public Task<ByteString> SynthesizeText(
        string text,
        TtsSettings ttsSettings,
        IReadOnlyList<Pronunciation>? pronunciations = null,
        AudioEncoding audioEncoding = AudioEncoding.Linear16,
        CancellationToken cancellationToken = default)
    {
        return SynthesizeText(text, ttsSettings.Voice!.Value, ttsSettings.LanguageCode, ttsSettings.SpeakingRate,
            ttsSettings.Pitch, ttsSettings.SampleRateHertz, pronunciations, audioEncoding, cancellationToken);
    }

    public Task<ByteString> SynthesizeSsml(
        string ssml,
        TtsSettings ttsSettings,
        IReadOnlyList<Pronunciation>? pronunciations = null,
        AudioEncoding audioEncoding = AudioEncoding.Linear16,
        CancellationToken cancellationToken = default)
    {
        return SynthesizeSsml(ssml, ttsSettings.Voice!.Value, ttsSettings.LanguageCode, ttsSettings.SpeakingRate,
            ttsSettings.Pitch, ttsSettings.SampleRateHertz, pronunciations, audioEncoding, cancellationToken);
    }

    public async Task<ByteString> SynthesizeText(
        string text,
        VoiceName voice = VoiceName.Default,
        string languageCode = "en-US",
        float speakingRate = 1.0f,
        float pitch = 0.0f,
        int sampleRateHertz = 44100,
        IReadOnlyList<Pronunciation>? pronunciations = null,
        AudioEncoding audioEncoding = AudioEncoding.Linear16,
        CancellationToken cancellationToken = default)
    {
        using RateLimitLease limit = await _rateLimiter.AcquireAsync(1, cancellationToken);
        return await SynthesizeTextCore(text, voice, languageCode, speakingRate, pitch, sampleRateHertz,
            pronunciations, audioEncoding, cancellationToken);
    }

    public async Task<ByteString> SynthesizeSsml(
        string ssml,
        VoiceName voice = VoiceName.Default,
        string languageCode = "en-US",
        float speakingRate = 1.0f,
        float pitch = 0.0f,
        int sampleRateHertz = 44100,
        IReadOnlyList<Pronunciation>? pronunciations = null,
        AudioEncoding audioEncoding = AudioEncoding.Linear16,
        CancellationToken cancellationToken = default)
    {
        using RateLimitLease limit = await _rateLimiter.AcquireAsync(1, cancellationToken);
        return await SynthesizeSsmlCore(ssml, voice, languageCode, speakingRate, pitch, sampleRateHertz,
            pronunciations, audioEncoding, cancellationToken);
    }

    public void Dispose()
    {
        _rateLimiter.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _rateLimiter.DisposeAsync();
    }

    /// <summary>
    /// Google matches each phrase against the input and applies the IPA to it. The docs require an
    /// exact match and forbid the phrase sitting inside a phoneme tag, which is why the callers
    /// send plain text rather than SSML for anything carrying a pronunciation. en-US only.
    /// </summary>
    private static SynthesisInput WithPronunciations(SynthesisInput input,
        IReadOnlyList<Pronunciation>? pronunciations)
    {
        if (pronunciations is not { Count: > 0 })
        {
            return input;
        }

        input.CustomPronunciations = new CustomPronunciations();
        foreach (Pronunciation pronunciation in pronunciations)
        {
            input.CustomPronunciations.Pronunciations.Add(new CustomPronunciationParams
            {
                Phrase = pronunciation.Phrase,
                PhoneticEncoding = CustomPronunciationParams.Types.PhoneticEncoding.Ipa,
                Pronunciation = pronunciation.Ipa
            });
        }

        return input;
    }

    private Task<ByteString> SynthesizeTextCore(
        string text,
        VoiceName voice,
        string languageCode,
        float speakingRate,
        float pitch,
        int sampleRateHertz,
        IReadOnlyList<Pronunciation>? pronunciations,
        AudioEncoding audioEncoding,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Synthesizing text: {Text}", text);
        return SynthesizeCore(new SynthesisInput { Text = text }, pronunciations, voice, languageCode,
            speakingRate, pitch, sampleRateHertz, audioEncoding, cancellationToken);
    }

    private Task<ByteString> SynthesizeSsmlCore(
        string ssml,
        VoiceName voice,
        string languageCode,
        float speakingRate,
        float pitch,
        int sampleRateHertz,
        IReadOnlyList<Pronunciation>? pronunciations,
        AudioEncoding audioEncoding,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Synthesizing SSML: {Ssml}", ssml);
        return SynthesizeCore(new SynthesisInput { Ssml = ssml }, pronunciations, voice, languageCode,
            speakingRate, pitch, sampleRateHertz, audioEncoding, cancellationToken);
    }

    private async Task<ByteString> SynthesizeCore(
        SynthesisInput input,
        IReadOnlyList<Pronunciation>? pronunciations,
        VoiceName voice,
        string languageCode,
        float speakingRate,
        float pitch,
        int sampleRateHertz,
        AudioEncoding audioEncoding,
        CancellationToken cancellationToken)
    {
        SynthesizeSpeechResponse? result = await client.SynthesizeSpeechAsync(
            new SynthesizeSpeechRequest
            {
                Input = WithPronunciations(input, pronunciations),
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
}
