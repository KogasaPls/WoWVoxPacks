using System.Collections.ObjectModel;
using System.Text.Json;

using Ardalis.GuardClauses;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns;

public class AddOn
{
    private readonly Note[] _additionalNotes;
    private readonly Dictionary<string, string> _additionalProperties;

    private readonly Dictionary<string, Lazy<string>?> _addOnFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _interfaces;

    private readonly string _outputDirectoryBase;

    private readonly Dictionary<string, SoundFile> _soundFiles = new(StringComparer.OrdinalIgnoreCase);


    protected AddOn(string outputDirectoryBase,
        AddOnSettings settings,
        TtsSettings ttsSettings)
    {
        _outputDirectoryBase = outputDirectoryBase;
        TtsSettings = ttsSettings;
        Title = Guard.Against.Null(settings.Title);
        DisplayTitle = settings.DisplayTitle ?? Title;
        Version = Guard.Against.Null(settings.Version);
        Author = Guard.Against.Null(settings.Author);
        PrimaryNote = settings.Notes is null ? null : new Note(null, settings.Notes);
        _interfaces = Guard.Against.NullOrEmpty(settings.Interfaces).ToArray();
        _additionalNotes = settings.AdditionalNotes?.Select(n => new Note(n.Key, n.Value)).ToArray() ?? [];
        _additionalProperties = new Dictionary<string, string>(
            settings.AdditionalProperties ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public TtsSettings TtsSettings { get; }
    public string AddOnDirectory => Path.Combine(_outputDirectoryBase, AddOnDirectoryName);

    public string SoundDirectory => Path.Combine(AddOnDirectory, SoundDirectoryName);
    public virtual string AddOnDirectoryName => Title.Replace(' ', '_');
    public string DisplayTitle { get; protected set; }

    public virtual string SoundDirectoryName => "Sounds";

    private string TocFileName => $"{AddOnDirectoryName}.toc";

    public string Title { get; }

    public string Version { get; }
    public string Author { get; }

    public IEnumerable<SoundFile> SoundFiles => _soundFiles.Values;

    private IEnumerable<SoundFile> NewOrModifiedSoundFiles =>
        SoundFiles.Where(f => !IsSameContentAsSoundFileInJson(f));

    public IEnumerable<SoundFile> SoundFilesToCreate =>
        SoundFiles.Where(f => !File.Exists(Path.Combine(SoundDirectory, f.FileName)))
            .UnionBy(NewOrModifiedSoundFiles, f => f.FileName);

    public IReadOnlyDictionary<string, string> AdditionalProperties => _additionalProperties.AsReadOnly();
    public Note? PrimaryNote { get; }
    public IReadOnlyCollection<Note> AdditionalNotes => _additionalNotes;
    public IReadOnlyCollection<string> Interfaces => _interfaces;
    public IReadOnlyCollection<string> Files => _addOnFiles.Keys;

    private ReadOnlyDictionary<string, SoundFile>? SoundFilesFromJson { get; set; }

    private string SoundFilesJsonPath => Path.Combine(AddOnDirectory, "SoundFiles.json");

    public async Task WriteSoundFilesJsonAsync(CancellationToken cancellationToken = default)
    {
        string? soundFilesJson =
            JsonSerializer.Serialize(SoundFiles.OrderBy(s => s.FileName).ToList(),
                SoundFileJsonContext.Default.ListSoundFile);
        await File.WriteAllTextAsync(SoundFilesJsonPath, soundFilesJson, cancellationToken);
    }

    public async Task LoadSoundFilesJsonAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SoundFilesJsonPath))
        {
            SoundFilesFromJson = new Dictionary<string, SoundFile>().AsReadOnly();
            return;
        }

        string json = await File.ReadAllTextAsync(SoundFilesJsonPath, cancellationToken);
        List<SoundFile> soundFiles =
            JsonSerializer.Deserialize<List<SoundFile>>(json, SoundFileJsonContext.Default.ListSoundFile) ??
            throw new Exception("Failed to deserialize sound files.");

        SoundFilesFromJson = soundFiles.ToDictionary(f => f.FileName, StringComparer.OrdinalIgnoreCase).AsReadOnly();
    }

    private bool IsSameContentAsSoundFileInJson(SoundFile soundFile)
    {
        ArgumentNullException.ThrowIfNull(SoundFilesFromJson);

        if (!SoundFilesFromJson.TryGetValue(soundFile.FileName, out SoundFile? soundFileFromJson))
        {
            return true;
        }

        return SoundFileContentEqualityComparer.Default.Equals(soundFile, soundFileFromJson);
    }

    protected void AddSoundFileJson(string filePath)
    {
        List<SoundFile> soundFiles =
            Guard.Against.Null(JsonSerializer.Deserialize<List<SoundFile>>(File.ReadAllText(filePath),
                SoundFileJsonContext.Default.ListSoundFile));

        AddSoundFiles(soundFiles, true);
    }

    protected void AddFile(string fileName, Func<AddOn, string> contentFactory)
    {
        _addOnFiles.Add(fileName, new Lazy<string>(() => contentFactory(this)));
    }

    protected void AddFile(string fileName)
    {
        _addOnFiles.Add(fileName, null);
    }

    protected void AddSoundFile(SoundFile soundFile, bool overwrite = false)
    {
        if (overwrite || !_soundFiles.ContainsKey(soundFile.FileName))
        {
            _soundFiles[soundFile.FileName] = soundFile;
        }
    }

    protected void AddSoundFiles(IEnumerable<SoundFile> soundFiles, bool overwrite = false)
    {
        foreach (SoundFile soundFile in soundFiles)
        {
            AddSoundFile(soundFile, overwrite);
        }
    }

    public virtual async Task WriteAllFilesAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AddOnDirectory);

        await WriteTocFileAsync(cancellationToken).ConfigureAwait(false);
        await WriteAddonFilesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteTocFileAsync(CancellationToken cancellationToken = default)
    {
        AddOnTocFile addOnTocFile = CreateTocFile();

        string tocFilePath = Path.Combine(AddOnDirectory, TocFileName);
        await File.WriteAllTextAsync(tocFilePath, (string?)addOnTocFile.TransformText(), cancellationToken);
    }


    private async Task WriteAddonFilesAsync(CancellationToken cancellationToken = default)
    {
        foreach ((string fileName, Lazy<string>? contentLazy) in _addOnFiles.Where(f => f.Value is not null))
        {
            string fileContent = contentLazy!.Value;
            string path = Path.Combine(AddOnDirectory, fileName);
            await File.WriteAllTextAsync(path, fileContent, cancellationToken);
        }
    }

    private AddOnTocFile CreateTocFile()
    {
        return new AddOnTocFile(this);
    }

    public record Note(string? LanguageCode, string Text);
}
