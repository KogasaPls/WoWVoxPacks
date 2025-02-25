using Google.Protobuf;

namespace BigWigsVoiceGCP.AddOns.BigWigsVoice;

public record BigWigsVoiceSoundFile(string SpellId, string SpellName, ByteString AudioContent)
    : AddOnSoundFile($"{SpellId}.wav", AudioContent)
{
    public string SpellId { get; } = SpellId;

    public string SpellName
    {
        get;
    } = SpellName;

    public Task WriteToDiskAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        string filePath = Path.Combine(outputDirectory, FileName);
        return File.WriteAllBytesAsync(filePath, AudioContent.ToByteArray(), cancellationToken);
    }
}
