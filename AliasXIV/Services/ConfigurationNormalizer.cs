using AliasXIV.Models;

namespace AliasXIV.Services;

public static class ConfigurationNormalizer
{
    public static void Normalize(Configuration configuration)
    {
        configuration.Rules ??= [];
        configuration.EnabledChannels ??= [];

        if (configuration.Version < 2)
        {
            configuration.EnabledChannels = [];
            configuration.Version = 2;
        }
        else
        {
            configuration.EnabledChannels = configuration.EnabledChannels
                .Where(OutgoingChatChannelCatalog.IsDefined)
                .Distinct()
                .ToList();
        }

        NormalizeRules(configuration.Rules);
    }

    public static void NormalizeRules(IList<ReplacementRule> rules)
    {
        foreach (var rule in rules)
        {
            rule.Find ??= string.Empty;
            rule.Replace ??= string.Empty;
            rule.Finds ??= [];
            rule.ChancePercent = Math.Clamp(rule.ChancePercent, 0f, 100f);
            if (rule.Id == Guid.Empty)
                rule.Id = Guid.NewGuid();

            if (rule.ChanceScope == ChanceScope.PerEntry)
                rule.ChanceScope = ChanceScope.PerMessage;

            if (rule.Finds.Count == 0 && !string.IsNullOrEmpty(rule.Find))
                rule.Finds.Add(rule.Find);

            for (var i = rule.Finds.Count - 1; i >= 0; i--)
            {
                var find = rule.Finds[i]?.Trim() ?? string.Empty;
                if (find.Length == 0)
                    rule.Finds.RemoveAt(i);
                else
                    rule.Finds[i] = find;
            }

            rule.Find = string.Empty;
        }
    }
}
