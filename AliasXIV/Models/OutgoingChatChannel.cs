namespace AliasXIV.Models;

/// <summary>
/// Selectable outgoing chat channels. Numeric values match the game's input-channel ids
/// (ChatTwo InputChannel / RaptureShellModule.ChatType), not XivChatType / LogKind.
/// </summary>
public enum OutgoingChatChannel : uint
{
    Tell = 0,
    Say = 1,
    Party = 2,
    Alliance = 3,
    Yell = 4,
    Shout = 5,
    FreeCompany = 6,
    PvpTeam = 7,
    NoviceNetwork = 8,
    CrossLinkshell1 = 9,
    CrossLinkshell2 = 10,
    CrossLinkshell3 = 11,
    CrossLinkshell4 = 12,
    CrossLinkshell5 = 13,
    CrossLinkshell6 = 14,
    CrossLinkshell7 = 15,
    CrossLinkshell8 = 16,
    // 17 and 18 are tell-related specials; mapped to Tell at read time.
    Linkshell1 = 19,
    Linkshell2 = 20,
    Linkshell3 = 21,
    Linkshell4 = 22,
    Linkshell5 = 23,
    Linkshell6 = 24,
    Linkshell7 = 25,
    Linkshell8 = 26,
}

public readonly record struct OutgoingChatChannelInfo(
    OutgoingChatChannel Channel,
    string DisplayName,
    IReadOnlyList<string> Aliases);

public static class OutgoingChatChannelCatalog
{
    public static IReadOnlyList<OutgoingChatChannelInfo> All { get; } =
    [
        new(OutgoingChatChannel.Say, "Say", ["s", "say"]),
        new(OutgoingChatChannel.Yell, "Yell", ["y", "yell"]),
        new(OutgoingChatChannel.Shout, "Shout", ["sh", "shout"]),
        new(OutgoingChatChannel.Tell, "Tell", ["t", "tell", "r", "reply"]),
        new(OutgoingChatChannel.Party, "Party", ["p", "party"]),
        new(OutgoingChatChannel.Alliance, "Alliance", ["a", "alliance"]),
        new(OutgoingChatChannel.FreeCompany, "Free Company", ["fc", "freecompany"]),
        new(OutgoingChatChannel.NoviceNetwork, "Novice Network", ["n", "novice"]),
        new(OutgoingChatChannel.PvpTeam, "PvP Team", ["pvpteam"]),
        new(OutgoingChatChannel.Linkshell1, "Linkshell 1", ["l1", "ls1", "linkshell1"]),
        new(OutgoingChatChannel.Linkshell2, "Linkshell 2", ["l2", "ls2", "linkshell2"]),
        new(OutgoingChatChannel.Linkshell3, "Linkshell 3", ["l3", "ls3", "linkshell3"]),
        new(OutgoingChatChannel.Linkshell4, "Linkshell 4", ["l4", "ls4", "linkshell4"]),
        new(OutgoingChatChannel.Linkshell5, "Linkshell 5", ["l5", "ls5", "linkshell5"]),
        new(OutgoingChatChannel.Linkshell6, "Linkshell 6", ["l6", "ls6", "linkshell6"]),
        new(OutgoingChatChannel.Linkshell7, "Linkshell 7", ["l7", "ls7", "linkshell7"]),
        new(OutgoingChatChannel.Linkshell8, "Linkshell 8", ["l8", "ls8", "linkshell8"]),
        new(OutgoingChatChannel.CrossLinkshell1, "Cross-world Linkshell 1", ["cwl1", "cwls1", "cwlinkshell1"]),
        new(OutgoingChatChannel.CrossLinkshell2, "Cross-world Linkshell 2", ["cwl2", "cwls2", "cwlinkshell2"]),
        new(OutgoingChatChannel.CrossLinkshell3, "Cross-world Linkshell 3", ["cwl3", "cwls3", "cwlinkshell3"]),
        new(OutgoingChatChannel.CrossLinkshell4, "Cross-world Linkshell 4", ["cwl4", "cwls4", "cwlinkshell4"]),
        new(OutgoingChatChannel.CrossLinkshell5, "Cross-world Linkshell 5", ["cwl5", "cwls5", "cwlinkshell5"]),
        new(OutgoingChatChannel.CrossLinkshell6, "Cross-world Linkshell 6", ["cwl6", "cwls6", "cwlinkshell6"]),
        new(OutgoingChatChannel.CrossLinkshell7, "Cross-world Linkshell 7", ["cwl7", "cwls7", "cwlinkshell7"]),
        new(OutgoingChatChannel.CrossLinkshell8, "Cross-world Linkshell 8", ["cwl8", "cwls8", "cwlinkshell8"]),
    ];

    private static readonly Dictionary<string, OutgoingChatChannel> AliasLookup =
        BuildAliasLookup();

    /// <summary>Every catalog channel — used by "Enable all", not as the default selection.</summary>
    public static IReadOnlyCollection<OutgoingChatChannel> AllChannels { get; } =
        All.Select(info => info.Channel).ToArray();

    public static bool TryMapAlias(string command, out OutgoingChatChannel channel)
        => AliasLookup.TryGetValue(command, out channel);

    public static bool IsDefined(OutgoingChatChannel channel)
        => All.Any(info => info.Channel == channel);

    public static bool IsDefined(uint raw)
        => Enum.IsDefined(typeof(OutgoingChatChannel), raw) && IsDefined((OutgoingChatChannel)raw);

    /// <summary>
    /// Maps a raw RaptureShellModule.ChatType value to a catalog channel.
    /// Values 17 and 18 are tell-related specials.
    /// </summary>
    public static bool TryFromRaw(uint raw, out OutgoingChatChannel channel)
    {
        channel = default;

        if (raw is 17 or 18)
        {
            channel = OutgoingChatChannel.Tell;
            return true;
        }

        if (!IsDefined(raw))
            return false;

        channel = (OutgoingChatChannel)raw;
        return true;
    }

    private static Dictionary<string, OutgoingChatChannel> BuildAliasLookup()
    {
        var lookup = new Dictionary<string, OutgoingChatChannel>(StringComparer.OrdinalIgnoreCase);
        foreach (var info in All)
        {
            foreach (var alias in info.Aliases)
                lookup[alias] = info.Channel;
        }

        return lookup;
    }
}
