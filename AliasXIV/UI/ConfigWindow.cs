using System.Numerics;
using AliasXIV.Models;
using AliasXIV.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AliasXIV.UI;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly ReplacementEngine replacementEngine;
    private string previewInput = "Today is a nice day";

    public ConfigWindow(Configuration configuration, ReplacementEngine replacementEngine)
        : base("AliasXIV###AliasXIVConfig")
    {
        this.configuration = configuration;
        this.replacementEngine = replacementEngine;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(640, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var enabled = configuration.Enabled;
        if (ImGui.Checkbox("Enable AliasXIV", ref enabled))
        {
            configuration.Enabled = enabled;
            configuration.Save();
        }

        var applyCommands = configuration.ApplyToSimpleChatCommands;
        if (ImGui.Checkbox("Apply replacements to slash-command payloads (all channels; excludes /tell /reply)", ref applyCommands))
        {
            configuration.ApplyToSimpleChatCommands = applyCommands;
            configuration.Save();
        }

        if (ImGui.CollapsingHeader("Example preview"))
        {
            ImGui.Indent();
            DrawPreview();
            ImGui.Unindent();
        }

        DrawValidationWarnings();

        if (ImGui.Button("+ Add Rule"))
        {
            configuration.Rules.Add(new ReplacementRule());
            configuration.Save();
        }

        // Fill all remaining window height with the rules table.
        var remaining = ImGui.GetContentRegionAvail();
        DrawRuleTable(Math.Max(120f, remaining.Y));
    }

    private void DrawRuleTable(float height)
    {
        var flags = ImGuiTableFlags.Borders
                    | ImGuiTableFlags.RowBg
                    | ImGuiTableFlags.SizingStretchProp
                    | ImGuiTableFlags.ScrollY
                    | ImGuiTableFlags.Resizable
                    | ImGuiTableFlags.ScrollX;

        if (!ImGui.BeginTable("##AliasXIVRulesV4", 8, flags, new Vector2(-1, height)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("Find", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Replace With", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Match", ImGuiTableColumnFlags.WidthFixed, 120f);
        ImGui.TableSetupColumn("Case Sensitive", ImGuiTableColumnFlags.WidthFixed, 110f);
        ImGui.TableSetupColumn("Chance", ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("%", ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableSetupColumn("Delete", ImGuiTableColumnFlags.WidthFixed, 50f);
        ImGui.TableHeadersRow();

        var rules = configuration.Rules;
        var removeIndex = -1;

        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            ImGui.PushID(rule.Id.ToString());

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            var ruleEnabled = rule.Enabled;
            if (ImGui.Checkbox("##enabled", ref ruleEnabled))
            {
                rule.Enabled = ruleEnabled;
                configuration.Save();
            }

            ImGui.TableSetColumnIndex(1);
            ImGui.SetNextItemWidth(-float.Epsilon);
            var find = rule.Find;
            if (ImGui.InputText("##find", ref find, 512))
            {
                rule.Find = find;
                configuration.Save();
            }

            ImGui.TableSetColumnIndex(2);
            ImGui.SetNextItemWidth(-float.Epsilon);
            var replace = rule.Replace;
            if (ImGui.InputText("##replace", ref replace, 512))
            {
                rule.Replace = replace;
                configuration.Save();
            }

            ImGui.TableSetColumnIndex(3);
            ImGui.SetNextItemWidth(-float.Epsilon);
            var matchIndex = rule.MatchMode == MatchMode.WholeWord ? 0 : 1;
            if (ImGui.Combo("##match", ref matchIndex, "Whole word\0Substring\0"))
            {
                rule.MatchMode = matchIndex == 0 ? MatchMode.WholeWord : MatchMode.Substring;
                configuration.Save();
            }

            ImGui.TableSetColumnIndex(4);
            var caseSensitive = rule.CaseSensitive;
            if (ImGui.Checkbox("##case", ref caseSensitive))
            {
                rule.CaseSensitive = caseSensitive;
                configuration.Save();
            }

            ImGui.TableSetColumnIndex(5);
            var chanceEnabled = rule.ChanceEnabled;
            if (ImGui.Checkbox("##chance", ref chanceEnabled))
            {
                rule.ChanceEnabled = chanceEnabled;
                configuration.Save();
            }

            ImGui.TableSetColumnIndex(6);
            ImGui.SetNextItemWidth(-float.Epsilon);
            var chancePercent = rule.ChancePercent;
            if (!rule.ChanceEnabled)
                ImGui.BeginDisabled();
            if (ImGui.InputFloat("##chancePercent", ref chancePercent, 0f, 0f, "%.0f"))
            {
                rule.ChancePercent = Math.Clamp(chancePercent, 0f, 100f);
                configuration.Save();
            }
            if (!rule.ChanceEnabled)
                ImGui.EndDisabled();

            ImGui.TableSetColumnIndex(7);
            if (ImGui.Button("X"))
                removeIndex = i;

            ImGui.PopID();
        }

        ImGui.EndTable();

        if (removeIndex >= 0)
        {
            rules.RemoveAt(removeIndex);
            configuration.Save();
        }
    }

    private void DrawPreview()
    {
        ImGui.TextUnformatted("Test message:");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##previewInput", ref previewInput, 1024);

        var previewOutput = replacementEngine.Transform(previewInput, configuration.Rules);
        ImGui.TextUnformatted("Would send:");
        ImGui.TextWrapped(previewOutput);
    }

    private void DrawValidationWarnings()
    {
        var hasEmptyFind = configuration.Rules.Any(r => r.Enabled && string.IsNullOrEmpty(r.Find));
        if (hasEmptyFind)
            ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), "Warning: enabled rules with empty Find are ignored.");

        var duplicates = configuration.Rules
            .Where(r => !string.IsNullOrEmpty(r.Find))
            .GroupBy(r => (r.Find, r.MatchMode, r.CaseSensitive), StringTupleComparer.Instance)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.Find)
            .Distinct()
            .ToList();

        if (duplicates.Count > 0)
        {
            ImGui.TextColored(
                new Vector4(1f, 0.7f, 0.2f, 1f),
                $"Warning: duplicate matching semantics for: {string.Join(", ", duplicates)}");
        }
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Find, MatchMode MatchMode, bool CaseSensitive)>
    {
        public static readonly StringTupleComparer Instance = new();

        public bool Equals(
            (string Find, MatchMode MatchMode, bool CaseSensitive) x,
            (string Find, MatchMode MatchMode, bool CaseSensitive) y)
        {
            var comparison = x.CaseSensitive || y.CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            return x.MatchMode == y.MatchMode
                   && x.CaseSensitive == y.CaseSensitive
                   && string.Equals(x.Find, y.Find, comparison);
        }

        public int GetHashCode((string Find, MatchMode MatchMode, bool CaseSensitive) obj)
        {
            var findHash = obj.CaseSensitive
                ? StringComparer.Ordinal.GetHashCode(obj.Find)
                : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Find);

            return HashCode.Combine(findHash, obj.MatchMode, obj.CaseSensitive);
        }
    }
}
