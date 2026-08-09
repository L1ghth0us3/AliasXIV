using System.Text;
using AliasXIV.Models;

namespace AliasXIV.Services;

public sealed class OutgoingChatRewriter
{
    public const int MaxOutgoingUtf8Bytes = 500;

    private readonly Configuration configuration;
    private readonly ReplacementEngine replacementEngine;
    private readonly ChatInputPolicy inputPolicy;
    private readonly IOutgoingChannelResolver channelResolver;

    public OutgoingChatRewriter(
        Configuration configuration,
        ReplacementEngine replacementEngine,
        ChatInputPolicy inputPolicy,
        IOutgoingChannelResolver channelResolver)
    {
        this.configuration = configuration;
        this.replacementEngine = replacementEngine;
        this.inputPolicy = inputPolicy;
        this.channelResolver = channelResolver;
    }

    public enum RewriteStatus
    {
        Disabled,
        Empty,
        PolicyRejected,
        Unchanged,
        TooLong,
        Applied,
    }

    public readonly record struct RewriteResult(
        RewriteStatus Status,
        string? FinalText,
        string? Command,
        ChatInputRejectReason RejectReason,
        OutgoingChatChannel? Channel,
        int PrefixLength,
        int PayloadLength,
        int EnabledRuleCount,
        int OriginalUtf8Bytes,
        int FinalUtf8Bytes);

    public RewriteResult TryRewrite(string originalText)
    {
        var enabledRules = CountEnabledRules();

        if (!configuration.Enabled)
        {
            return new RewriteResult(
                RewriteStatus.Disabled,
                null,
                null,
                ChatInputRejectReason.None,
                null,
                0,
                0,
                enabledRules,
                0,
                0);
        }

        if (string.IsNullOrEmpty(originalText))
        {
            return new RewriteResult(
                RewriteStatus.Empty,
                null,
                null,
                ChatInputRejectReason.Empty,
                null,
                0,
                0,
                enabledRules,
                0,
                0);
        }

        var originalBytes = Encoding.UTF8.GetByteCount(originalText);
        var enabledChannels = configuration.GetEnabledChannelSet();

        if (!inputPolicy.TryGetTransformablePayload(
                originalText,
                enabledChannels,
                channelResolver,
                out var prefix,
                out var payload,
                out var rejectReason,
                out var command,
                out var channel))
        {
            return new RewriteResult(
                RewriteStatus.PolicyRejected,
                null,
                command,
                rejectReason,
                channel,
                0,
                0,
                enabledRules,
                originalBytes,
                0);
        }

        var transformed = replacementEngine.Transform(payload, configuration.Rules, evaluateChance: true);
        if (transformed == payload)
        {
            return new RewriteResult(
                RewriteStatus.Unchanged,
                null,
                command,
                ChatInputRejectReason.None,
                channel,
                prefix.Length,
                payload.Length,
                enabledRules,
                originalBytes,
                originalBytes);
        }

        var finalText = prefix + transformed;
        var finalBytes = Encoding.UTF8.GetByteCount(finalText);
        if (finalBytes > MaxOutgoingUtf8Bytes)
        {
            return new RewriteResult(
                RewriteStatus.TooLong,
                null,
                command,
                ChatInputRejectReason.None,
                channel,
                prefix.Length,
                payload.Length,
                enabledRules,
                originalBytes,
                finalBytes);
        }

        return new RewriteResult(
            RewriteStatus.Applied,
            finalText,
            command,
            ChatInputRejectReason.None,
            channel,
            prefix.Length,
            payload.Length,
            enabledRules,
            originalBytes,
            finalBytes);
    }

    private int CountEnabledRules()
    {
        var count = 0;
        foreach (var rule in configuration.Rules)
        {
            if (rule.Enabled && rule.GetEffectiveFinds().Count > 0)
                count++;
        }

        return count;
    }
}
