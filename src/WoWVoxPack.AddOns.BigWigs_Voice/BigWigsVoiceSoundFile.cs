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
        string[] words = text.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<XNode> xElementContent = words.Select((word, index) => new
            {
                Word = word + (index == words.Length - 1 ? "" : " "),
                WordIpa = word.Split('=').ElementAtOrDefault(1)
            })
            .Select(
                x => x.WordIpa is null
                    ? new XText(x.Word) as XNode
                    : new XElement("phoneme", new XAttribute("alphabet", "IPA"),
                        new XAttribute("ph", x.WordIpa), x.Word));

        return new XDocument(
            new XElement(
                "speak", xElementContent
            )).ToString().TrimEnd();
    }
}
