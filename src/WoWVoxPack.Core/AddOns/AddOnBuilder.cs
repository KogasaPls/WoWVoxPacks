using System.Text.Json;

using Ardalis.GuardClauses;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns;

public sealed class AddOnBuilder(AddOnSettings settings, TtsSettings ttsSettings, string? addOnId = null)
{
    private readonly Dictionary<string, SoundFile> _soundFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<AddOn, string>> _fileFactories = new(StringComparer.OrdinalIgnoreCase);
    private string? _title;
    private string? _displayTitle;

    /// <summary>
    /// Deserializes a list of <see cref="SoundFile"/> from a JSON manifest file. Exposed
    /// statically so callers can cache the result across repeated builds of the same addon
    /// (e.g. once per voice in the build matrix) instead of re-reading the file from disk
    /// every time.
    /// </summary>
    public static List<SoundFile> LoadSoundFileJson(string filePath)
    {
        return Guard.Against.Null(
            JsonSerializer.Deserialize<List<SoundFile>>(File.ReadAllText(filePath),
                SoundFileJsonContext.Default.ListSoundFile));
    }

    public AddOnBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public AddOnBuilder WithDisplayTitle(string displayTitle)
    {
        _displayTitle = displayTitle;
        return this;
    }

    public AddOnBuilder AddSoundFile(SoundFile soundFile, bool overwrite = false)
    {
        if (overwrite || !_soundFiles.ContainsKey(soundFile.Key))
        {
            _soundFiles[soundFile.Key] = soundFile;
        }

        return this;
    }

    public AddOnBuilder AddSoundFiles(IEnumerable<SoundFile> soundFiles, bool overwrite = false)
    {
        foreach (SoundFile soundFile in soundFiles)
        {
            AddSoundFile(soundFile, overwrite);
        }

        return this;
    }

    public AddOnBuilder AddSoundFileJson(string filePath, bool overwrite = true)
    {
        return AddSoundFiles(LoadSoundFileJson(filePath), overwrite);
    }

    public AddOnBuilder AddFile(string fileName, Func<AddOn, string> contentFactory)
    {
        _fileFactories.Add(fileName, contentFactory);
        return this;
    }

    public AddOn Build(string outputDirectoryBase)
    {
        AddOnDraft draft = BuildDraft(outputDirectoryBase);
        return draft.Finalize(draft.SoundFiles);
    }

    public AddOnDraft BuildDraft(string outputDirectoryBase)
    {
        string title = Guard.Against.NullOrWhiteSpace(_title ?? settings.Title);
        string displayTitle = _displayTitle ?? settings.DisplayTitle ?? title;
        string version = Guard.Against.NullOrWhiteSpace(settings.Version);
        string author = Guard.Against.NullOrWhiteSpace(settings.Author);
        AddOn.Note? primaryNote = settings.Notes is null ? null : new AddOn.Note(null, settings.Notes);
        AddOn.Note[] additionalNotes =
            settings.AdditionalNotes?.Select(n => new AddOn.Note(n.Key, n.Value)).ToArray() ?? [];
        Dictionary<string, string> additionalProperties = new(
            settings.AdditionalProperties ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);

        return new AddOnDraft(
            Guard.Against.NullOrWhiteSpace(addOnId ?? settings.Title),
            outputDirectoryBase,
            title,
            displayTitle,
            version,
            author,
            ttsSettings,
            primaryNote,
            additionalNotes,
            additionalProperties,
            settings.Interfaces,
            new Dictionary<string, SoundFile>(_soundFiles, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, Func<AddOn, string>>(_fileFactories, StringComparer.OrdinalIgnoreCase));
    }
}
