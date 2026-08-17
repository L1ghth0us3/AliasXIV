using AliasXIV.Models;
using AliasXIV.Services;
using Xunit;

namespace AliasXIV.Tests;

public class RulesImportExportTests
{
    private static ReplacementRule SampleRule()
        => new()
        {
            Finds = ["yes", "yea"],
            Replace = "qi",
            MatchMode = MatchMode.WholeWord,
            ChanceEnabled = true,
            ChancePercent = 15f,
            ChanceScope = ChanceScope.PerOccurrence,
        };

    [Fact]
    public void SerializeAndDeserializeRoundTrip()
    {
        var original = new List<ReplacementRule> { SampleRule() };
        var json = RulesImportExport.SerializeRules(original);

        Assert.True(RulesImportExport.TryDeserializeRules(json, out var imported, out var error), error);
        Assert.Single(imported);
        Assert.Equal(["yes", "yea"], imported[0].Finds);
        Assert.Equal("qi", imported[0].Replace);
        Assert.Equal(MatchMode.WholeWord, imported[0].MatchMode);
        Assert.True(imported[0].ChanceEnabled);
        Assert.Equal(15f, imported[0].ChancePercent);
        Assert.Equal(ChanceScope.PerOccurrence, imported[0].ChanceScope);
        Assert.NotEqual(Guid.Empty, imported[0].Id);
    }

    [Fact]
    public void InvalidJsonReturnsError()
    {
        Assert.False(RulesImportExport.TryDeserializeRules("{not json", out var rules, out var error));
        Assert.Empty(rules);
        Assert.NotNull(error);
    }

    [Fact]
    public void UnsupportedVersionReturnsError()
    {
        const string json = """{"version":99,"rules":[]}""";

        Assert.False(RulesImportExport.TryDeserializeRules(json, out var rules, out var error));
        Assert.Empty(rules);
        Assert.Contains("Unsupported", error, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyFileReturnsError()
    {
        Assert.False(RulesImportExport.TryDeserializeRules(string.Empty, out var rules, out var error));
        Assert.Empty(rules);
        Assert.NotNull(error);
    }

    [Fact]
    public void PerEntryScopeOnRuleIsNormalizedToPerMessage()
    {
        var rule = SampleRule();
        rule.ChanceScope = ChanceScope.PerEntry;
        var json = RulesImportExport.SerializeRules([rule]);

        Assert.True(RulesImportExport.TryDeserializeRules(json, out var imported, out _));
        Assert.Equal(ChanceScope.PerMessage, imported[0].ChanceScope);
    }
}
