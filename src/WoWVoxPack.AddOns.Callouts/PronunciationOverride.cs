namespace WoWVoxPack.AddOns.Callouts;

/// <summary>A hand-authored pronunciation exception for one upstream sound name.</summary>
public sealed record PronunciationOverride(
    string? Text = null,
    string? Ssml = null,
    bool Exclude = false,
    string? FileName = null);
