using AliasXIV.Models;
using AliasXIV.Services;
using Xunit;

namespace AliasXIV.Tests;

public class ChatInputPolicyTests
{
    private readonly ChatInputPolicy policy = new();

    [Fact]
    public void PlainChatUsesActiveChannelWhenEnabled()
    {
        var resolver = FakeResolver.WithActive(OutgoingChatChannel.Say);
        Assert.True(policy.TryGetTransformablePayload(
            "Today is nice",
            AllEnabled(),
            resolver,
            out var prefix,
            out var payload,
            out _,
            out _,
            out var channel));
        Assert.Equal(string.Empty, prefix);
        Assert.Equal("Today is nice", payload);
        Assert.Equal(OutgoingChatChannel.Say, channel);
    }

    [Fact]
    public void PlainChatRejectedWhenChannelDisabled()
    {
        var resolver = FakeResolver.WithActive(OutgoingChatChannel.Party);
        Assert.False(policy.TryGetTransformablePayload(
            "hello",
            Only(OutgoingChatChannel.Say),
            resolver,
            out _,
            out _,
            out var reason,
            out _,
            out var channel));
        Assert.Equal(ChatInputRejectReason.ChannelDisabled, reason);
        Assert.Equal(OutgoingChatChannel.Party, channel);
    }

    [Fact]
    public void PlainChatRejectedWhenActiveChannelUnresolved()
    {
        var resolver = FakeResolver.Unresolved();
        Assert.False(policy.TryGetTransformablePayload(
            "hello",
            AllEnabled(),
            resolver,
            out _,
            out _,
            out var reason,
            out _,
            out _));
        Assert.Equal(ChatInputRejectReason.ActiveChannelUnresolved, reason);
    }

    [Fact]
    public void SimplePartyCommandKeepsPrefix()
    {
        Assert.True(policy.TryGetTransformablePayload(
            "/p Today is nice",
            AllEnabled(),
            FakeResolver.SlashOnly(),
            out var prefix,
            out var payload,
            out _,
            out _,
            out var channel));
        Assert.Equal("/p ", prefix);
        Assert.Equal("Today is nice", payload);
        Assert.Equal(OutgoingChatChannel.Party, channel);
    }

    [Fact]
    public void DisabledSlashChannelIsRejected()
    {
        Assert.False(policy.TryGetTransformablePayload(
            "/p Today is nice",
            Only(OutgoingChatChannel.Say),
            FakeResolver.SlashOnly(),
            out _,
            out _,
            out var reason,
            out _,
            out var channel));
        Assert.Equal(ChatInputRejectReason.ChannelDisabled, reason);
        Assert.Equal(OutgoingChatChannel.Party, channel);
    }

    [Fact]
    public void EchoCommandIsUnknown()
    {
        Assert.False(policy.TryGetTransformablePayload(
            "/echo nice",
            AllEnabled(),
            FakeResolver.SlashOnly(),
            out _,
            out _,
            out var reason,
            out var command,
            out _));
        Assert.Equal(ChatInputRejectReason.UnknownCommand, reason);
        Assert.Equal("echo", command);
    }

    [Fact]
    public void ActionCommandIsUnknown()
    {
        Assert.False(policy.TryGetTransformablePayload(
            "/ac \"Nice Ability\"",
            AllEnabled(),
            FakeResolver.SlashOnly(),
            out _,
            out _,
            out var reason,
            out _,
            out _));
        Assert.Equal(ChatInputRejectReason.UnknownCommand, reason);
    }

    [Fact]
    public void TellCommandIsBypassed()
    {
        Assert.False(policy.TryGetTransformablePayload(
            "/tell Character Name@World nice",
            AllEnabled(),
            FakeResolver.SlashOnly(),
            out _,
            out _,
            out var reason,
            out _,
            out _));
        Assert.Equal(ChatInputRejectReason.ExplicitBypass, reason);
    }

    [Fact]
    public void ReplyCommandIsBypassed()
    {
        Assert.False(policy.TryGetTransformablePayload(
            "/r hello",
            AllEnabled(),
            FakeResolver.SlashOnly(),
            out _,
            out _,
            out var reason,
            out _,
            out _));
        Assert.Equal(ChatInputRejectReason.ExplicitBypass, reason);
    }

    [Fact]
    public void UnknownSlashCommandIsRejected()
    {
        Assert.False(policy.TryGetTransformablePayload(
            "/whatever nice",
            AllEnabled(),
            FakeResolver.SlashOnly(),
            out _,
            out _,
            out var reason,
            out _,
            out _));
        Assert.Equal(ChatInputRejectReason.UnknownCommand, reason);
    }

    [Fact]
    public void LinkshellCommandIsSupported()
    {
        Assert.True(policy.TryGetTransformablePayload(
            "/l1 hello",
            AllEnabled(),
            FakeResolver.SlashOnly(),
            out var prefix,
            out var payload,
            out _,
            out _,
            out var channel));
        Assert.Equal("/l1 ", prefix);
        Assert.Equal("hello", payload);
        Assert.Equal(OutgoingChatChannel.Linkshell1, channel);
    }

