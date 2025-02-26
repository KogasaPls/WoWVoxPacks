namespace WoWVoxPack.AddOns.BigWigs_Voice;

public record SpellListFile(string FileName)
{
    private const string BaseUrl = "https://github.com/BigWigsMods/BigWigs_Voice/raw/master/Tools/";
    private string? _content;

    public Uri Url => new(BaseUrl + FileName);

    public string Content => _content ?? throw new InvalidOperationException("Content not loaded");

    public async Task<string> GetContentAsync()
    {
        if (_content != null)
        {
            return _content;
        }

        using HttpClient httpClient = new();
        _content = await httpClient.GetStringAsync(Url);

        return _content;
    }
}
