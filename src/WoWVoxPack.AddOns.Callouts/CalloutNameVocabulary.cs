namespace WoWVoxPack.AddOns.Callouts;

/// <summary>
/// Reads a tracked upstream callout-name file. Comment-prefixed provenance stays beside the
/// generated data without becoming a spoken name; tracking the file keeps builds offline and
/// makes upstream vocabulary changes reviewable.
/// </summary>
public static class CalloutNameVocabulary
{
    public static IReadOnlyList<string> Parse(IEnumerable<string> lines) =>
        lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<string> Load(string path) => Parse(File.ReadAllLines(path));
}
