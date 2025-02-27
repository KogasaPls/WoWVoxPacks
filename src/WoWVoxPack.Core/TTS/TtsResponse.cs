namespace WoWVoxPack.TTS;

public record TtsResponse(byte[] AudioContent, AudioFormat Format);

public static class AudioFormatExtensions
{
    public static string GetFileExtension(this AudioFormat format)
    {
        return format switch
        {
            AudioFormat.Wav => ".wav",
            AudioFormat.Mp3 => ".mp3",
            AudioFormat.OggOpus => ".ogg",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }
}
