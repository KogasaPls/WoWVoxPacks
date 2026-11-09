using System.ComponentModel.DataAnnotations;

namespace WoWVoxPack.AddOns;

public sealed class ForeverSettings
{
    [Required]
    public string Interface { get; set; } = string.Empty;

    [Required]
    public string AllowLoadGameType { get; set; } = string.Empty;

    [Required]
    public List<string> AddOns { get; set; } = [];
}
