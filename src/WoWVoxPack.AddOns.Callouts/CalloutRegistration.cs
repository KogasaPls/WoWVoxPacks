using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.Callouts;

/// <summary>
/// A callout's audio plus any literal LibSharedMedia keys owned by its source. A retirement-only
/// registration may reuse a recording that already shipped, but must never create a new one.
/// </summary>
public sealed record CalloutRegistration(
    SoundFile SoundFile,
    IReadOnlyList<string> MediaKeys,
    bool ReuseExistingAudioOnly = false);
