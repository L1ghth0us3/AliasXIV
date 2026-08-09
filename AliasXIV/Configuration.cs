using AliasXIV.Models;
using Dalamud.Configuration;

namespace AliasXIV;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    public bool ApplyToSimpleChatCommands { get; set; } = true;

    public List<ReplacementRule> Rules { get; set; } = [];

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
