using System.Text.Json.Serialization;

namespace WoWVoxPack.TTS;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SoundFile))]
[JsonSerializable(typeof(List<SoundFile>))]
public partial class SoundFileJsonContext : JsonSerializerContext;
