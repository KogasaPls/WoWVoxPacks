using Google.Protobuf;

namespace BigWigsVoiceGCP.AddOns.SharedMedia_Causese;

internal record CauseseSoundFile(string FileName, ByteString AudioContent) : AddOnSoundFile(FileName, AudioContent);
