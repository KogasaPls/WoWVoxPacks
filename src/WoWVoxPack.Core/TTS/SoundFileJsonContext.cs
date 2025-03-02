using System.Text.Json.Serialization;

namespace WoWVoxPack.TTS;

[JsonSourceGenerationOptions(WriteIndented = true,
    RespectNullableAnnotations = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SoundFile))]
[JsonSerializable(typeof(List<SoundFile>))]
public partial class SoundFileJsonContext : JsonSerializerContext;