    [Fact]
    public void CrossWorldLinkshellCommandIsSupported()
    {
        Assert.True(policy.TryGetTransformablePayload(
            "/cwl2 hello",
            AllEnabled(),
            FakeResolver.SlashOnly(),
            out var prefix,
            out var payload,
            out _,
            out _,
            out var channel));
        Assert.Equal("/cwl2 ", prefix);
        Assert.Equal("hello", payload);
        Assert.Equal(OutgoingChatChannel.CrossLinkshell2, channel);
    }

    [Fact]
    public void ActiveCrossWorldLinkshellCommandUsesResolver()
    {
        var resolver = FakeResolver.WithSlashMap("cwl", OutgoingChatChannel.CrossLinkshell3);
        Assert.True(policy.TryGetTransformablePayload(
            "/cwl hello",
            AllEnabled(),
            resolver,
            out var prefix,
            out var payload,
            out _,
            out _,
            out var channel));
        Assert.Equal("/cwl ", prefix);
        Assert.Equal("hello", payload);
        Assert.Equal(OutgoingChatChannel.CrossLinkshell3, channel);
    }

    [Fact]
    public void ActiveLinkshellCommandUsesResolver()
    {
        var resolver = FakeResolver.WithSlashMap("l", OutgoingChatChannel.Linkshell4);
        Assert.True(policy.TryGetTransformablePayload(
            "/l hello",
            AllEnabled(),
            resolver,
            out var prefix,
            out var payload,
            out _,
            out _,
            out var channel));
        Assert.Equal("/l ", prefix);
        Assert.Equal("hello", payload);
        Assert.Equal(OutgoingChatChannel.Linkshell4, channel);
    }

    [Fact]
    public void CommandOnlyIsBypassed()
    {
        Assert.False(policy.TryGetTransformablePayload(
            "/cwl1",
            AllEnabled(),
            FakeResolver.SlashOnly(),
            out _,
            out _,
            out var reason,
            out var command,
            out _));
        Assert.Equal(ChatInputRejectReason.CommandOnlyNoPayload, reason);
        Assert.Equal("cwl1", command);
    }

    [Fact]
    public void TellReportsExplicitBypass()
    {
        Assert.False(policy.TryGetTransformablePayload(
            "/tell Somebody nice",
            AllEnabled(),
            FakeResolver.SlashOnly(),
            out _,
            out _,
            out var reason,
            out var command,
            out _));
        Assert.Equal(ChatInputRejectReason.ExplicitBypass, reason);
        Assert.Equal("tell", command);
    }

    [Theory]
    [InlineData("p", OutgoingChatChannel.Party)]
    [InlineData("party", OutgoingChatChannel.Party)]
    [InlineData("s", OutgoingChatChannel.Say)]
    [InlineData("say", OutgoingChatChannel.Say)]
    [InlineData("fc", OutgoingChatChannel.FreeCompany)]
    [InlineData("l3", OutgoingChatChannel.Linkshell3)]
    [InlineData("ls3", OutgoingChatChannel.Linkshell3)]
    [InlineData("cwl2", OutgoingChatChannel.CrossLinkshell2)]
    public void CatalogMapsAliases(string alias, OutgoingChatChannel expected)
    {
        Assert.True(OutgoingChatChannelCatalog.TryMapAlias(alias, out var channel));
        Assert.Equal(expected, channel);
    }

    [Theory]
    [InlineData(1u, OutgoingChatChannel.Say)]
    [InlineData(0u, OutgoingChatChannel.Tell)]
    [InlineData(17u, OutgoingChatChannel.Tell)]
    [InlineData(18u, OutgoingChatChannel.Tell)]
    [InlineData(19u, OutgoingChatChannel.Linkshell1)]
    public void TryFromRawMapsInputChannels(uint raw, OutgoingChatChannel expected)
    {
        Assert.True(OutgoingChatChannelCatalog.TryFromRaw(raw, out var channel));
        Assert.Equal(expected, channel);
    }

    private static HashSet<OutgoingChatChannel> AllEnabled()
        => new(OutgoingChatChannelCatalog.AllChannels);

    private static HashSet<OutgoingChatChannel> Only(params OutgoingChatChannel[] channels)
        => new(channels);

    private sealed class FakeResolver : IOutgoingChannelResolver
    {
        private readonly OutgoingChatChannel? active;
        private readonly Dictionary<string, OutgoingChatChannel> slashOverrides;

        private FakeResolver(
            OutgoingChatChannel? active,
            Dictionary<string, OutgoingChatChannel>? slashOverrides = null)
        {
            this.active = active;
            this.slashOverrides = slashOverrides ?? new(StringComparer.OrdinalIgnoreCase);
        }

        public static FakeResolver WithActive(OutgoingChatChannel channel)
            => new(channel);

        public static FakeResolver Unresolved()
            => new(null);

        public static FakeResolver SlashOnly()
            => new(null);

        public static FakeResolver WithSlashMap(string command, OutgoingChatChannel channel)
            => new(null, new Dictionary<string, OutgoingChatChannel>(StringComparer.OrdinalIgnoreCase)
            {
                [command] = channel,
            });

        public bool TryGetActiveChannel(out OutgoingChatChannel channel)
        {
            if (active is { } value)
            {
                channel = value;
                return true;
            }

            channel = default;
            return false;
        }

        public bool TryMapSlashCommand(string command, out OutgoingChatChannel channel)
        {
            if (slashOverrides.TryGetValue(command, out channel))
                return true;

            return OutgoingChatChannelCatalog.TryMapAlias(command, out channel);
        }
    }
}
