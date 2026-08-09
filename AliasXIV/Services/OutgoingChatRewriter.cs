using System.Text;

namespace AliasXIV.Services;

public sealed class OutgoingChatRewriter
{
    public const int MaxOutgoingUtf8Bytes = 500;

    private readonly Configuration configuration;
    private readonly ReplacementEngine replacementEngine;
    private readonly ChatInputPolicy inputPolicy;

    public OutgoingChatRewriter(
        Configuration configuration,
        ReplacementEngine replacementEngine,
        ChatInputPolicy inputPolicy)
    {
        this.configuration = configuration;
        this.replacementEngine = replacementEngine;
        this.inputPolicy = inputPolicy;
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
        int PrefixLength,
        int PayloadLength,
        int EnabledRuleCount,
        int OriginalUtf8Bytes,
        int FinalUtf8Bytes);

    public RewriteResult TryRewrite(string originalText)
    {
        var enabledRules = CountEnabledRules();

        if (!configuration.Enabled)
            return new RewriteResult(RewriteStatus.Disabled, null, null, ChatInputRejectReason.None, 0, 0, enabledRules, 0, 0);

        if (string.IsNullOrEmpty(originalText))
            return new RewriteResult(RewriteStatus.Empty, null, null, ChatInputRejectReason.Empty, 0, 0, enabledRules, 0, 0);

        var originalBytes = Encoding.UTF8.GetByteCount(originalText);

        if (!inputPolicy.TryGetTransformablePayload(
                originalText,
                configuration.ApplyToSimpleChatCommands,
                out var prefix,
                out var payload,
                out var rejectReason,
                out var command))
        {
            return new RewriteResult(
                RewriteStatus.PolicyRejected,
                null,
                command,
                rejectReason,
                0,
                0,
                enabledRules,
                originalBytes,
                0);
        }

        var transformed = replacementEngine.Transform(payload, configuration.Rules);
        if (transformed == payload)
        {
            return new RewriteResult(
                RewriteStatus.Unchanged,
                null,
                command,
                ChatInputRejectReason.None,
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
            if (rule.Enabled && !string.IsNullOrEmpty(rule.Find))
                count++;
        }

        return count;
    }
}
