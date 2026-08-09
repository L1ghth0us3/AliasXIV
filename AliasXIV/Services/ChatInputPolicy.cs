using System.Diagnostics.CodeAnalysis;
using AliasXIV.Models;

namespace AliasXIV.Services;

public enum ChatInputRejectReason
{
    None,
    Empty,
    InvalidCommand,
    ExplicitBypass,
    CommandOnlyNoPayload,
    UnknownCommand,
    ChannelDisabled,
    ActiveChannelUnresolved,
}

public sealed class ChatInputPolicy
{
    /// <summary>
    /// Commands whose payloads must never be rewritten (recipient / targeting risk).
    /// Active Tell channel (plain chat) still follows the Tell enable tickbox.
    /// </summary>
    private static readonly HashSet<string> ExplicitBypassCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "t", "tell",
        "r", "reply",
    };

    public bool TryGetTransformablePayload(
        string originalText,
        IReadOnlySet<OutgoingChatChannel> enabledChannels,
        IOutgoingChannelResolver channelResolver,
        [NotNullWhen(true)] out string? prefix,
        [NotNullWhen(true)] out string? payload)
        => TryGetTransformablePayload(
            originalText,
            enabledChannels,
            channelResolver,
            out prefix,
            out payload,
            out _,
            out _,
            out _);

    public bool TryGetTransformablePayload(
        string originalText,
        IReadOnlySet<OutgoingChatChannel> enabledChannels,
        IOutgoingChannelResolver channelResolver,
        [NotNullWhen(true)] out string? prefix,
        [NotNullWhen(true)] out string? payload,
        out ChatInputRejectReason rejectReason,
        out string? command,
        out OutgoingChatChannel? channel)
    {
        prefix = null;
        payload = null;
        command = null;
        channel = null;
        rejectReason = ChatInputRejectReason.None;

        if (string.IsNullOrEmpty(originalText))
        {
            rejectReason = ChatInputRejectReason.Empty;
            return false;
        }

        // Plain chat: gate on the game's active outgoing channel.
        if (originalText[0] != '/')
        {
            if (!channelResolver.TryGetActiveChannel(out var activeChannel))
            {
                rejectReason = ChatInputRejectReason.ActiveChannelUnresolved;
                return false;
            }

            channel = activeChannel;
            if (!enabledChannels.Contains(activeChannel))
            {
                rejectReason = ChatInputRejectReason.ChannelDisabled;
                return false;
            }

            prefix = string.Empty;
            payload = originalText;
            return true;
        }

        if (!TrySplitCommand(originalText, out command, out var commandEndIndex))
        {
            rejectReason = ChatInputRejectReason.InvalidCommand;
            return false;
        }

        if (ExplicitBypassCommands.Contains(command))
        {
            rejectReason = ChatInputRejectReason.ExplicitBypass;
            return false;
        }

        if (!channelResolver.TryMapSlashCommand(command, out var mappedChannel))
        {
            rejectReason = ChatInputRejectReason.UnknownCommand;
            return false;
        }

        channel = mappedChannel;
        if (!enabledChannels.Contains(mappedChannel))
        {
            rejectReason = ChatInputRejectReason.ChannelDisabled;
            return false;
        }

        if (commandEndIndex >= originalText.Length)
        {
            rejectReason = ChatInputRejectReason.CommandOnlyNoPayload;
            return false;
        }

        prefix = originalText[..commandEndIndex];
        payload = originalText[commandEndIndex..];
        return true;
    }

    private static bool TrySplitCommand(string text, out string command, out int commandEndIndex)
    {
        command = string.Empty;
        commandEndIndex = 0;

        var i = 1;
        while (i < text.Length && !char.IsWhiteSpace(text[i]))
            i++;

        if (i == 1)
            return false;

        command = text[1..i];
        if (command.Length == 0)
            return false;

        var afterCommand = i;
        while (afterCommand < text.Length && char.IsWhiteSpace(text[afterCommand]))
            afterCommand++;

        // Keep a single trailing space in the prefix when payload follows.
        commandEndIndex = afterCommand > i ? i + 1 : i;
        return true;
    }
}
