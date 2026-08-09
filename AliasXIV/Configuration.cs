using AliasXIV.Models;
using Dalamud.Configuration;

namespace AliasXIV;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Legacy umbrella flag from config v1. Ignored after migration to <see cref="EnabledChannels"/>.
    /// </summary>
    public bool ApplyToSimpleChatCommands { get; set; } = true;

    public List<OutgoingChatChannel> EnabledChannels { get; set; } = [];

    public List<ReplacementRule> Rules { get; set; } = [];

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    public HashSet<OutgoingChatChannel> GetEnabledChannelSet()
        => new(EnabledChannels);

    public bool IsChannelEnabled(OutgoingChatChannel channel)
        => EnabledChannels.Contains(channel);

    public void SetChannelEnabled(OutgoingChatChannel channel, bool enabled)
    {
        var index = EnabledChannels.IndexOf(channel);
        if (enabled)
        {
            if (index < 0)
                EnabledChannels.Add(channel);
        }
        else if (index >= 0)
        {
            EnabledChannels.RemoveAt(index);
        }
    }
}
