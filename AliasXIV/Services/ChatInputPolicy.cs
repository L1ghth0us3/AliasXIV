using System.Diagnostics.CodeAnalysis;

namespace AliasXIV.Services;

public enum ChatInputRejectReason
{
    None,
    Empty,
    SlashCommandsDisabled,
    InvalidCommand,
    ExplicitBypass,
    CommandOnlyNoPayload,
}

public sealed class ChatInputPolicy
{
    /// <summary>
    /// Commands whose payloads must never be rewritten (recipient / targeting risk).
    /// Umbrella mode still transforms every other slash-command payload.
    /// </summary>
    private static readonly HashSet<string> ExplicitBypassCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "t", "tell",
        "r", "reply",
    };

    public bool TryGetTransformablePayload(
        string originalText,
        bool applyToSlashCommandPayloads,
        [NotNullWhen(true)] out string? prefix,
        [NotNullWhen(true)] out string? payload)
        => TryGetTransformablePayload(
            originalText,
            applyToSlashCommandPayloads,
            out prefix,
            out payload,
            out _,
            out _);

    public bool TryGetTransformablePayload(
        string originalText,
        bool applyToSlashCommandPayloads,
        [NotNullWhen(true)] out string? prefix,
        [NotNullWhen(true)] out string? payload,
        out ChatInputRejectReason rejectReason,
        out string? command)
    {
        prefix = null;
        payload = null;
        command = null;
        rejectReason = ChatInputRejectReason.None;

        if (string.IsNullOrEmpty(originalText))
        {
            rejectReason = ChatInputRejectReason.Empty;
            return false;
        }

        // Plain chat (active channel, no slash): always transformable.
        if (originalText[0] != '/')
        {
            prefix = string.Empty;
            payload = originalText;
            return true;
        }

        if (!applyToSlashCommandPayloads)
        {
            rejectReason = ChatInputRejectReason.SlashCommandsDisabled;
            return false;
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

        // Umbrella: transform payload for every other slash command that has one.
        // Keeps the command token untouched (/cwl1, /echo, /p, ...).
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
