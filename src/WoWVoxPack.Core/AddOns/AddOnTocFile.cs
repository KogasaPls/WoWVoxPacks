using System.Text;

namespace WoWVoxPack.AddOns;

/// <summary>
/// Generates an addon's <c>.toc</c>: the metadata the game reads before it loads anything, then
/// the files to load. WoW has no error path for a malformed one, so the shape here is the whole
/// contract.
/// </summary>
public static class AddOnTocFile
{
    public static string Render(AddOn addOn)
    {
        StringBuilder toc = new();
        toc.Append($"## Interface: {string.Join(", ", addOn.Interfaces)}\n");
        toc.Append('\n');
        toc.Append($"## Title: {addOn.DisplayTitle}\n");
        toc.Append($"## Version: {addOn.Version}\n");

        if (addOn.PrimaryNote is not null)
        {
            toc.Append($"## Notes: {addOn.PrimaryNote.Text}\n");
        }

        foreach (AddOn.Note note in addOn.AdditionalNotes)
        {
            toc.Append($"## Notes-{note.LanguageCode ?? string.Empty}: {note.Text}\n");
        }

        toc.Append($"## Author: {addOn.Author}\n");
        toc.Append('\n');

        foreach ((string key, string value) in addOn.AdditionalProperties)
        {
            toc.Append($"## {key}: {value}\n");
        }

        toc.Append('\n');

        foreach (string file in addOn.Files)
        {
            toc.Append($"{file}\n");
        }

        return toc.ToString();
    }
}
