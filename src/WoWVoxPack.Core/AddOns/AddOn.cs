using Ardalis.GuardClauses;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns;

public sealed class AddOn
{
    private readonly string _outputDirectoryBase;
    private readonly IReadOnlyDictionary<string, SoundFile> _soundFiles;
    private readonly IReadOnlyDictionary<string, Func<AddOn, string>> _fileFactories;
    private Dictionary<string, string>? _fileContents;

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
        Interfaces = [ToInterfaceNumber(Version)];
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

    public IReadOnlyDictionary<string, string> FileContents =>
        _fileContents ??= _fileFactories.ToDictionary(kvp => kvp.Key, kvp => kvp.Value(this),
            StringComparer.OrdinalIgnoreCase);

    public string AddOnDirectory => Path.Combine(_outputDirectoryBase, AddOnDirectoryName);
    public string AddOnDirectoryName => Title.Replace(' ', '_');
    public string SoundDirectory => Path.Combine(AddOnDirectory, SoundDirectoryName);
    public string SoundDirectoryName => "Sounds";
    public string TocFileName => $"{AddOnDirectoryName}.toc";
    public string SoundFilesJsonPath => Path.Combine(AddOnDirectory, "SoundFiles.json");

    public record Note(string? LanguageCode, string Text);

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
