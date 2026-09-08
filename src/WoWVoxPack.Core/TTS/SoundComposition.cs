namespace WoWVoxPack.TTS;

/// <summary>
/// A recording assembled from other recordings in the same pack instead of synthesised: each
/// part starts at its onset, and the whole is padded or cut to the duration, in seconds.
/// </summary>
public sealed record SoundComposition(double Duration, IReadOnlyList<SoundCompositionPart> Parts)
{
    public bool Equals(SoundComposition? other) =>
        other is not null && Duration.Equals(other.Duration) && Parts.SequenceEqual(other.Parts);

    public override int GetHashCode() => HashCode.Combine(Duration, Parts.Count);

    public override string ToString() =>
        FormattableString.Invariant(
            $"{string.Join(" ", Parts.Select(part => FormattableString.Invariant($"{part.FileName}@{part.At:0.00}s")))} in {Duration:0.00}s");
}
