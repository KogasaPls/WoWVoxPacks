using System.Text.Json;

using Ardalis.GuardClauses;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns;

public sealed class AddOnBuilder(AddOnSettings settings, TtsSettings ttsSettings)
{
    private readonly Dictionary<string, SoundFile> _soundFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<AddOn, string>> _fileFactories = new(StringComparer.OrdinalIgnoreCase);
    private string? _title;
    private string? _displayTitle;

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
        if (overwrite || !_soundFiles.ContainsKey(soundFile.DisplayName))
        {
            _soundFiles[soundFile.DisplayName] = soundFile;
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

    /// <summary>
    /// Like <see cref="LoadSoundFileJson"/>, but backfills SSML phoneme markup for entries
    /// whose <see cref="SoundFile.Text"/> contains an IPA escape ('=') and has no explicit
    /// <see cref="SoundFile.Ssml"/> set. Shared by addons (SharedMedia_Abilities, ExBoss)
    /// whose sound-file JSON manifests use this convention.
    /// </summary>
    public static List<SoundFile> LoadSoundFileJsonWithSsmlFallback(string filePath)
    {
        return LoadSoundFileJson(filePath).Select(RewriteSsmlFallback).ToList();
    }

    private static SoundFile RewriteSsmlFallback(SoundFile soundFile)
    {
        return soundFile.Ssml is null && soundFile.Text?.Contains('=') == true
            ? new SoundFile(soundFile.FileName, ssml: SoundFile.GetSsml(soundFile.Text),
                displayName: soundFile.DisplayName, formattedDisplayName: soundFile.FormattedDisplayName)
            : soundFile;
    }

    public AddOnBuilder AddFile(string fileName, Func<AddOn, string> contentFactory)
    {
        _fileFactories.Add(fileName, contentFactory);
        return this;
    }

    public AddOn Build(string outputDirectoryBase)
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

        return new AddOn(
            outputDirectoryBase,
            title,
            displayTitle,
            version,
            author,
            ttsSettings,
            primaryNote,
            additionalNotes,
            additionalProperties,
            new Dictionary<string, SoundFile>(_soundFiles, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, Func<AddOn, string>>(_fileFactories, StringComparer.OrdinalIgnoreCase));
    }
}
