using AliasXIV.Hooks;
using AliasXIV.Services;
using AliasXIV.UI;
using Dalamud.Game.Command;
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
    private readonly ConfigWindow configWindow;
    private readonly ChatBoxHook chatBoxHook;
    private readonly ReplacementEngine replacementEngine = new();
    private readonly ChatInputPolicy chatInputPolicy = new();
    private readonly OutgoingChatRewriter rewriter;

    public Configuration Configuration { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        NormalizeConfiguration(Configuration);

        rewriter = new OutgoingChatRewriter(Configuration, replacementEngine, chatInputPolicy);

        configWindow = new ConfigWindow(Configuration, replacementEngine);
        windowSystem.AddWindow(configWindow);

        chatBoxHook = new ChatBoxHook(
            rewriter,
            Log,
            GameInteropProvider,
            ChatGui);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open AliasXIV configuration. Use '/aliasxiv on|off' to toggle.",
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

        Log.Information("AliasXIV loaded.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;

        windowSystem.RemoveAllWindows();
        configWindow.Dispose();
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

        ToggleConfigUi();
    }

    public void ToggleConfigUi() => configWindow.Toggle();

    private static void NormalizeConfiguration(Configuration configuration)
    {
        configuration.Rules ??= [];
        foreach (var rule in configuration.Rules)
        {
            rule.Find ??= string.Empty;
            rule.Replace ??= string.Empty;
            rule.Finds ??= [];
            rule.ChancePercent = Math.Clamp(rule.ChancePercent, 0f, 100f);
            if (rule.Id == Guid.Empty)
                rule.Id = Guid.NewGuid();

            // Migrate legacy single Find into Finds, then keep Finds as the source of truth.
            if (rule.Finds.Count == 0 && !string.IsNullOrEmpty(rule.Find))
                rule.Finds.Add(rule.Find);

            for (var i = rule.Finds.Count - 1; i >= 0; i--)
            {
                var find = rule.Finds[i]?.Trim() ?? string.Empty;
                if (find.Length == 0)
                    rule.Finds.RemoveAt(i);
                else
                    rule.Finds[i] = find;
            }

            rule.Find = string.Empty;
        }
    }
}
