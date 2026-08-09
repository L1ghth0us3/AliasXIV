using AliasXIV.Models;
using AliasXIV.Services;
using Xunit;

namespace AliasXIV.Tests;

public class ReplacementEngineTests
{
    private readonly ReplacementEngine engine = new();

    private static ReplacementRule Rule(
        string find,
        string replace,
        MatchMode mode = MatchMode.WholeWord,
        bool caseSensitive = false,
        bool enabled = true)
        => new()
        {
            Find = find,
            Replace = replace,
            MatchMode = mode,
            CaseSensitive = caseSensitive,
            Enabled = enabled,
        };

    private static ReplacementRule MultiFindRule(
        string[] finds,
        string replace,
        MatchMode mode = MatchMode.WholeWord,
        bool caseSensitive = false,
        bool enabled = true)
        => new()
        {
            Finds = finds.ToList(),
            Replace = replace,
            MatchMode = mode,
            CaseSensitive = caseSensitive,
            Enabled = enabled,
        };

    [Fact]
    public void SimpleReplacement()
    {
        var rules = new[] { Rule("nice", "bad") };
        Assert.Equal("Today is a bad day", engine.Transform("Today is a nice day", rules));
    }

    [Fact]
    public void RepeatedReplacement()
    {
        var rules = new[] { Rule("nice", "bad") };
        Assert.Equal("bad bad bad", engine.Transform("nice nice nice", rules));
    }

    [Fact]
    public void WholeWordBoundary()
    {
        var rules = new[] { Rule("nice", "bad") };
        Assert.Equal("bad nicely niceness", engine.Transform("nice nicely niceness", rules));
    }

    [Fact]
    public void PunctuationBoundaries()
    {
        var rules = new[] { Rule("nice", "bad") };
        Assert.Equal("bad, bad! (bad)", engine.Transform("nice, nice! (nice)", rules));
    }

    [Fact]
    public void CaseInsensitive()
    {
        var rules = new[] { Rule("nice", "bad", caseSensitive: false) };
        Assert.Equal("bad bad bad", engine.Transform("nice Nice NICE", rules));
    }

    [Fact]
    public void CaseSensitive()
    {
        var rules = new[] { Rule("nice", "bad", caseSensitive: true) };
        Assert.Equal("bad Nice NICE", engine.Transform("nice Nice NICE", rules));
    }

    [Fact]
    public void PhraseReplacement()
    {
        var rules = new[] { Rule("very nice", "very bad") };
        Assert.Equal("That was very bad.", engine.Transform("That was very nice.", rules));
    }

    [Fact]
    public void NoCascading()
    {
        var rules = new[]
        {
            Rule("nice", "bad"),
            Rule("bad", "awful"),
        };
        Assert.Equal("bad awful", engine.Transform("nice bad", rules));
    }

    [Fact]
    public void LongestSamePositionMatchWins()
    {
        var rules = new[]
        {
            Rule("nice", "bad"),
            Rule("nice day", "awful evening"),
        };
        Assert.Equal("awful evening", engine.Transform("nice day", rules));
    }

    [Fact]
    public void EmptyReplacement()
    {
        var rules = new[] { Rule("really", string.Empty) };
        Assert.Equal("I  like it", engine.Transform("I really like it", rules));
    }

    [Fact]
    public void EmptyFindIsIgnored()
    {
        var rules = new[] { Rule(string.Empty, "bad") };
        Assert.Equal("nice", engine.Transform("nice", rules));
    }

    [Fact]
    public void DisabledRuleIsIgnored()
    {
        var rules = new[] { Rule("nice", "bad", enabled: false) };
        Assert.Equal("nice", engine.Transform("nice", rules));
    }

    [Fact]
    public void SubstringMode()
    {
        var rules = new[] { Rule("cat", "dog", MatchMode.Substring) };
        Assert.Equal("dogs", engine.Transform("cats", rules));
        Assert.Equal("condogenate", engine.Transform("concatenate", rules));
    }

    [Fact]
    public void UnicodeAndEmojiSurroundingMatch()
    {
        var rules = new[] { Rule("café", "bistro") };
        Assert.Equal("bistro ☕ Straße", engine.Transform("café ☕ Straße", rules));
    }

    [Fact]
    public void EmojiDoesNotBlockWholeWordMatch()
    {
        var rules = new[] { Rule("nice", "bad") };
        Assert.Equal("😀 bad 😀", engine.Transform("😀 nice 😀", rules));
    }

    [Fact]
    public void JapaneseWholeWordUsesSubstringFriendlyBoundaries()
    {
        var rules = new[] { Rule("猫", "犬", MatchMode.Substring) };
        Assert.Equal("犬です", engine.Transform("猫です", rules));
    }

    [Fact]
    public void IdenticalResultUnchangedReferenceEqualityNotRequired()
    {
        var rules = new[] { Rule("nice", "nice") };
        Assert.Equal("nice day", engine.Transform("nice day", rules));
    }

    [Fact]
    public void EarlierRuleWinsWhenLengthEqual()
    {
        var rules = new[]
        {
            Rule("nice", "first"),
            Rule("nice", "second"),
        };
        Assert.Equal("first", engine.Transform("nice", rules));
    }

