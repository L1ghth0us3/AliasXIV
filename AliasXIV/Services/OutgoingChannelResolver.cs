using AliasXIV.Models;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace AliasXIV.Services;

public sealed unsafe class OutgoingChannelResolver : IOutgoingChannelResolver
{
    private static readonly HashSet<string> ActiveLinkshellAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "l", "linkshell",
    };

    private static readonly HashSet<string> ActiveCrossLinkshellAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "cwl", "cwls", "cwlinkshell",
    };

    public bool TryGetActiveChannel(out OutgoingChatChannel channel)
    {
        channel = default;
        var shell = RaptureShellModule.Instance();
        if (shell == null)
            return false;

        return TryFromRaw((uint)shell->ChatType, out channel);
    }

    public bool TryMapSlashCommand(string command, out OutgoingChatChannel channel)
    {
        channel = default;
        if (string.IsNullOrEmpty(command))
            return false;

        if (OutgoingChatChannelCatalog.TryMapAlias(command, out channel))
            return true;

        if (ActiveLinkshellAliases.Contains(command))
            return TryMapActiveLinkshell(isCrossWorld: false, out channel);

        if (ActiveCrossLinkshellAliases.Contains(command))
            return TryMapActiveLinkshell(isCrossWorld: true, out channel);

        return false;
    }

    public static bool TryFromRaw(uint raw, out OutgoingChatChannel channel)
        => OutgoingChatChannelCatalog.TryFromRaw(raw, out channel);

    private static bool TryMapActiveLinkshell(bool isCrossWorld, out OutgoingChatChannel channel)
    {
        channel = default;
        var ui = UIModule.Instance();
        if (ui == null)
            return false;

        var cycle = isCrossWorld ? ui->CrossWorldLinkshellCycle : ui->LinkshellCycle;
        if (cycle is < 0 or > 7)
            return false;

        channel = isCrossWorld
            ? OutgoingChatChannel.CrossLinkshell1 + (uint)cycle
            : OutgoingChatChannel.Linkshell1 + (uint)cycle;
        return true;
    }
}
