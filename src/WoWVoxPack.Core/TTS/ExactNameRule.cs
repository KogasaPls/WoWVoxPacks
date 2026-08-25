namespace WoWVoxPack.TTS;

public sealed record ExactNameRule(string Name, IReadOnlyList<string>? Aliases = null,
    string? Text = null, IReadOnlyList<Pronunciation>? Pronunciations = null);
