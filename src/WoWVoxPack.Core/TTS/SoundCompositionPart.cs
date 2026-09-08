namespace WoWVoxPack.TTS;

/// <summary>One source recording of a composition and the second it starts speaking at.</summary>
public sealed record SoundCompositionPart(string FileName, double At);
