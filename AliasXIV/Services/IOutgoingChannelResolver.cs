using AliasXIV.Models;

namespace AliasXIV.Services;

public interface IOutgoingChannelResolver
{
    /// <summary>
    /// Reads the game's currently selected outgoing chat channel (plain / no-slash input).
    /// </summary>
    bool TryGetActiveChannel(out OutgoingChatChannel channel);

    /// <summary>
    /// Maps a slash-command token (without leading '/') to a known chat channel.
    /// Active LS/CWLS aliases (/l, /cwl) use the game's current cycle index when available.
    /// </summary>
    bool TryMapSlashCommand(string command, out OutgoingChatChannel channel);
}
