using WoWVoxPack.AddOns.Callouts;

namespace WoWVoxPack.UnitTests;

public class CalloutPronunciationTests
{
    [Theory]
    [InlineData("Soak", "Soak")]
    [InlineData("MindControl", "Mind Control")]
    [InlineData("DropPool", "Drop Pool")]
    [InlineData("HealAbsorb", "Heal Absorb")]
    [InlineData("RunOut", "Run Out")]
    [InlineData("1", "One")]
    [InlineData("10", "Ten")]
    public void ToDisplayName_SplitsPascalCaseAndSpellsNumbers(string soundName, string expected) =>
        Assert.Equal(expected, CalloutPronunciation.ToDisplayName(soundName));

    [Theory]
    [InlineData("Soak", "soak.ogg")]
    [InlineData("Mind Control", "mind_control.ogg")]
    [InlineData("Yu'lon", "yu_lon.ogg")]
    public void ToFileName_LowercasesAndCollapsesSeparators(string displayName, string expected) =>
        Assert.Equal(expected, CalloutPronunciation.ToFileName(displayName));

    [Fact]
    public void LoadOverrides_ReadsACompatibilityFileName()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path,
                """{"Invoke Yu'lon, the Jade Serpent":{"FileName":"invoke_yulon_the_jade_serpent.ogg"}}""");

            IReadOnlyDictionary<string, PronunciationOverride> overrides =
                CalloutPronunciation.LoadOverrides(path);

            Assert.Equal(
                "invoke_yulon_the_jade_serpent.ogg",
                overrides["Invoke Yu'lon, the Jade Serpent"].FileName);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
