using System.Text.RegularExpressions;

using Ardalis.GuardClauses;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns;

public sealed partial class AddOn
{
    private readonly string _outputDirectoryBase;
    private readonly IReadOnlyDictionary<string, SoundFile> _soundFiles;
    private readonly IReadOnlyDictionary<string, Func<AddOn, string>> _fileFactories;

    internal AddOn(
        string outputDirectoryBase,
        string title,
        string displayTitle,
        string version,
        string author,
        TtsSettings ttsSettings,
        Note? primaryNote,
        IReadOnlyCollection<Note> additionalNotes,
        IReadOnlyDictionary<string, string> additionalProperties,
        IReadOnlyCollection<string>? interfaces,
        IReadOnlyDictionary<string, SoundFile> soundFiles,
        IReadOnlyDictionary<string, Func<AddOn, string>> fileFactories)
    {
        _outputDirectoryBase = outputDirectoryBase;
        Title = Guard.Against.NullOrWhiteSpace(title);
        DisplayTitle = displayTitle;
        Version = Guard.Against.NullOrWhiteSpace(version);
        Author = Guard.Against.NullOrWhiteSpace(author);
        TtsSettings = ttsSettings;
        PrimaryNote = primaryNote;
        AdditionalNotes = additionalNotes;
        AdditionalProperties = additionalProperties;
        // Configured Interfaces list wins so a pack can ship two toc versions and survive a
        // patch; the version-derived value is only a fallback for addons with none configured.
        Interfaces = interfaces is { Count: > 0 }
            ? NormalizeInterfaces(interfaces)
            : [ToInterfaceNumber(Version)];
        _soundFiles = soundFiles;
        _fileFactories = fileFactories;
    }

    public string Title { get; }
    public string DisplayTitle { get; }
    public string Version { get; }
    public string Author { get; }
    public TtsSettings TtsSettings { get; }
    public Note? PrimaryNote { get; }
    public IReadOnlyCollection<Note> AdditionalNotes { get; }
    public IReadOnlyDictionary<string, string> AdditionalProperties { get; }
    public IReadOnlyCollection<string> Interfaces { get; }

    public IEnumerable<SoundFile> SoundFiles => _soundFiles.Values;

    public IEnumerable<string> Files => _fileFactories.Keys;

    public string GetFileContent(string fileName) => _fileFactories[fileName](this);

    public string AddOnDirectory => Path.Combine(_outputDirectoryBase, AddOnDirectoryName);
    public string AddOnDirectoryName => Title.Replace(' ', '_');
    public string SoundDirectory => Path.Combine(AddOnDirectory, SoundDirectoryName);
    public string SoundDirectoryName => "Sounds";
    public string TocFileName => $"{AddOnDirectoryName}.toc";
    public string SoundFilesJsonPath => Path.Combine(AddOnDirectory, "SoundFiles.json");

    public record Note(string? LanguageCode, string Text);

    /// <summary>
    /// The configuration binder appends to a list rather than replacing it, so binding a
    /// per-addon section and then the root can hand the same number over twice. WoW also has
    /// no error path for a malformed <c>## Interface:</c> line: it treats the addon as
    /// unsupported and says nothing, so a typo has to fail the build instead.
    /// </summary>
    private static IReadOnlyCollection<string> NormalizeInterfaces(IReadOnlyCollection<string> interfaces)
    {
        string[] distinct = interfaces.Distinct(StringComparer.Ordinal).ToArray();

        foreach (string @interface in distinct)
        {
            if (!InterfaceNumber().IsMatch(@interface))
            {
                throw new InvalidOperationException(
                    $"Interface '{@interface}' is not a 5- or 6-digit toc Interface number.");
            }
        }

        return distinct;
    }

    [GeneratedRegex(@"^\d{5,6}$")]
    private static partial Regex InterfaceNumber();

    /// <summary>
    /// Converts a dotted game version (e.g. "12.0.7") into the WoW toc Interface number
    /// (e.g. "120007"): the major version followed by two-digit minor and patch components.
    /// </summary>
    internal static string ToInterfaceNumber(string version)
    {
        Guard.Against.NullOrWhiteSpace(version);

        int[] parts = version
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();

        int major = parts.Length > 0 ? parts[0] : 0;
        int minor = parts.Length > 1 ? parts[1] : 0;
        int patch = parts.Length > 2 ? parts[2] : 0;

        return $"{major}{minor:D2}{patch:D2}";
    }
}
