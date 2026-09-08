using Microsoft.Extensions.Logging;

using WoWVoxPack.AddOns;

namespace WoWVoxPack.TTS;

public sealed class PronunciationResolver(
    PronunciationCatalog catalog,
    ILogger<PronunciationResolver> logger)
{
    private readonly IReadOnlyDictionary<string, ExactNameRule> _exactNames = BuildExactNameLookup(catalog);
    private readonly IReadOnlyDictionary<string, PhraseRule> _phrases = BuildPhraseLookup(catalog);

    public IReadOnlyList<AddOn> Resolve(IEnumerable<AddOnDraft> source)
    {
        AddOnDraft[] drafts = source.ToArray();
        IReadOnlyDictionary<PronunciationIdentity, IReadOnlyList<Candidate>> upstream = HarvestUpstream(drafts);
        List<ResolvedDraft> resolved = drafts
            .Select(draft => new ResolvedDraft(draft, draft.SoundFiles
                .Select(sound => ResolveSound(draft.AddOnId, draft.Voice, sound, upstream)).ToArray()))
            .ToList();

        GuardExactNames(resolved);
        return resolved.Select(item => item.Draft.Finalize(item.Sounds)).ToList();
    }

    private static IReadOnlyDictionary<string, ExactNameRule> BuildExactNameLookup(
        PronunciationCatalog pronunciationCatalog)
    {
        Dictionary<string, ExactNameRule> lookup = new(StringComparer.Ordinal);
        foreach (ExactNameRule rule in pronunciationCatalog.ExactNames)
        {
            foreach (string name in new[] { rule.Name }.Concat(rule.Aliases ?? []))
            {
                lookup.Add(PronunciationCatalog.NormalizeExactName(name), rule);
            }
        }

        return lookup;
    }

    private static IReadOnlyDictionary<string, PhraseRule> BuildPhraseLookup(
        PronunciationCatalog pronunciationCatalog)
    {
        Dictionary<string, PhraseRule> lookup = new(StringComparer.OrdinalIgnoreCase);
        foreach (PhraseRule rule in pronunciationCatalog.Phrases)
        {
            lookup.Add(rule.Phrase, rule);
            foreach (string alias in rule.Aliases ?? [])
            {
                lookup.Add(alias, rule);
            }
        }

        return lookup;
    }

    private IReadOnlyDictionary<PronunciationIdentity, IReadOnlyList<Candidate>> HarvestUpstream(
        IEnumerable<AddOnDraft> drafts)
    {
        Dictionary<PronunciationIdentity, List<Candidate>> result = [];
        foreach (AddOnDraft draft in drafts)
        {
            foreach (SoundFile sound in draft.SoundFiles)
            {
                PronunciationIdentity identity = new(draft.Voice, Identity(sound));
                foreach (PronunciationHint hint in sound.ImportedPronunciations ?? [])
                {
                    PhraseRule? rule = FindPhraseRule(hint.Pronunciation.Phrase);
                    Candidate candidate = new(
                        rule?.Phrase ?? hint.Pronunciation.Phrase,
                        hint.Pronunciation.Phrase,
                        hint.Pronunciation.Ipa,
                        hint.Origin);
                    if (!result.TryGetValue(identity, out List<Candidate>? candidates))
                    {
                        candidates = [];
                        result.Add(identity, candidates);
                    }

                    candidates.Add(candidate);
                }
            }
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<Candidate>)pair.Value.Distinct().ToArray());
    }

    private SoundFile ResolveSound(
        string addOnId,
        VoiceName? voice,
        SoundFile sound,
        IReadOnlyDictionary<PronunciationIdentity, IReadOnlyList<Candidate>> upstream)
    {
        if (sound.Composition is not null)
        {
            return sound;
        }

        ExactNameRule? exact = FindExactRule(sound);
        string text = exact?.Text ?? sound.Text ?? sound.DisplayName;
        List<Candidate> candidates = [];

        foreach (Pronunciation pronunciation in sound.Pronunciations ?? [])
        {
            AddCandidate(candidates, text, pronunciation, "local:inline", addOnId, sound.Key);
        }

        foreach (Pronunciation pronunciation in exact?.Pronunciations ?? [])
        {
            AddCandidate(candidates, text, pronunciation, $"catalog:exact:{exact!.Name}", addOnId, sound.Key);
        }

        if (upstream.TryGetValue(new PronunciationIdentity(voice, Identity(sound)),
                out IReadOnlyList<Candidate>? imported))
        {
            foreach (Candidate candidate in imported)
            {
                AddCandidate(candidates, text,
                    new Pronunciation(candidate.MatchedPhrase, candidate.Ipa), candidate.Origin,
                    addOnId, sound.Key);
            }
        }

        foreach (PhraseRule rule in catalog.Phrases)
        {
            foreach (string spelling in new[] { rule.Phrase }.Concat(rule.Aliases ?? []))
            {
                string? actual = FindBoundedPhrase(text, spelling);
                if (actual is not null)
                {
                    candidates.Add(new Candidate(rule.Phrase, actual, rule.Ipa, $"catalog:phrase:{rule.Phrase}"));
                    break;
                }
            }
        }

        List<Pronunciation> pronunciations = [];
        foreach (IGrouping<string, Candidate> group in candidates.GroupBy(
                     candidate => candidate.CanonicalPhrase, StringComparer.OrdinalIgnoreCase))
        {
            Candidate[] distinct = group
                .DistinctBy(candidate => candidate.Ipa, StringComparer.Ordinal)
                .ToArray();
            PronunciationExceptionRule? exception = FindException(addOnId, sound.Key, group.Key,
                group.Select(candidate => candidate.MatchedPhrase));

            if (exception?.Suppress == true)
            {
                continue;
            }

            string ipa;
            if (exception?.Ipa is not null)
            {
                ipa = exception.Ipa;
            }
            else if (distinct.Length == 1)
            {
                ipa = distinct[0].Ipa;
            }
            else
            {
                string details = string.Join(", ", distinct.Select(candidate =>
                    $"{candidate.Ipa} ({candidate.Origin})"));
                throw new InvalidOperationException(
                    $"Conflicting pronunciations for '{group.Key}' in {addOnId}/{sound.Key}: {details}. " +
                    "Add a scoped pronunciation exception to choose deliberately.");
            }

            Candidate chosen = distinct[0];
            pronunciations.Add(new Pronunciation(chosen.MatchedPhrase, ipa));
            foreach (Candidate adopted in group.Where(candidate => candidate.Origin.StartsWith(
                         "upstream:", StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogInformation(
                    "Imported pronunciation {Phrase}={ImportedIpa} from {Origin}; " +
                    "effective IPA for {AddOn}/{Key} is {EffectiveIpa}",
                    adopted.MatchedPhrase, adopted.Ipa, adopted.Origin, addOnId, sound.Key, ipa);
            }
        }

        return Clone(sound, text, pronunciations);
    }

    private void AddCandidate(
        List<Candidate> candidates,
        string text,
        Pronunciation pronunciation,
        string origin,
        string addOnId,
        string key)
    {
        string? actual = FindBoundedPhrase(text, pronunciation.Phrase);
        if (actual is null)
        {
            throw new InvalidOperationException(
                $"Pronunciation phrase '{pronunciation.Phrase}' from {origin} does not occur in " +
                $"resolved text for {addOnId}/{key}: '{text}'.");
        }

        PhraseRule? rule = FindPhraseRule(pronunciation.Phrase);
        candidates.Add(new Candidate(rule?.Phrase ?? pronunciation.Phrase, actual, pronunciation.Ipa, origin));
    }

    private ExactNameRule? FindExactRule(SoundFile sound)
    {
        foreach (string? name in new[] { sound.PronunciationName, sound.DisplayName, sound.Text })
        {
            if (name is not null && _exactNames.TryGetValue(
                    PronunciationCatalog.NormalizeExactName(name), out ExactNameRule? rule))
            {
                return rule;
            }
        }

        return null;
    }

    private PhraseRule? FindPhraseRule(string phrase)
    {
        return _phrases.GetValueOrDefault(phrase);
    }

    private PronunciationExceptionRule? FindException(
        string addOnId,
        string key,
        string canonicalPhrase,
        IEnumerable<string> matchedPhrases)
    {
        HashSet<string> phrases = new(matchedPhrases.Append(canonicalPhrase), StringComparer.OrdinalIgnoreCase);
        return catalog.Exceptions.SingleOrDefault(rule =>
            rule.Addon.Equals(addOnId, StringComparison.OrdinalIgnoreCase) &&
            rule.Key.Equals(key, StringComparison.OrdinalIgnoreCase) &&
            phrases.Contains(rule.Phrase));
    }

    private string Identity(SoundFile sound)
    {
        return PronunciationCatalog.NormalizeExactName(sound.PronunciationName ?? sound.DisplayName);
    }

    private string? FindBoundedPhrase(string text, string phrase)
    {
        int start = 0;
        while (start <= text.Length - phrase.Length)
        {
            int index = text.IndexOf(phrase, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            int end = index + phrase.Length;
            bool leftBoundary = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            bool rightBoundary = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (leftBoundary && rightBoundary)
            {
                return text.Substring(index, phrase.Length);
            }

            start = index + 1;
        }

        return null;
    }

    private void GuardExactNames(IEnumerable<ResolvedDraft> drafts)
    {
        foreach (IGrouping<PronunciationIdentity, ResolvedSound> group in drafts
                     .SelectMany(draft => draft.Sounds.Select(sound => new ResolvedSound(draft.Draft, sound)))
                     .GroupBy(item => new PronunciationIdentity(
                         item.Draft.Voice, Identity(item.Sound))))
        {
            if (group.Select(item => BaseSpokenSignature(item.Sound))
                    .Distinct(StringComparer.Ordinal).Count() > 1)
            {
                ThrowExactNameConflict(group.Key.Name, group);
            }

            foreach (string phrase in group
                         .SelectMany(item => item.Sound.Pronunciations ?? [])
                         .Select(pronunciation => FindPhraseRule(pronunciation.Phrase)?.Phrase ?? pronunciation.Phrase)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                List<string?> unexplained = [];
                foreach (ResolvedSound item in group)
                {
                    Pronunciation? pronunciation = (item.Sound.Pronunciations ?? []).SingleOrDefault(candidate =>
                        (FindPhraseRule(candidate.Phrase)?.Phrase ?? candidate.Phrase)
                        .Equals(phrase, StringComparison.OrdinalIgnoreCase));
                    PronunciationExceptionRule? exception = FindException(
                        item.Draft.AddOnId,
                        item.Sound.Key,
                        phrase,
                        pronunciation is null ? [] : [pronunciation.Phrase]);
                    bool explained = exception is not null &&
                                     ((exception.Suppress && pronunciation is null) ||
                                      exception.Ipa == pronunciation?.Ipa);
                    if (!explained)
                    {
                        unexplained.Add(pronunciation?.Ipa);
                    }
                }

                if (unexplained.Distinct(StringComparer.Ordinal).Count() > 1)
                {
                    ThrowExactNameConflict(group.Key.Name, group);
                }
            }
        }
    }

    private void ThrowExactNameConflict(string name, IEnumerable<ResolvedSound> group)
    {
        string entries = string.Join(", ", group.Select(item =>
            $"{item.Draft.AddOnId}/{item.Sound.Key}").Distinct(StringComparer.OrdinalIgnoreCase));
        throw new InvalidOperationException(
            $"Equivalent pronunciation name '{name}' resolves to different spoken content ({entries}).");
    }

    private string BaseSpokenSignature(SoundFile sound)
    {
        string text = sound.Text is null ? string.Empty : PronunciationCatalog.NormalizeExactName(sound.Text);
        return $"{text}\n{sound.Ssml}";
    }

    private SoundFile Clone(
        SoundFile source,
        string text,
        IReadOnlyList<Pronunciation> pronunciations)
    {
        return new SoundFile(
            source.FileName,
            text,
            source.Ssml,
            source.DisplayName,
            source.FormattedDisplayName,
            pronunciations.OrderBy(item => item.Phrase, StringComparer.OrdinalIgnoreCase).ToArray())
        {
            ExplicitKey = source.ExplicitKey,
            CopyFromPath = source.CopyFromPath,
            PronunciationName = source.PronunciationName,
            ImportedPronunciations = source.ImportedPronunciations
        };
    }

    private sealed record Candidate(string CanonicalPhrase, string MatchedPhrase, string Ipa, string Origin);

    private sealed record ResolvedDraft(AddOnDraft Draft, SoundFile[] Sounds);

    private sealed record ResolvedSound(AddOnDraft Draft, SoundFile Sound);

    private sealed record PronunciationIdentity(VoiceName? Voice, string Name);
}