    [Fact]
    public void UnderscoreIsWordCharacter()
    {
        var rules = new[] { Rule("nice", "bad") };
        Assert.Equal("nice_day", engine.Transform("nice_day", rules));
    }

    [Fact]
    public void ChanceDisabledAlwaysAppliesEvenWhenPercentIsZero()
    {
        var rule = Rule("nice", "bad");
        rule.ChanceEnabled = false;
        rule.ChancePercent = 0f;

        Assert.Equal("bad", engine.Transform("nice", [rule], evaluateChance: true));
    }

    [Fact]
    public void ChanceEnabledAtZeroPercentNeverApplies()
    {
        var rule = Rule("nice", "bad");
        rule.ChanceEnabled = true;
        rule.ChancePercent = 0f;

        Assert.Equal("nice", engine.Transform("nice", [rule], evaluateChance: true));
    }

    [Fact]
    public void ChanceEnabledAtOneHundredPercentAlwaysApplies()
    {
        var rule = Rule("nice", "bad");
        rule.ChanceEnabled = true;
        rule.ChancePercent = 100f;

        Assert.Equal("bad", engine.Transform("nice", [rule], evaluateChance: true));
    }

    [Fact]
    public void ChanceRollBelowPercentAppliesRule()
    {
        var rule = Rule("nice", "bad");
        rule.ChanceEnabled = true;
        rule.ChancePercent = 50f;
        var random = new SequenceRandom(0.49);

        Assert.Equal("bad", engine.Transform("nice", [rule], evaluateChance: true, random));
    }

    [Fact]
    public void ChanceRollAtOrAbovePercentSkipsRule()
    {
        var rule = Rule("nice", "bad");
        rule.ChanceEnabled = true;
        rule.ChancePercent = 50f;
        var random = new SequenceRandom(0.50);

        Assert.Equal("nice", engine.Transform("nice", [rule], evaluateChance: true, random));
    }

    [Fact]
    public void EvaluateChanceFalseIgnoresChanceFields()
    {
        var rule = Rule("nice", "bad");
        rule.ChanceEnabled = true;
        rule.ChancePercent = 0f;

        Assert.Equal("bad", engine.Transform("nice", [rule], evaluateChance: false));
    }

    [Fact]
    public void ChanceIsRolledOncePerRuleForAllMatches()
    {
        var rule = Rule("nice", "bad");
        rule.ChanceEnabled = true;
        rule.ChancePercent = 50f;
        var random = new SequenceRandom(0.10);

        Assert.Equal("bad bad bad", engine.Transform("nice nice nice", [rule], evaluateChance: true, random));
    }

    [Fact]
    public void MultiFindReplacesAllTermsWithSameReplacement()
    {
        var rules = new[] { MultiFindRule(["yes", "yea"], "qi") };
        Assert.Equal("qi qi please", engine.Transform("yes yea please", rules));
    }

    [Fact]
    public void MultiFindLongestMatchWinsWithinSameRule()
    {
        var rules = new[] { MultiFindRule(["nice", "nice day"], "qi") };
        Assert.Equal("qi", engine.Transform("nice day", rules));
    }

    [Fact]
    public void MultiFindChanceIsRolledOnceForAllFinds()
    {
        var rule = MultiFindRule(["yes", "yea"], "qi");
        rule.ChanceEnabled = true;
        rule.ChancePercent = 50f;
        var random = new SequenceRandom(0.10);

        Assert.Equal("qi qi", engine.Transform("yes yea", [rule], evaluateChance: true, random));
    }

    [Fact]
    public void MultiFindChanceMissSkipsAllFinds()
    {
        var rule = MultiFindRule(["yes", "yea"], "qi");
        rule.ChanceEnabled = true;
        rule.ChancePercent = 50f;
        var random = new SequenceRandom(0.50);

        Assert.Equal("yes yea", engine.Transform("yes yea", [rule], evaluateChance: true, random));
    }

    [Fact]
    public void LegacySingleFindStillWorksViaEffectiveFinds()
    {
        var rules = new[] { Rule("yes", "qi") };
        Assert.Equal("qi", engine.Transform("yes", rules));
        Assert.Equal(["yes"], rules[0].GetEffectiveFinds());
    }

    [Fact]
    public void ParseAndFormatFindsTextRoundTrips()
    {
        Assert.Equal(["yes", "yea"], ReplacementRule.ParseFindsText("yes|yea"));
        Assert.Equal("yes|yea", ReplacementRule.FormatFindsText(["yes", "yea"]));
        Assert.Equal(["yes", ""], ReplacementRule.ParseFindsText("yes|"));
        Assert.Equal("yes|", ReplacementRule.FormatFindsText(["yes", ""]));
        Assert.Empty(ReplacementRule.ParseFindsText(""));
        Assert.DoesNotContain(
            ReplacementRule.ParseFindsText("|||"),
            static s => s.Length > 0);
    }

    private sealed class SequenceRandom : Random
    {
        private readonly Queue<double> values;

        public SequenceRandom(params double[] sequence)
        {
            values = new Queue<double>(sequence);
        }

        public override double NextDouble() => values.Dequeue();
    }
}
