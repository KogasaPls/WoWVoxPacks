using WoWVoxPack.TTS;

namespace WoWVoxPack.UnitTests;

public class PronunciationCatalogTests
{
    [Fact]
    public void Load_ReadsEveryRuleKind()
    {
        string path = WriteJson("""
            {
              "ExactNames": [{"Name":"Axegrinder","Text":"Axe grinder"}],
              "Phrases": [{"Phrase":"Gul'dan","Ipa":"ɡʊlˈdɑn","Aliases":[]}],
              "Exceptions": [{
                "Addon":"BigWigs_Voice", "Key":"1248171", "Phrase":"Tear",
                "Suppress":true, "Reason":"Use the provider default for this recording."
              }]
            }
            """);

        PronunciationCatalog catalog = PronunciationCatalog.Load(path);

        Assert.Equal("Axe grinder", Assert.Single(catalog.ExactNames).Text);
        Assert.Equal("ɡʊlˈdɑn", Assert.Single(catalog.Phrases).Ipa);
        Assert.True(Assert.Single(catalog.Exceptions).Suppress);
    }

    [Fact]
    public void Load_RejectsAliasesThatNormalizeToAnotherExactName()
    {
        string path = WriteJson("""
            {
              "ExactNames": [
                {"Name":"Bomb Voyage!"},
                {"Name":"Another Name","Aliases":[" bomb   voyage "]}
              ],
              "Phrases": [], "Exceptions": []
            }
            """);

        Assert.Throws<InvalidOperationException>(() => PronunciationCatalog.Load(path));
    }

    private static string WriteJson(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"pronunciations-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
