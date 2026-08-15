using System.Text.Json.Serialization;

namespace WoWVoxPack.TTS;

[JsonSerializable(typeof(BuildRecipe))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class BuildRecipeJsonContext : JsonSerializerContext;
