namespace WoWVoxPack.TTS;

public sealed record PronunciationExceptionRule(string Addon, string Key, string Phrase,
    string? Ipa = null, bool Suppress = false, string Reason = "");
