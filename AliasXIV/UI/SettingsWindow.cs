using System.Numerics;
using AliasXIV.Models;
using AliasXIV.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace AliasXIV.UI;

public sealed class SettingsWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly FileDialogManager fileDialogManager;
    private readonly IChatGui chatGui;

    private bool showImportModal;
    private string? pendingImportPath;
    private string? pendingImportError;

    public SettingsWindow(
        Configuration configuration,
        FileDialogManager fileDialogManager,
        IChatGui chatGui)
        : base("AliasXIV Settings###AliasXIVSettings")
    {
        this.configuration = configuration;
        this.fileDialogManager = fileDialogManager;
        this.chatGui = chatGui;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(480, 420),
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

        ImGui.Spacing();
        ImGui.TextUnformatted("Chance scope");
        DrawChanceScopeRadios();

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Channels", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            ChannelSelectorUi.Draw(configuration);
            ImGui.Unindent();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Rules backup");
        if (ImGui.Button("Export rules"))
            BeginExport();

        ImGui.SameLine();
        if (ImGui.Button("Import rules"))
            BeginImport();

        DrawImportModal();
        fileDialogManager.Draw();
    }

    private void DrawChanceScopeRadios()
    {
        var scope = configuration.ChanceScope;

        if (ImGui.RadioButton("Per message", scope == ChanceScope.PerMessage))
        {
            configuration.ChanceScope = ChanceScope.PerMessage;
            configuration.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Each rule with chance rolls once per message. All matches for that rule are replaced or none are.");
        }

        if (ImGui.RadioButton("Per occurrence", scope == ChanceScope.PerOccurrence))
        {
            configuration.ChanceScope = ChanceScope.PerOccurrence;
            configuration.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Each match rolls independently for every rule with chance enabled.");

        if (ImGui.RadioButton("Per entry", scope == ChanceScope.PerEntry))
        {
            configuration.ChanceScope = ChanceScope.PerEntry;
            configuration.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Each rule chooses its own scope in the rules editor (per message or per occurrence).");
        }
    }

    private void BeginExport()
    {
        fileDialogManager.SaveFileDialog(
            "Export AliasXIV Rules",
            ".json",
            "aliasxiv-rules.json",
            ".json",
            OnExportComplete);
    }

    private void OnExportComplete(bool success, string path)
    {
        if (!success)
            return;

        try
        {
            RulesImportExport.ExportToFile(configuration.Rules, path);
            chatGui.Print("[AliasXIV] Exported " + configuration.Rules.Count + " rule(s).");
        }
        catch (Exception ex)
        {
            chatGui.PrintError("[AliasXIV] Export failed: " + ex.Message);
        }
    }

    private void BeginImport()
    {
        fileDialogManager.OpenFileDialog(
            "Import AliasXIV Rules",
            ".json",
            OnImportFileSelected);
    }

    private void OnImportFileSelected(bool success, string path)
    {
        if (!success)
            return;

        pendingImportPath = path;
        pendingImportError = null;
        showImportModal = true;
    }

    private void DrawImportModal()
    {
        if (!showImportModal)
            return;

        ImGui.OpenPopup("ImportRules###AliasXIVImportRules");
        if (!ImGui.BeginPopupModal("ImportRules###AliasXIVImportRules", ref showImportModal, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        if (pendingImportError is not null)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), pendingImportError);
        }
        else
        {
            ImGui.TextWrapped("How should imported rules be applied?");
            if (!string.IsNullOrEmpty(pendingImportPath))
                ImGui.TextWrapped(pendingImportPath);
        }

        if (ImGui.Button("Replace all rules"))
            ApplyImport(replaceExisting: true);

        ImGui.SameLine();
        if (ImGui.Button("Append to existing"))
            ApplyImport(replaceExisting: false);

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
            CloseImportModal();

        ImGui.EndPopup();
    }

    private void ApplyImport(bool replaceExisting)
    {
        if (string.IsNullOrEmpty(pendingImportPath))
        {
            pendingImportError = "No file selected.";
            return;
        }

        if (!RulesImportExport.TryImportFromFile(pendingImportPath, out var imported, out var error))
        {
            pendingImportError = error ?? "Import failed.";
            return;
        }

        if (replaceExisting)
            configuration.Rules = imported;
        else
            configuration.Rules.AddRange(imported);

        ConfigurationNormalizer.NormalizeRules(configuration.Rules);
        configuration.Save();
        chatGui.Print(
            "[AliasXIV] Imported " + imported.Count + " rule(s) (" + (replaceExisting ? "replaced" : "appended") + ").");
        CloseImportModal();
    }

    private void CloseImportModal()
    {
        showImportModal = false;
        pendingImportPath = null;
        pendingImportError = null;
    }
}
