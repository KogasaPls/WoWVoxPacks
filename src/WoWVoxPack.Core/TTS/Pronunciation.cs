namespace WoWVoxPack.TTS;

/// <summary>
/// How one phrase in a recording's text should be said. Google matches the phrase in the input
/// and applies the IPA to it, which is why the hint cannot stay in the text the way the
/// "Word=IPA" convention writes it.
/// </summary>
public sealed record Pronunciation(string Phrase, string Ipa);
