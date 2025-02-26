using Google.Protobuf;

namespace WoWVoxPack.AddOns;

public record AddOnSoundFile(string FileName, ByteString AudioContent);
