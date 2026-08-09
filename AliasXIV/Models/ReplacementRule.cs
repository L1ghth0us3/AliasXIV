namespace AliasXIV.Models;

public sealed class ReplacementRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Legacy single-find field kept for deserializing older configs.
    /// Prefer <see cref="Finds"/>; use <see cref="GetEffectiveFinds"/> at runtime.
    /// </summary>
    public string Find { get; set; } = string.Empty;

    /// <summary>
    /// Find terms that all map to <see cref="Replace"/>. Edited in the UI as pipe-separated text.
    /// </summary>
    public List<string> Finds { get; set; } = [];

    public string Replace { get; set; } = string.Empty;

    public MatchMode MatchMode { get; set; } = MatchMode.WholeWord;

    public bool CaseSensitive { get; set; }

    public bool ChanceEnabled { get; set; }

    public float ChancePercent { get; set; } = 100f;

    /// <summary>
    /// Returns non-empty find terms: <see cref="Finds"/> when present, otherwise legacy <see cref="Find"/>.
    /// </summary>
    public IReadOnlyList<string> GetEffectiveFinds()
    {
        if (Finds is { Count: > 0 })
        {
            List<string>? filtered = null;
            foreach (var find in Finds)
            {
                if (string.IsNullOrEmpty(find))
                    continue;

                filtered ??= new List<string>(Finds.Count);
                filtered.Add(find);
            }

            if (filtered is { Count: > 0 })
                return filtered;
        }

        if (!string.IsNullOrEmpty(Find))
            return [Find];

        return [];
    }

    public static List<string> ParseFindsText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        // Keep empty segments so typing a trailing "|" does not collapse the field.
        return text.Split('|').Select(static segment => segment.Trim()).ToList();
    }

    public static string FormatFindsText(IReadOnlyList<string> finds)
    {
        if (finds.Count == 0)
            return string.Empty;

        return string.Join('|', finds);
    }
}
