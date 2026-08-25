namespace WoWVoxPack.TTS;

public sealed record PhraseRule(string Phrase, string Ipa,
    IReadOnlyList<string>? Aliases = null);
