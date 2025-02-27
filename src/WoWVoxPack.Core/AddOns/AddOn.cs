using Ardalis.GuardClauses;

namespace WoWVoxPack.AddOns;

public class AddOn
{
    private readonly Note[] _additionalNotes;
    private readonly Dictionary<string, string> _additionalProperties;

    private readonly List<string> _files = [];

    private readonly Dictionary<string, Lazy<string>> _filesWithContentFactory = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _interfaces;

    public string OutputDirectoryName => Title.Replace(' ', '_');

    public AddOn(
        AddOnSettings settings)
    {
        Title = $"{Guard.Against.Null(settings.Title)}_{Guard.Against.Null(settings.TtsSettings?.Voice)}";
        Version = Guard.Against.Null(settings.Version);
        Author = Guard.Against.Null(settings.Author);
        PrimaryNote = settings.Notes is null ? null : new Note(null, settings.Notes);
        _interfaces = Guard.Against.NullOrEmpty(settings.Interfaces).ToArray();
        _additionalNotes = settings.AdditionalNotes?.Select(n => new Note(n.Key, n.Value)).ToArray() ?? [];
        _additionalProperties = new Dictionary<string, string>(
            settings.AdditionalProperties ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    private string TocFileName => $"{Title.Replace(' ', '_')}.toc";

    public string Title { get; }

    public string Version { get; }
    public string Author { get; }

    public IReadOnlyDictionary<string, string> AdditionalProperties => _additionalProperties.AsReadOnly();
    public Note? PrimaryNote { get; }
    public IReadOnlyCollection<Note> AdditionalNotes => _additionalNotes;
    public IReadOnlyCollection<string> Interfaces => _interfaces;
    public IReadOnlyCollection<string> Files => _filesWithContentFactory.Keys.Concat(_files).ToArray();

    protected void AddFile(string fileName, Func<AddOn, string> contentFactory)
    {
        _filesWithContentFactory.Add(fileName, new Lazy<string>(() => contentFactory(this)));
    }

    protected void AddFile(string fileName)
    {
        _files.Add(fileName);
    }

    public virtual async Task WriteAllFilesAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        await WriteTocFileAsync(outputDirectory, cancellationToken);

        foreach ((string fileName, Lazy<string> contentLazy) in _filesWithContentFactory)
        {
            string fileContent = contentLazy.Value;
            string path = Path.Combine(outputDirectory, fileName);
            await File.WriteAllTextAsync(path, fileContent, cancellationToken);
        }
    }

    private async Task WriteTocFileAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        AddOnTocFile addOnTocFile = CreateTocFile();

        Directory.CreateDirectory(outputDirectory);

        string tocFilePath = Path.Combine(outputDirectory, TocFileName);
        await File.WriteAllTextAsync(tocFilePath, (string?)addOnTocFile.TransformText(), cancellationToken);
    }

    private AddOnTocFile CreateTocFile()
    {
        return new AddOnTocFile(this);
    }

    public record Note(string? LanguageCode, string Text);
}
