using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WoWVoxPack.TTS;

public sealed record PronunciationCatalog(
    IReadOnlyList<ExactNameRule> ExactNames,
    IReadOnlyList<PhraseRule> Phrases,
    IReadOnlyList<PronunciationExceptionRule> Exceptions)
{
    public static PronunciationCatalog Empty { get; } = new([], [], []);

    public static PronunciationCatalog Load(string path)
    {
        PronunciationCatalog catalog = JsonSerializer.Deserialize<PronunciationCatalog>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Pronunciation catalog is empty.");
        catalog.Validate();
        return catalog;
    }

    internal static string NormalizeExactName(string value)
    {
        string normalized = Regex.Replace(value.Trim(), @"\s+", " ");
        while (normalized.Length > 0 && IsPunctuation(normalized[^1]))
        {
            normalized = normalized[..^1].TrimEnd();
        }

        return normalized.ToUpperInvariant();
    }

    private void Validate()
    {
        HashSet<string> names = [];
        foreach (ExactNameRule rule in ExactNames)
        {
            RequireValue(rule.Name, "Exact-name rule name");
            foreach (string name in new[] { rule.Name }.Concat(rule.Aliases ?? []))
            {
                RequireValue(name, "Exact-name rule alias");
                if (!names.Add(NormalizeExactName(name)))
                {
                    throw new InvalidOperationException($"Duplicate normalized exact name or alias: {name}");
                }
            }

            foreach (Pronunciation pronunciation in rule.Pronunciations ?? [])
            {
                RequireValue(pronunciation.Phrase, $"Pronunciation phrase for {rule.Name}");
                RequireValue(pronunciation.Ipa, $"Pronunciation IPA for {rule.Name}");
            }
        }

        foreach (PhraseRule rule in Phrases)
        {
            RequireValue(rule.Phrase, "Phrase rule phrase");
            RequireValue(rule.Ipa, $"IPA for phrase {rule.Phrase}");
            foreach (string alias in rule.Aliases ?? [])
            {
                RequireValue(alias, $"Alias for phrase {rule.Phrase}");
            }
        }

        foreach (PronunciationExceptionRule rule in Exceptions)
        {
            RequireValue(rule.Addon, "Exception addon");
            RequireValue(rule.Key, "Exception key");
            RequireValue(rule.Phrase, "Exception phrase");
            RequireValue(rule.Reason, $"Exception reason for {rule.Addon}/{rule.Key}");
            if ((rule.Ipa is not null) == rule.Suppress)
            {
                throw new InvalidOperationException(
                    $"Exception {rule.Addon}/{rule.Key}/{rule.Phrase} must set exactly one of Ipa or Suppress.");
            }

            if (rule.Ipa is not null)
            {
                RequireValue(rule.Ipa, $"Exception IPA for {rule.Addon}/{rule.Key}");
            }
        }
    }

    private static void RequireValue(string value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{description} cannot be empty.");
        }
    }

    private static bool IsPunctuation(char value)
    {
        return CharUnicodeInfo.GetUnicodeCategory(value) is
            UnicodeCategory.ConnectorPunctuation or
            UnicodeCategory.DashPunctuation or
            UnicodeCategory.OpenPunctuation or
            UnicodeCategory.ClosePunctuation or
            UnicodeCategory.InitialQuotePunctuation or
            UnicodeCategory.FinalQuotePunctuation or
            UnicodeCategory.OtherPunctuation;
    }
}
