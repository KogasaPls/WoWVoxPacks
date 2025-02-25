using Google.Protobuf;

namespace BigWigsVoiceGCP.AddOns;

public record AddOnSoundFile(string FileName, ByteString AudioContent);
