using System.ComponentModel.DataAnnotations;

namespace WoWVoxPack.AddOns;

public class AddOnSettings
{
    [Required] public string? Title { get; set; }

    [Required] public string? Version { get; set; }

    [Required] public string? Author { get; set; }

    [Required] public string? Notes { get; set; }

    [Required] public List<string>? Interfaces { get; set; } = new();

    [Required] public List<string>? LuaFiles { get; set; } = new();

    public Dictionary<string, string>? AdditionalNotes
    {
        get;
        set;
    } = new();

    public Dictionary<string, string>? AdditionalProperties
    {
        get;
        set;
    } = new();
}
