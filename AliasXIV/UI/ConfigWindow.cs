using System.Numerics;
using AliasXIV.Models;
using AliasXIV.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace AliasXIV.UI;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly ReplacementEngine replacementEngine;
    private readonly Action openSettings;
    private string previewInput = "Today is a nice day";
    private MatchMode activeMatchTab = MatchMode.WholeWord;

    public ConfigWindow(
        Configuration configuration,
        ReplacementEngine replacementEngine,
        Action openSettings)
        : base("AliasXIV###AliasXIVConfig")
    {
        this.configuration = configuration;
        this.replacementEngine = replacementEngine;
        this.openSettings = openSettings;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(640, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        TitleBarButtons =
        [
            new TitleBarButton
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new Vector2(2, 1),
                ShowTooltip = () => ImGui.SetTooltip("Settings"),
                Click = _ => openSettings(),
            },
        ];
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        if (ImGui.CollapsingHeader("Example preview"))
        {
            ImGui.Indent();
            DrawPreview();
            ImGui.Unindent();
        }

        DrawValidationWarnings();

        ImGui.TextWrapped(
            "Find tip: separate multiple words with | to replace them all with the same text (e.g. yes|yea → qi).");

        if (configuration.ChanceScope == ChanceScope.PerEntry)
        {
            ImGui.TextWrapped(
                "Per-entry chance scope is enabled in settings. Use the Scope column to choose per message or per occurrence for each rule.");
        }

        if (ImGui.BeginTabBar("##AliasXIVMatchTabs"))
        {
            if (ImGui.BeginTabItem("Whole Word"))
            {
                activeMatchTab = MatchMode.WholeWord;
                DrawActiveTabContents();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Substring"))
            {
                activeMatchTab = MatchMode.Substring;
                DrawActiveTabContents();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawActiveTabContents()
    {
        if (ImGui.Button("+ Add Rule"))
        {
            configuration.Rules.Add(new ReplacementRule { MatchMode = activeMatchTab });
            configuration.Save();
        }

        var remaining = ImGui.GetContentRegionAvail();
        DrawRuleTable(Math.Max(120f, remaining.Y));
    }

    private void DrawRuleTable(float height)
    {
        var showScopeColumn = configuration.ChanceScope == ChanceScope.PerEntry;
        var columnCount = showScopeColumn ? 8 : 7;

        var flags = ImGuiTableFlags.Borders
                    | ImGuiTableFlags.RowBg
                    | ImGuiTableFlags.SizingStretchProp
                    | ImGuiTableFlags.ScrollY
                    | ImGuiTableFlags.Resizable
                    | ImGuiTableFlags.ScrollX;

        if (!ImGui.BeginTable("##AliasXIVRulesV8", columnCount, flags, new Vector2(-1, height)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("Find (a|b)", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Replace With", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Case Sensitive", ImGuiTableColumnFlags.WidthFixed, 110f);
        ImGui.TableSetupColumn("Chance", ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("%", ImGuiTableColumnFlags.WidthFixed, 70f);
        if (showScopeColumn)
            ImGui.TableSetupColumn("Scope", ImGuiTableColumnFlags.WidthFixed, 140f);
        ImGui.TableSetupColumn("Delete", ImGuiTableColumnFlags.WidthFixed, 50f);
        ImGui.TableHeadersRow();

        var rules = configuration.Rules;
        var removeIndex = -1;

        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (rule.MatchMode != activeMatchTab)
                continue;

            ImGui.PushID(rule.Id.ToString());

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            CenterNextCheckbox();
            var ruleEnabled = rule.Enabled;
            if (ImGui.Checkbox("##enabled", ref ruleEnabled))
            {
                rule.Enabled = ruleEnabled;
                configuration.Save();
            }

            ImGui.TableSetColumnIndex(1);
            ImGui.SetNextItemWidth(-float.Epsilon);
            var find = rule.Finds.Count > 0
                ? ReplacementRule.FormatFindsText(rule.Finds)
                : rule.Find;
            if (ImGui.InputText("##find", ref find, 512))
            {
                rule.Finds = ReplacementRule.ParseFindsText(find);
                rule.Find = string.Empty;
                configuration.Save();
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Separate multiple finds with | (e.g. yes|yea)");

            ImGui.TableSetColumnIndex(2);
            ImGui.SetNextItemWidth(-float.Epsilon);
            var replace = rule.Replace;
            if (ImGui.InputText("##replace", ref replace, 512))
            {
                rule.Replace = replace;
                configuration.Save();
            }

            ImGui.TableSetColumnIndex(3);
            CenterNextCheckbox();
            var caseSensitive = rule.CaseSensitive;
            if (ImGui.Checkbox("##case", ref caseSensitive))
            {
                rule.CaseSensitive = caseSensitive;
                configuration.Save();
            }

            ImGui.TableSetColumnIndex(4);
            CenterNextCheckbox();
            var chanceEnabled = rule.ChanceEnabled;
            if (ImGui.Checkbox("##chance", ref chanceEnabled))
            {
                rule.ChanceEnabled = chanceEnabled;
                configuration.Save();
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When enabled, this rule applies with the given probability.");

            ImGui.TableSetColumnIndex(5);
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

            var deleteColumn = showScopeColumn ? 7 : 6;
            if (showScopeColumn)
            {
                ImGui.TableSetColumnIndex(6);
                ImGui.SetNextItemWidth(-float.Epsilon);
                if (!rule.ChanceEnabled)
                    ImGui.BeginDisabled();
                var scopeLabel = rule.ChanceScope == ChanceScope.PerOccurrence
                    ? "Per occurrence"
                    : "Per message";
                if (ImGui.BeginCombo("##chanceScope", scopeLabel))
                {
                    if (ImGui.Selectable("Per message", rule.ChanceScope == ChanceScope.PerMessage))
                    {
                        rule.ChanceScope = ChanceScope.PerMessage;
                        configuration.Save();
                    }

                    if (rule.ChanceScope == ChanceScope.PerMessage)
                        ImGui.SetItemDefaultFocus();

                    if (ImGui.Selectable("Per occurrence", rule.ChanceScope == ChanceScope.PerOccurrence))
                    {
                        rule.ChanceScope = ChanceScope.PerOccurrence;
                        configuration.Save();
                    }

                    if (rule.ChanceScope == ChanceScope.PerOccurrence)
                        ImGui.SetItemDefaultFocus();

                    ImGui.EndCombo();
                }
                if (!rule.ChanceEnabled)
                    ImGui.EndDisabled();

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip(
                        "Per message: one roll for this rule per send.\n" +
                        "Per occurrence: independent roll per match for this rule.");
                }
            }

            ImGui.TableSetColumnIndex(deleteColumn);
            var io = ImGui.GetIO();
            var canDelete = io.KeyCtrl && io.KeyShift;
            if (!canDelete)
                ImGui.BeginDisabled();
            if (ImGui.Button("X") && canDelete)
                removeIndex = i;
            if (!canDelete)
                ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Hold Ctrl+Shift and click to delete");

            ImGui.PopID();
        }

        ImGui.EndTable();

        if (removeIndex >= 0)
        {
            rules.RemoveAt(removeIndex);
            configuration.Save();
        }
    }

    private static void CenterNextCheckbox()
    {
        var columnWidth = ImGui.GetColumnWidth();
        var checkboxSize = ImGui.GetFrameHeight();
        var offset = Math.Max(0f, (columnWidth - checkboxSize) * 0.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
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
        var hasEmptyFind = configuration.Rules.Any(r => r.Enabled && r.GetEffectiveFinds().Count == 0);
        if (hasEmptyFind)
            ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), "Warning: enabled rules with empty Find are ignored.");

        var duplicates = configuration.Rules
            .SelectMany(r => r.GetEffectiveFinds().Select(find => (Find: find, r.MatchMode, r.CaseSensitive)))
            .GroupBy(x => x, StringTupleComparer.Instance)
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
