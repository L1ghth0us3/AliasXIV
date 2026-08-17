using AliasXIV.Hooks;
using AliasXIV.Models;
using AliasXIV.Services;
using AliasXIV.UI;
using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AliasXIV;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/aliasxiv";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("AliasXIV");
    private readonly FileDialogManager fileDialogManager = new();
    private readonly ConfigWindow configWindow;
    private readonly SettingsWindow settingsWindow;
    private readonly ChatBoxHook chatBoxHook;
    private readonly ReplacementEngine replacementEngine = new();
    private readonly ChatInputPolicy chatInputPolicy = new();
    private readonly OutgoingChannelResolver channelResolver = new();
    private readonly OutgoingChatRewriter rewriter;

    public Configuration Configuration { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ConfigurationNormalizer.Normalize(Configuration);

        rewriter = new OutgoingChatRewriter(
            Configuration,
            replacementEngine,
            chatInputPolicy,
            channelResolver);

        settingsWindow = new SettingsWindow(
            Configuration,
            fileDialogManager,
            ChatGui);
        configWindow = new ConfigWindow(
            Configuration,
            replacementEngine,
            () => settingsWindow.IsOpen = true);
        windowSystem.AddWindow(configWindow);
        windowSystem.AddWindow(settingsWindow);

        chatBoxHook = new ChatBoxHook(
            rewriter,
            Log,
            GameInteropProvider,
            ChatGui);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open AliasXIV rules editor. Use '/aliasxiv on|off' to toggle.",
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleSettingsUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information("AliasXIV loaded.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleSettingsUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        windowSystem.RemoveAllWindows();
        fileDialogManager.Reset();
        configWindow.Dispose();
        settingsWindow.Dispose();
        chatBoxHook.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();
        if (trimmed.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            Configuration.Enabled = true;
            Configuration.Save();
            ChatGui.Print("AliasXIV enabled.");
            return;
        }

        if (trimmed.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            Configuration.Enabled = false;
            Configuration.Save();
            ChatGui.Print("AliasXIV disabled.");
            return;
        }

        ToggleMainUi();
    }

    public void ToggleSettingsUi() => settingsWindow.Toggle();

    public void ToggleMainUi() => configWindow.Toggle();
}
