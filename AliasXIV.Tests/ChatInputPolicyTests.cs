using AliasXIV.Services;
using Xunit;

namespace AliasXIV.Tests;

public class ChatInputPolicyTests
{
    private readonly ChatInputPolicy policy = new();

    [Fact]
    public void PlainChatIsTransformable()
    {
        Assert.True(policy.TryGetTransformablePayload("Today is nice", true, out var prefix, out var payload));
        Assert.Equal(string.Empty, prefix);
        Assert.Equal("Today is nice", payload);
    }

    [Fact]
    public void SimplePartyCommandKeepsPrefix()
    {
        Assert.True(policy.TryGetTransformablePayload("/p Today is nice", true, out var prefix, out var payload));
        Assert.Equal("/p ", prefix);
        Assert.Equal("Today is nice", payload);
    }

    [Fact]
    public void EchoCommandPayloadIsTransformableInUmbrellaMode()
    {
        Assert.True(policy.TryGetTransformablePayload("/echo nice", true, out var prefix, out var payload));
        Assert.Equal("/echo ", prefix);
        Assert.Equal("nice", payload);
    }

    [Fact]
    public void ActionCommandPayloadIsTransformableInUmbrellaMode()
    {
        Assert.True(policy.TryGetTransformablePayload("/ac \"Nice Ability\"", true, out var prefix, out var payload));
        Assert.Equal("/ac ", prefix);
        Assert.Equal("\"Nice Ability\"", payload);
    }

    [Fact]
    public void TellCommandIsBypassed()
    {
        Assert.False(policy.TryGetTransformablePayload("/tell Character Name@World nice", true, out _, out _));
    }

    [Fact]
    public void ReplyCommandIsBypassed()
    {
        Assert.False(policy.TryGetTransformablePayload("/r hello", true, out _, out _));
    }

    [Fact]
    public void UnknownSlashCommandPayloadIsTransformableInUmbrellaMode()
    {
        Assert.True(policy.TryGetTransformablePayload("/whatever nice", true, out var prefix, out var payload));
        Assert.Equal("/whatever ", prefix);
        Assert.Equal("nice", payload);
    }

    [Fact]
    public void SlashPayloadsCanBeDisabled()
    {
        Assert.False(policy.TryGetTransformablePayload("/p Today is nice", false, out _, out _));
    }

    [Fact]
    public void LinkshellCommandIsSupported()
    {
        Assert.True(policy.TryGetTransformablePayload("/l1 hello", true, out var prefix, out var payload));
        Assert.Equal("/l1 ", prefix);
        Assert.Equal("hello", payload);
    }

    [Fact]
    public void CrossWorldLinkshellCommandIsSupported()
    {
        Assert.True(policy.TryGetTransformablePayload("/cwl2 hello", true, out var prefix, out var payload));
        Assert.Equal("/cwl2 ", prefix);
        Assert.Equal("hello", payload);
    }

    [Fact]
    public void ActiveCrossWorldLinkshellCommandIsSupported()
    {
        Assert.True(policy.TryGetTransformablePayload("/cwl hello", true, out var prefix, out var payload));
        Assert.Equal("/cwl ", prefix);
        Assert.Equal("hello", payload);
    }

    [Fact]
    public void ActiveLinkshellCommandIsSupported()
    {
        Assert.True(policy.TryGetTransformablePayload("/l hello", true, out var prefix, out var payload));
        Assert.Equal("/l ", prefix);
        Assert.Equal("hello", payload);
    }

    [Fact]
    public void CommandOnlyIsBypassed()
    {
        Assert.False(policy.TryGetTransformablePayload(
            "/cwl1",
            true,
            out _,
            out _,
            out var reason,
            out var command));
        Assert.Equal(ChatInputRejectReason.CommandOnlyNoPayload, reason);
        Assert.Equal("cwl1", command);
    }

    [Fact]
    public void TellReportsExplicitBypass()
    {
        Assert.False(policy.TryGetTransformablePayload(
            "/tell Somebody nice",
            true,
            out _,
            out _,
            out var reason,
            out var command));
        Assert.Equal(ChatInputRejectReason.ExplicitBypass, reason);
        Assert.Equal("tell", command);
    }
}
