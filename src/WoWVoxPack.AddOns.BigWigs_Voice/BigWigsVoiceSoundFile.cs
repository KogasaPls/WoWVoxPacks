using System.Xml.Linq;

using WoWVoxPack.TTS;

namespace WoWVoxPack.AddOns.BigWigs_Voice;

public class BigWigsVoiceSoundFile(string spellId, string spellName)
    : SoundFile($"{spellId}.ogg", ssml: GetSsml(spellName), displayName: spellName)
{
    public string SpellId { get; } = spellId;

    public string SpellName { get; } = spellName;

    private static string GetSsml(string text)
    {
        XNamespace ns = XNamespace.Get("http://www.w3.org/2001/10/synthesis");

        return new XDocument(
            new XElement(
                ns + "speak",
                new XAttribute("version", "1.0"),
                new XAttribute(XNamespace.Xml + "lang", "en"),
                text.Split([' '], StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Split('='))
                    .Select(x => new { Word = x.ElementAt(0) + " ", WordIpa = x.ElementAtOrDefault(1) })
                    .Select(
                        x => x.WordIpa == null
                            ? new XText(x.Word) as XNode
                            : new XElement(ns + "phoneme", new XAttribute("alphabet", "IPA"),
                                new XAttribute("ph", x.WordIpa), x.Word)))).ToString();
    }
}
