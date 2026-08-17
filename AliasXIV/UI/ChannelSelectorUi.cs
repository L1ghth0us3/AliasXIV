using AliasXIV.Models;
using Dalamud.Bindings.ImGui;

namespace AliasXIV.UI;

public static class ChannelSelectorUi
{
    public static void Draw(Configuration configuration)
    {
        ImGui.TextWrapped(
            "Replacements apply only on enabled channels. Plain chat uses the game's active channel; " +
            "/tell and /reply are always skipped.");

        if (ImGui.Button("Enable all"))
        {
            configuration.EnabledChannels = OutgoingChatChannelCatalog.AllChannels.ToList();
            configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Disable all"))
        {
            configuration.EnabledChannels.Clear();
            configuration.Save();
        }

        foreach (var info in OutgoingChatChannelCatalog.All)
        {
            if (IsLinkshell(info.Channel) || IsCrossLinkshell(info.Channel))
                continue;

            DrawChannelCheckbox(configuration, info);
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Linkshells");
        DrawChannelGrid(
            configuration,
            "##AliasXIVLsChannels",
            OutgoingChatChannelCatalog.All.Where(info => IsLinkshell(info.Channel)));

        ImGui.Spacing();
        ImGui.TextUnformatted("Cross-world Linkshells");
        DrawChannelGrid(
            configuration,
            "##AliasXIVCwlsChannels",
            OutgoingChatChannelCatalog.All.Where(info => IsCrossLinkshell(info.Channel)));
    }

    private static void DrawChannelGrid(
        Configuration configuration,
        string tableId,
        IEnumerable<OutgoingChatChannelInfo> channels)
    {
        var list = channels.ToList();
        const int columns = 2;
        if (!ImGui.BeginTable(tableId, columns, ImGuiTableFlags.SizingStretchSame))
            return;

        for (var i = 0; i < list.Count; i++)
        {
            if (i % columns == 0)
                ImGui.TableNextRow();

            ImGui.TableNextColumn();
            DrawChannelCheckbox(configuration, list[i]);
        }

        ImGui.EndTable();
    }

    private static void DrawChannelCheckbox(Configuration configuration, OutgoingChatChannelInfo info)
    {
        var isEnabled = configuration.IsChannelEnabled(info.Channel);
        if (!ImGui.Checkbox(info.DisplayName, ref isEnabled))
            return;

        configuration.SetChannelEnabled(info.Channel, isEnabled);
        configuration.Save();
    }

    private static bool IsLinkshell(OutgoingChatChannel channel)
        => channel is >= OutgoingChatChannel.Linkshell1 and <= OutgoingChatChannel.Linkshell8;

    private static bool IsCrossLinkshell(OutgoingChatChannel channel)
        => channel is >= OutgoingChatChannel.CrossLinkshell1 and <= OutgoingChatChannel.CrossLinkshell8;
}
