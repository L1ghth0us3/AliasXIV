namespace AliasXIV.Models;

public sealed class ReplacementRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool Enabled { get; set; } = true;

    public string Find { get; set; } = string.Empty;

    public string Replace { get; set; } = string.Empty;

    public MatchMode MatchMode { get; set; } = MatchMode.WholeWord;

    public bool CaseSensitive { get; set; }
}
