using Google.Protobuf;

namespace WoWVoxPack.AddOns.SharedMedia_Causese;

internal record CauseseSoundFile(string FileName, ByteString AudioContent) : AddOnSoundFile(FileName, AudioContent);
