using System.Buffers;
using System.Globalization;
using System.Text;
using AliasXIV.Models;

namespace AliasXIV.Services;

public sealed class ReplacementEngine
{
    private readonly record struct ReplacementMatch(
        int Start,
        int Length,
        string Replacement,
        int RulePriority);

    public string Transform(
        string input,
        IReadOnlyList<ReplacementRule> rules,
        bool evaluateChance = false,
        ChanceScope chanceScope = ChanceScope.PerMessage,
        Random? random = null)
    {
        if (string.IsNullOrEmpty(input) || rules.Count == 0)
            return input;

        var candidates = new List<ReplacementMatch>();
        var rng = evaluateChance ? random ?? Random.Shared : null;

        for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            var rule = rules[ruleIndex];
            if (!rule.Enabled)
                continue;

            var finds = rule.GetEffectiveFinds();
            if (finds.Count == 0)
                continue;

            var effectiveScope = chanceScope == ChanceScope.PerEntry
                ? rule.ChanceScope
                : chanceScope;

            if (evaluateChance && rule.ChanceEnabled && effectiveScope == ChanceScope.PerMessage)
            {
                if (rng!.NextDouble() * 100.0 >= rule.ChancePercent)
                    continue;
            }

            CollectMatches(input, rule, finds, ruleIndex, candidates, evaluateChance, effectiveScope, rng);
        }

        if (candidates.Count == 0)
            return input;

        candidates.Sort(static (a, b) =>
        {
            var startCmp = a.Start.CompareTo(b.Start);
            if (startCmp != 0)
                return startCmp;

            var lengthCmp = b.Length.CompareTo(a.Length);
            if (lengthCmp != 0)
                return lengthCmp;

            return a.RulePriority.CompareTo(b.RulePriority);
        });

        var accepted = new List<ReplacementMatch>(candidates.Count);
        var cursor = 0;
        foreach (var candidate in candidates)
        {
            if (candidate.Start < cursor)
                continue;

            accepted.Add(candidate);
            cursor = candidate.Start + candidate.Length;
        }

        if (accepted.Count == 0)
            return input;

        var builder = new StringBuilder(input.Length);
        var index = 0;
        foreach (var match in accepted)
        {
            if (match.Start > index)
                builder.Append(input, index, match.Start - index);

            builder.Append(match.Replacement);
            index = match.Start + match.Length;
        }

        if (index < input.Length)
            builder.Append(input, index, input.Length - index);

        return builder.ToString();
    }

    private static void CollectMatches(
        string input,
        ReplacementRule rule,
        IReadOnlyList<string> finds,
        int rulePriority,
        List<ReplacementMatch> candidates,
        bool evaluateChance,
        ChanceScope chanceScope,
        Random? rng)
    {
        var comparison = rule.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var rollPerOccurrence = evaluateChance
                                && rule.ChanceEnabled
                                && chanceScope == ChanceScope.PerOccurrence;

        foreach (var find in finds)
        {
            var findLength = find.Length;
            if (findLength == 0 || findLength > input.Length)
                continue;

            var searchFrom = 0;
            while (searchFrom <= input.Length - findLength)
            {
                var index = input.IndexOf(find, searchFrom, comparison);
                if (index < 0)
                    break;

                if (rule.MatchMode == MatchMode.WholeWord && !IsWholeWordMatch(input, index, findLength))
                {
                    searchFrom = index + 1;
                    continue;
                }

                if (rollPerOccurrence && rng!.NextDouble() * 100.0 >= rule.ChancePercent)
                {
                    searchFrom = index + 1;
                    continue;
                }

                candidates.Add(new ReplacementMatch(index, findLength, rule.Replace, rulePriority));
                searchFrom = index + 1;
            }
        }
    }

    private static bool IsWholeWordMatch(string input, int start, int length)
    {
        if (start > 0 && IsWordCharacter(input, start - 1))
            return false;

        var end = start + length;
        if (end < input.Length && IsWordCharacter(input, end))
            return false;

        return true;
    }

    /// <summary>
    /// Word characters are Unicode letters, Unicode numbers, or underscore.
    /// </summary>
    private static bool IsWordCharacter(string input, int index)
    {
        if ((uint)index >= (uint)input.Length)
            return false;

        if (char.IsLowSurrogate(input[index]))
        {
            if (index == 0 || !char.IsHighSurrogate(input[index - 1]))
                return false;
            index--;
        }

        if (Rune.DecodeFromUtf16(input.AsSpan(index), out var rune, out _) != OperationStatus.Done)
            return false;

        if (rune.Value == '_')
            return true;

        var category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.DecimalDigitNumber
            or UnicodeCategory.LetterNumber
            or UnicodeCategory.OtherNumber;
    }
}
