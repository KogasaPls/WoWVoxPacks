namespace WoWVoxPack.AddOns.Callouts;

/// <summary>
/// A recording cut from other media keys' recordings rather than spoken: Northern Sky Raid
/// Tools ships a few timed clips, such as a countdown, that no single phrase renders.
/// </summary>
public sealed record CompositionOverride(double Duration, IReadOnlyList<CompositionPartOverride> Parts);
