using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns;

public sealed class AddOnDraft
{
    private readonly string _outputDirectoryBase;
    private readonly string _title;
    private readonly string _displayTitle;
    private readonly string _version;
    private readonly string _author;
    private readonly TtsSettings _ttsSettings;
    private readonly AddOn.Note? _primaryNote;
    private readonly IReadOnlyCollection<AddOn.Note> _additionalNotes;
    private readonly IReadOnlyDictionary<string, string> _additionalProperties;
    private readonly IReadOnlyCollection<string>? _interfaces;
    private readonly IReadOnlyDictionary<string, SoundFile> _soundFiles;
    private readonly IReadOnlyDictionary<string, Func<AddOn, string>> _fileFactories;

    internal AddOnDraft(
        string addOnId,
        string outputDirectoryBase,
        string title,
        string displayTitle,
        string version,
        string author,
        TtsSettings ttsSettings,
        AddOn.Note? primaryNote,
        IReadOnlyCollection<AddOn.Note> additionalNotes,
        IReadOnlyDictionary<string, string> additionalProperties,
        IReadOnlyCollection<string>? interfaces,
        IReadOnlyDictionary<string, SoundFile> soundFiles,
        IReadOnlyDictionary<string, Func<AddOn, string>> fileFactories)
    {
        AddOnId = addOnId;
        _outputDirectoryBase = outputDirectoryBase;
        _title = title;
        _displayTitle = displayTitle;
        _version = version;
        _author = author;
        _ttsSettings = ttsSettings;
        _primaryNote = primaryNote;
        _additionalNotes = additionalNotes;
        _additionalProperties = additionalProperties;
        _interfaces = interfaces;
        _soundFiles = soundFiles;
        _fileFactories = fileFactories;
    }

    public string AddOnId { get; }

    public VoiceName? Voice => _ttsSettings.Voice;

    public IEnumerable<SoundFile> SoundFiles => _soundFiles.Values;

    public string SoundDirectory => Path.Combine(_outputDirectoryBase, _title.Replace(' ', '_'), "Sounds");

    public AddOn Finalize(IEnumerable<SoundFile> soundFiles)
    {
        Dictionary<string, SoundFile> resolved = soundFiles.ToDictionary(
            soundFile => soundFile.Key, StringComparer.OrdinalIgnoreCase);
        GuardAgainstConflictingRecordings(resolved.Values);

        return new AddOn(
            _outputDirectoryBase,
            _title,
            _displayTitle,
            _version,
            _author,
            _ttsSettings,
            _primaryNote,
            _additionalNotes,
            _additionalProperties,
            _interfaces,
            resolved,
            _fileFactories);
    }

    private static void GuardAgainstConflictingRecordings(IEnumerable<SoundFile> soundFiles)
    {
        IEnumerable<IGrouping<string, SoundFile>> perFile =
            soundFiles.GroupBy(f => f.FileName, StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, SoundFile> group in perFile)
        {
            SoundFile[] distinct = group
                .Distinct(SoundFileContentEqualityComparer.Default)
                .ToArray();

            if (distinct.Length > 1)
            {
                string keys = string.Join(", ", group.Select(f => $"'{f.Key}'"));
                throw new InvalidOperationException(
                    $"{group.Key} is claimed by entries that do not agree on what it says ({keys}).");
            }
        }
    }
}
