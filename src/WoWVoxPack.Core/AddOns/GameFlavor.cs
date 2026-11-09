namespace WoWVoxPack.AddOns;

/// <remarks>Null <see cref="Interfaces"/> and <see cref="AllowLoadGameType"/> keep the root
/// <c>AddOn</c> settings; null <see cref="AddOnIds"/> builds every addon.</remarks>
public sealed record GameFlavor(
    string Name,
    string OutputDirectoryName,
    IReadOnlyList<string>? Interfaces,
    string? AllowLoadGameType,
    IReadOnlySet<string>? AddOnIds)
{
    public static GameFlavor Retail { get; } = new("Retail", "output", null, null, null);

    public static GameFlavor Forever(ForeverSettings settings)
    {
        return new GameFlavor("Forever", "output-forever", [settings.Interface], settings.AllowLoadGameType,
            new HashSet<string>(settings.AddOns, StringComparer.Ordinal));
    }

    public bool Includes(string addOnId)
    {
        return AddOnIds?.Contains(addOnId) ?? true;
    }
}
