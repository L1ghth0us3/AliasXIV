using System.Text.Json;
using System.Text.Json.Serialization;
using AliasXIV.Models;

namespace AliasXIV.Services;

public sealed class RulesImportExport
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public sealed class RulesExportFile
    {
        public int Version { get; set; } = CurrentVersion;

        public List<ReplacementRule> Rules { get; set; } = [];
    }

    public static string SerializeRules(IReadOnlyList<ReplacementRule> rules)
    {
        var export = new RulesExportFile
        {
            Version = CurrentVersion,
            Rules = rules.Select(CloneRule).ToList(),
        };

        return JsonSerializer.Serialize(export, JsonOptions);
    }

    public static bool TryDeserializeRules(string json, out List<ReplacementRule> rules, out string? error)
    {
        rules = [];
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "File is empty.";
            return false;
        }

        RulesExportFile? export;
        try
        {
            export = JsonSerializer.Deserialize<RulesExportFile>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return false;
        }

        if (export is null)
        {
            error = "File did not contain any data.";
            return false;
        }

        if (export.Version != CurrentVersion)
        {
            error = $"Unsupported rules file version {export.Version}. Expected {CurrentVersion}.";
            return false;
        }

        if (export.Rules is null)
        {
            error = "Rules list is missing.";
            return false;
        }

        rules = export.Rules.Select(CloneRule).ToList();
        ConfigurationNormalizer.NormalizeRules(rules);
        return true;
    }

    public static void ExportToFile(IReadOnlyList<ReplacementRule> rules, string path)
    {
        var json = SerializeRules(rules);
        File.WriteAllText(path, json);
    }

    public static bool TryImportFromFile(string path, out List<ReplacementRule> rules, out string? error)
    {
        rules = [];
        error = null;

        try
        {
            if (!File.Exists(path))
            {
                error = "File not found.";
                return false;
            }

            var json = File.ReadAllText(path);
            return TryDeserializeRules(json, out rules, out error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static ReplacementRule CloneRule(ReplacementRule rule)
        => new()
        {
            Id = Guid.NewGuid(),
            Enabled = rule.Enabled,
            Finds = rule.Finds?.ToList() ?? [],
            Replace = rule.Replace ?? string.Empty,
            MatchMode = rule.MatchMode,
            CaseSensitive = rule.CaseSensitive,
            ChanceEnabled = rule.ChanceEnabled,
            ChancePercent = rule.ChancePercent,
            ChanceScope = rule.ChanceScope == ChanceScope.PerEntry
                ? ChanceScope.PerMessage
                : rule.ChanceScope,
        };
}
