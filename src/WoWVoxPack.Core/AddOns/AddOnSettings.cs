using System.ComponentModel.DataAnnotations;

namespace WoWVoxPack.AddOns;

public class AddOnSettings
{
    [Required]
    public string? Title { get; set; }

    public string? DisplayTitle { get; set; }

    [Required]
    public string? Version { get; set; }

    [Required]
    public string? Author { get; set; }

    [Required]
    public string? Notes { get; set; }

    public Dictionary<string, string>? AdditionalNotes
    {
        get;
        set;
    }

    = new();

    public Dictionary<string, string>? AdditionalProperties
    {
        get;
        set;
    }

    = new();

    /// <summary>
    /// WoW toc Interface numbers, root-only: every addon is bound to its own
    /// <c>AddOn:{Name}</c> section and then to <c>AddOn</c>, and the configuration binder
    /// APPENDS to a <see cref="List{T}"/> rather than replacing it, so a per-addon
    /// <c>["110000"]</c> would come out as <c>["110000", "120007", "120100"]</c>.
    /// <see cref="AddOn"/> deduplicates and validates what it is handed.
    /// </summary>
    public List<string>? Interfaces { get; set; }
}
