using System.Text;
using AliasXIV.Services;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.Shell;

namespace AliasXIV.Hooks;

public sealed unsafe class ChatBoxHook : IDisposable
{
    private const string ProcessChatBoxEntrySignature =
        "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 48 8B F2 48 8B F9 45 84 C9";

    private const byte SeStringPayloadStart = 0x02;

    private readonly OutgoingChatRewriter rewriter;
    private readonly IPluginLog log;
    private readonly IChatGui? chatGui;

    private readonly Hook<UIModule.Delegates.ProcessChatBoxEntry>? processChatBoxHook;
    private readonly Hook<ShellCommandModule.Delegates.ExecuteCommandInner>? executeCommandHook;

    [ThreadStatic]
    private static bool rewriting;

    public ChatBoxHook(
        OutgoingChatRewriter rewriter,
        IPluginLog log,
        IGameInteropProvider gameInteropProvider,
        IChatGui? chatGui = null)
    {
        this.rewriter = rewriter;
        this.log = log;
        this.chatGui = chatGui;

        try
        {
            processChatBoxHook = gameInteropProvider.HookFromSignature<UIModule.Delegates.ProcessChatBoxEntry>(
                ProcessChatBoxEntrySignature,
                ProcessChatBoxDetour);
            processChatBoxHook.Enable();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to hook ProcessChatBoxEntry via signature; trying Address.Value.");
            var address = UIModule.Addresses.ProcessChatBoxEntry.Value;
            if (address == nint.Zero)
                address = (nint)UIModule.MemberFunctionPointers.ProcessChatBoxEntry;

            if (address != nint.Zero)
            {
                processChatBoxHook = gameInteropProvider.HookFromAddress<UIModule.Delegates.ProcessChatBoxEntry>(
                    address,
                    ProcessChatBoxDetour);
                processChatBoxHook.Enable();
            }
            else
            {
                log.Error("ProcessChatBoxEntry address is zero; this hook is inactive.");
            }
        }

        try
        {
            var execAddress = ShellCommandModule.Addresses.ExecuteCommandInner.Value;
            if (execAddress == nint.Zero)
                execAddress = (nint)ShellCommandModule.MemberFunctionPointers.ExecuteCommandInner;

            if (execAddress != nint.Zero)
            {
                executeCommandHook = gameInteropProvider.HookFromAddress<ShellCommandModule.Delegates.ExecuteCommandInner>(
                    execAddress,
                    ExecuteCommandInnerDetour);
                executeCommandHook.Enable();
            }
            else
            {
                log.Warning("ExecuteCommandInner address is zero; secondary hook inactive.");
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to hook ExecuteCommandInner.");
        }

        if (processChatBoxHook == null && executeCommandHook == null)
            throw new InvalidOperationException("No outgoing chat hooks could be installed.");
    }

    public void Dispose()
    {
        processChatBoxHook?.Dispose();
        executeCommandHook?.Dispose();
    }

    private void ProcessChatBoxDetour(
        UIModule* uiModule,
        Utf8String* message,
        nint a4,
        bool saveToHistory)
    {
        if (rewriting || processChatBoxHook == null)
        {
            processChatBoxHook?.Original(uiModule, message, a4, saveToHistory);
            return;
        }

        try
        {
            if (message == null || !TryRewriteMessage(message, out var finalText))
            {
                processChatBoxHook.Original(uiModule, message, a4, saveToHistory);
                return;
            }

            rewriting = true;
            var modified = Utf8String.FromString(finalText);
            try
            {
                processChatBoxHook.Original(uiModule, modified, a4, saveToHistory);
            }
            finally
            {
                modified->Dtor(true);
                rewriting = false;
            }
        }
        catch (Exception ex)
        {
            rewriting = false;
            log.Error(ex, "Failed while processing outgoing chat replacement.");
            processChatBoxHook.Original(uiModule, message, a4, saveToHistory);
        }
    }

    private void ExecuteCommandInnerDetour(
        ShellCommandModule* shellCommandModule,
        Utf8String* command,
        UIModule* uiModule)
    {
        if (rewriting || executeCommandHook == null)
        {
            executeCommandHook?.Original(shellCommandModule, command, uiModule);
            return;
        }

        try
        {
            if (command == null || !TryRewriteMessage(command, out var finalText))
            {
                executeCommandHook.Original(shellCommandModule, command, uiModule);
                return;
            }

            rewriting = true;
            var modified = Utf8String.FromString(finalText);
            try
            {
                executeCommandHook.Original(shellCommandModule, modified, uiModule);
            }
            finally
            {
                modified->Dtor(true);
                rewriting = false;
            }
        }
        catch (Exception ex)
        {
            rewriting = false;
            log.Error(ex, "Failed while processing outgoing chat replacement.");
            executeCommandHook.Original(shellCommandModule, command, uiModule);
        }
    }

    private bool TryRewriteMessage(Utf8String* message, out string finalText)
    {
        finalText = string.Empty;
        var span = message->AsSpan();
        if (ContainsSpecialPayload(span))
            return false;

        var originalText = span.IsEmpty ? string.Empty : Encoding.UTF8.GetString(span);
        var result = rewriter.TryRewrite(originalText);

        switch (result.Status)
        {
            case OutgoingChatRewriter.RewriteStatus.Applied:
                finalText = result.FinalText!;
                return true;
            case OutgoingChatRewriter.RewriteStatus.TooLong:
                chatGui?.PrintError("AliasXIV: replacement skipped because the result was too long.");
                return false;
            case OutgoingChatRewriter.RewriteStatus.Disabled:
            case OutgoingChatRewriter.RewriteStatus.Empty:
            case OutgoingChatRewriter.RewriteStatus.PolicyRejected:
            case OutgoingChatRewriter.RewriteStatus.Unchanged:
                return false;
            default:
                return false;
        }
    }

    private static bool ContainsSpecialPayload(ReadOnlySpan<byte> span)
    {
        foreach (var b in span)
        {
            if (b == SeStringPayloadStart)
                return true;
        }

        return false;
    }
}
