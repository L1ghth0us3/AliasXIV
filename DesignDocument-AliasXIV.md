# AliasXIV — Dalamud Plugin Design Specification

**Document status:** Implementation-ready
**Target platform:** Final Fantasy XIV / XIVLauncher / Dalamud
**Verified against ecosystem state:** August 9, 2026
**Working plugin name:** `AliasXIV`

---

# 1. Purpose

AliasXIV is a small Dalamud plugin that transforms user-defined words or phrases in **outgoing FFXIV chat immediately before the game processes/sends the message**.

The user defines rules such as:

```text
nice  -> bad
cat   -> dog
very nice -> absolutely terrible
```

If the user enters:

```text
Today is a nice day
```

the game should actually process/send:

```text
Today is a bad day
```

This is not merely a visual substitution in the local chat window. The transformed string must be the string passed onward through FFXIV's normal chat-processing function.

The plugin should remain deliberately small, predictable, synchronous, and fail-safe.

---

# 2. Primary Requirements

## 2.1 Core behavior

The plugin MUST:

1. Intercept an outgoing chat entry before FFXIV processes it.
2. Convert the entry into a normal C# string.
3. Apply enabled replacement rules.
4. If the result differs:

   * create a temporary FFXIV `Utf8String` containing the transformed text;
   * invoke the game's original chat-processing function with that transformed string.
5. If there is no change:

   * call the original function with the original message pointer.
6. If the plugin encounters any error:

   * log the error;
   * call the original function with the untouched message.
7. Never silently discard a user's message.

Example:

```text
Rule:
Find: nice
Replace: bad

Input:
Today is a nice day

Actually processed by FFXIV:
Today is a bad day
```

---

# 3. Current Technical Baseline

## 3.1 Dalamud SDK

Use the current official Dalamud SDK project format:

```xml
<Project Sdk="Dalamud.NET.Sdk/15.0.0">
```

The official goatcorp SamplePlugin currently uses `Dalamud.NET.Sdk/15.0.0`.

Do not start from an old API-10/API-11/API-12 tutorial.

## 3.2 .NET

The current Dalamud source targets:

```text
net10.0-windows
x64
Nullable enabled
```

Dalamud's current shared build configuration explicitly targets `net10.0-windows` and x64.

The plugin project should normally let `Dalamud.NET.Sdk` supply the correct framework configuration rather than manually fighting the SDK.

## 3.3 Unsafe code

This plugin needs access to native FFXIV structures and therefore requires unsafe code.

Add:

```xml
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

A contemporary API-15 plugin using the exact chat hook required here uses this configuration.

## 3.4 FFXIVClientStructs

Use the **FFXIVClientStructs supplied by Dalamud.NET.Sdk**.

Do NOT add or ship a separate arbitrary FFXIVClientStructs NuGet/DLL unless absolutely necessary.

Dalamud explicitly discourages shipping custom ClientStructs versions and recommends returning to the Dalamud-provided version after testing.

---

# 4. Chosen Architecture

## 4.1 Interception point

Hook:

```text
UIModule.ProcessChatBoxEntry
```

using the current generated FFXIVClientStructs delegate and member-function pointer.

Conceptually:

```csharp
Hook<UIModule.Delegates.ProcessChatBoxEntry>
```

created from:

```csharp
UIModule.MemberFunctionPointers.ProcessChatBoxEntry
```

via Dalamud's:

```csharp
IGameInteropProvider.HookFromAddress<T>()
```

This approach is based on the current EchoXIV implementation, which uses this function specifically to intercept outgoing chat **before it is processed**.

Current source demonstrates this shape:

```text
IGameInteropProvider
    ↓
HookFromAddress<UIModule.Delegates.ProcessChatBoxEntry>
    ↓
ProcessChatBoxDetour(...)
    ↓
read original Utf8String
    ↓
produce modified string
    ↓
hook.Original(... modified Utf8String ...)
```

This should be preferred over signature-scanning or manually declaring an undocumented native function signature because FFXIVClientStructs already exposes the generated function pointer and delegate.

Dalamud's documentation describes hooks as the appropriate mechanism when a plugin needs to intercept or modify a game function invocation.

---

# 5. Why Not Modify the Visible Chat Input?

Do NOT implement the primary feature by polling and rewriting the vanilla `ChatLog` input field.

QuickSymbols currently uses that approach for interactive symbol shortcuts, but its implementation documents several complications:

* it has to poll the current chat input;
* whole-field `SetText` caused flickering;
* previous text could reappear after another keypress;
* it ultimately resorts to simulated backspaces followed by native `InsertText`;
* it only performs replacements at the end of the current input.

That mechanism is appropriate when the user must visibly see a symbol inserted while typing.

It is unnecessarily fragile for AliasXIV.

AliasXIV only cares about:

> "What text actually gets submitted when Enter is pressed?"

Therefore transformation should occur at `ProcessChatBoxEntry`.

Benefits:

* works on the completed message;
* catches pasted text;
* catches edits made in the middle of the message;
* requires no caret tracking;
* requires no keyboard simulation;
* has no chat-input flicker;
* does not transform prematurely while the user is still typing;
* guarantees the transformed string is the one handed onward to FFXIV's normal chat processing.

---

# 6. Proposed Project Structure

Keep the project deliberately small.

```text
AliasXIV/
│
├─ AliasXIV.csproj
├─ AliasXIV.json
│
├─ Plugin.cs
├─ Configuration.cs
│
├─ Models/
│  ├─ ReplacementRule.cs
│  └─ MatchMode.cs
│
├─ Services/
│  ├─ ReplacementEngine.cs
│  └─ ChatInputPolicy.cs
│
├─ Hooks/
│  └─ ChatBoxHook.cs
│
└─ UI/
   └─ ConfigWindow.cs
```

Optional tests:

```text
AliasXIV.Tests/
├─ ReplacementEngineTests.cs
└─ ChatInputPolicyTests.cs
```

Do not introduce dependency injection frameworks, databases, networking, or other infrastructure.

---

# 7. Plugin Services

Use normal Dalamud `[PluginService]` injection following the official SamplePlugin pattern. The current official template uses static plugin-service properties and an `IDalamudPlugin` lifecycle.

Minimum likely services:

```csharp
[PluginService]
internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

[PluginService]
internal static ICommandManager CommandManager { get; private set; } = null!;

[PluginService]
internal static IPluginLog Log { get; private set; } = null!;

[PluginService]
internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
```

Potentially:

```csharp
[PluginService]
internal static IChatGui ChatGui { get; private set; } = null!;
```

`IChatGui` is optional but useful for displaying an error when a replacement cannot safely be sent.

The plugin does not need network access.

It does not need `IDataManager`, `IObjectTable`, or translation APIs.

---

# 8. Configuration Model

Use Dalamud's normal `IPluginConfiguration`.

Suggested model:

```csharp
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    public bool ApplyToSimpleChatCommands { get; set; } = true;

    public List<ReplacementRule> Rules { get; set; } = [];

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pi)
        => pluginInterface = pi;

    public void Save()
        => pluginInterface?.SavePluginConfig(this);
}
```

Replacement rule:

```csharp
public sealed class ReplacementRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool Enabled { get; set; } = true;

    // Legacy single-find field for older configs; runtime uses Finds / GetEffectiveFinds().
    public string Find { get; set; } = string.Empty;

    // One or more find terms that all map to Replace (UI: pipe-separated, e.g. yes|yea).
    public List<string> Finds { get; set; } = [];

    public string Replace { get; set; } = string.Empty;

    public MatchMode MatchMode { get; set; } = MatchMode.WholeWord;

    public bool CaseSensitive { get; set; } = false;
}
```

Match mode:

```csharp
public enum MatchMode
{
    WholeWord,
    Substring
}
```

The list's order is the rule priority.

Do not require a separate numeric priority field.

---

# 9. Replacement Semantics

The behavior must be deterministic.

## 9.1 Empty values

`Finds` with no non-empty terms (and legacy empty `Find`):

```text
""
```

is invalid and must never execute.

A single rule may list multiple find terms (UI: `yes|yea`) that all map to the same `Replace`. Shared settings (`MatchMode`, `CaseSensitive`, chance) apply to every term on that rule. Chance, when enabled, is rolled once per rule for all of its finds.

`Replace` may be empty.

This allows deletion rules:

```text
"actually" -> ""
```

## 9.2 Whole-word matching

Default mode should be:

```text
WholeWord
```

Example:

```text
nice -> bad
```

should transform:

```text
nice
a nice day
(nice)
nice!
```

but NOT:

```text
nicely
niceness
denice
```

Do not rely blindly on ASCII-only `\b`.

Word-boundary logic should be Unicode-aware.

A reasonable definition is that the character immediately before and after the match must not be a Unicode letter, Unicode number, or underscore.

Equivalent conceptual regex boundaries:

```regex
(?<![\p{L}\p{N}_])
...
(?![\p{L}\p{N}_])
```

However, the implementation does not have to use regex.

For CJK or other languages where "whole word" is unsuitable, the user can choose `Substring`.

## 9.3 Substring matching

Example:

```text
cat -> dog
```

with `Substring` enabled:

```text
cat       -> dog
cats      -> dogs
concatenate -> condogenate
```

This behavior is intentional.

## 9.4 Case sensitivity

Default:

```text
CaseSensitive = false
```

A case-insensitive rule:

```text
nice -> bad
```

matches:

```text
nice
Nice
NICE
NiCe
```

The replacement text should initially be used **exactly as configured**.

Therefore all four become:

```text
bad
```

Do not implement automatic capitalization in MVP.

That can be an optional later feature.

## 9.5 Phrase rules

Multi-word rules MUST work.

Example:

```text
very nice -> absolutely terrible
```

Input:

```text
That was very nice of you.
```

Output:

```text
That was absolutely terrible of you.
```

## 9.6 Multiple occurrences

Every valid occurrence should be replaced.

```text
nice nice nice
```

becomes:

```text
bad bad bad
```

## 9.7 No cascading replacements

Rules MUST operate conceptually against the **original input**, not repeatedly against newly generated replacement text.

Example rules:

```text
nice -> bad
bad  -> awful
```

Input:

```text
nice bad
```

Expected output:

```text
bad awful
```

NOT:

```text
awful awful
```

This is important.

Replacement output should not unexpectedly activate another rule.

---

# 10. Overlapping Rules

Consider:

```text
Rule A: nice      -> bad
Rule B: nice day  -> awful evening
```

Input:

```text
Today is a nice day.
```

The longer match at the same start position should win:

```text
Today is a awful evening.
```

Grammar is the user's responsibility.

Selection policy:

1. leftmost match wins;
2. if multiple candidates begin at the same character, longest find term wins;
3. if length is also equal, earlier rule in configuration wins;
4. selected matches may not overlap.

This makes behavior reproducible.

---

# 11. Recommended Replacement Algorithm

Do NOT simply run:

```csharp
foreach (var rule in rules)
    text = text.Replace(...);
```

because that causes cascading replacements and priority surprises.

Instead:

## Phase 1 — Find candidates

For every enabled rule:

1. inspect the original input;
2. find every applicable occurrence;
3. create a candidate:

```csharp
record ReplacementMatch(
    int Start,
    int Length,
    string Replacement,
    int RulePriority
);
```

## Phase 2 — Order candidates

Sort:

```text
Start ascending
Length descending
RulePriority ascending
```

## Phase 3 — Resolve overlap

Walk candidates left-to-right.

Accept a match only when its start is at or after the end of the previously accepted match.

## Phase 4 — Construct final output

Use a `StringBuilder`:

```text
original section
replacement
original section
replacement
...
remaining original section
```

Never mutate and re-search the output.

For normal FFXIV message sizes and a typical rule count below a few hundred, a straightforward implementation is more than adequate.

Optimize for correctness and readability rather than cleverness.

---

# 12. Outgoing Chat Hook

Create:

```text
Hooks/ChatBoxHook.cs
```

This class should be `unsafe` and implement `IDisposable`.

Conceptual fields:

```csharp
private readonly Configuration configuration;
private readonly ReplacementEngine replacementEngine;
private readonly ChatInputPolicy inputPolicy;
private readonly IPluginLog log;
private readonly IGameInteropProvider gameInteropProvider;

private Hook<UIModule.Delegates.ProcessChatBoxEntry>? processChatBoxHook;
```

## 12.1 Enable

The current API-15 pattern demonstrated by EchoXIV is:

```csharp
processChatBoxHook =
    gameInteropProvider.HookFromAddress<UIModule.Delegates.ProcessChatBoxEntry>(
        (nint)UIModule.MemberFunctionPointers.ProcessChatBoxEntry,
        ProcessChatBoxDetour);

processChatBoxHook.Enable();
```

EchoXIV currently uses this generated delegate/function-pointer pair specifically to avoid maintaining a duplicate handwritten native signature.

The coding agent MUST verify the exact generated signature against the version of FFXIVClientStructs resolved by the current Dalamud SDK at build time.

Do not cargo-cult an old signature if the compiler says the generated delegate changed.

## 12.2 Current detour shape

At the time this specification was written, the current implementation being used in an API-15 plugin has the shape:

```csharp
private void ProcessChatBoxDetour(
    UIModule* uiModule,
    Utf8String* message,
    nint a4,
    bool saveToHistory)
```

and calls:

```csharp
processChatBoxHook.Original(
    uiModule,
    message,
    a4,
    saveToHistory);
```

for the passthrough case.

Again: prefer the generated delegate definition over this document if a future game/API update changes it.

---

# 13. Detour Logic

The detour should be extremely small.

Conceptually:

```csharp
private void ProcessChatBoxDetour(
    UIModule* uiModule,
    Utf8String* message,
    nint a4,
    bool saveToHistory)
{
    try
    {
        if (!configuration.Enabled || message == null)
        {
            Original(...);
            return;
        }

        var originalText = message->ToString();

        if (!inputPolicy.TryGetTransformablePayload(
                originalText,
                out var prefix,
                out var payload))
        {
            Original(...);
            return;
        }

        var transformed = replacementEngine.Transform(payload);

        if (transformed == payload)
        {
            Original(...);
            return;
        }

        var finalText = prefix + transformed;

        var modifiedUtf8 = Utf8String.FromString(finalText);

        try
        {
            Original(
                uiModule,
                modifiedUtf8,
                a4,
                saveToHistory);
        }
        finally
        {
            modifiedUtf8->Dtor(true);
        }
    }
    catch (Exception ex)
    {
        log.Error(ex, "Failed to transform outgoing chat message.");

        Original(
            uiModule,
            message,
            a4,
            saveToHistory);
    }
}
```

This is pseudocode, not a request to copy it without checking nullable/native-pointer details.

---

# 14. Critical Native-Memory Rule

Do NOT modify the incoming `Utf8String` buffer in place.

Instead:

1. read it;
2. transform into managed text;
3. create a new temporary `Utf8String`;
4. call `hook.Original(...)`;
5. destroy the temporary string in `finally`.

A current outgoing-chat implementation follows this lifetime pattern with `Utf8String.FromString(...)`, calls the original function with that temporary value, and explicitly destroys it afterward.

This is simpler and safer than trying to reason about the capacity/ownership of the game's incoming string.

---

# 15. Fail-Open Behavior

This requirement is non-negotiable.

Any exception in:

* input parsing;
* replacement matching;
* string allocation;
* configuration access;
* hook transformation logic;

must result in:

```text
original user message -> game's original function
```

NOT:

```text
exception -> message disappears
```

The user should never lose a chat message because AliasXIV malfunctioned.

Do not attempt a second transformation inside the catch handler.

---

# 16. Slash Command Policy

Blindly replacing the entire string would be dangerous.

Example rule:

```text
party -> solo
```

must NOT transform:

```text
/party Hello
```

into:

```text
/solo Hello
```

Similarly, plugin commands and game commands must not be modified.

Create:

```text
Services/ChatInputPolicy.cs
```

## 16.1 Plain chat

If the message does not begin with `/`:

```text
prefix  = ""
payload = complete message
```

Transform it.

## 16.2 Known simple chat-channel commands

MVP should optionally support chat commands whose payload is simply everything after the command token.

Examples:

```text
/s
/say
/p
/party
/a
/alliance
/y
/yell
/sh
/shout
/fc
```

Also consider the current Linkshell/Cross-world Linkshell command aliases during implementation.

Example:

```text
/p Today is nice
```

should be split into:

```text
prefix  = "/p "
payload = "Today is nice"
```

and reconstructed as:

```text
/p Today is bad
```

The command itself is never passed through the replacement engine.

QuickSymbols uses the same general defensive idea: it bypasses ordinary slash commands while explicitly allowing normal chat-channel commands.

## 16.3 Tell/reply commands

For MVP, bypass explicit:

```text
/t
/tell
/r
/reply
```

commands rather than risk modifying a recipient.

Tell syntax can contain quoted names, world names, and spacing that deserves its own parser.

A user who is already in the active Tell channel and types an ordinary message without a slash prefix will still receive normal transformation.

Tell-command parsing may be Phase 2.

## 16.4 Unknown slash command

Always bypass.

Examples:

```text
/echo nice
/em nice
/ac nice
/whatever nice
```

must reach FFXIV untouched.

Never assume an unknown slash command represents chat text.

---

# 17. Message Payload / Special FFXIV Data

The coding agent must test messages containing:

* Auto-Translate entries;
* item links;
* map links;
* Unicode;
* private-use FFXIV glyphs where applicable.

The fundamental concern is that converting a special encoded chat buffer to a managed string and rebuilding it could potentially lose data that is not plain text.

Therefore:

**MVP safety rule:**

If the outgoing buffer contains chat payload/control data that cannot be proven to round-trip correctly through the implementation, bypass replacement and send the original message unchanged.

Do not corrupt an item link or Auto-Translate payload merely to replace an adjacent word.

Prefer "replacement didn't happen" over "message payload was damaged."

Add integration tests before claiming special-payload support.

---

# 18. Output Length Safety

Replacement can increase message length:

```text
a -> this is a dramatically longer replacement
```

Do not let the plugin truncate messages silently.

A current outgoing-chat plugin uses conservative UTF-8 byte-count guards around the game's chat length and treats approximately 500 UTF-8 bytes as a boundary in its send path.

For AliasXIV:

1. calculate the UTF-8 byte length of the final transformed text;
2. define a conservative maximum compatible with the current game implementation;
3. verify that maximum during implementation against current FFXIV/Dalamud behavior;
4. if transformed output would exceed the supported limit:

   * do NOT silently truncate;
   * send the original unchanged;
   * optionally show a short Dalamud error:
     `AliasXIV: replacement skipped because the result was too long.`

Keep the maximum in one named constant and comment that it must be reverified if game behavior changes.

---

# 19. ReplacementEngine API

Suggested public interface:

```csharp
public sealed class ReplacementEngine
{
    public string Transform(
        string input,
        IReadOnlyList<ReplacementRule> rules);
}
```

Alternative:

```csharp
public string Transform(string input);
```

with rules supplied through the constructor.

Prefer a pure function where practical because it makes testing extremely easy.

The engine:

* must not access Dalamud;
* must not access native pointers;
* must not save configuration;
* must not display UI;
* must not log chat content.

It is purely:

```text
string + rules -> string
```

---

# 20. Configuration UI

Create one normal Dalamud window.

Command:

```text
/aliasxiv
```

opens it.

Optional:

```text
/aliasxiv on
/aliasxiv off
```

may toggle the master switch.

## 20.1 Main controls

Top:

```text
[x] Enable AliasXIV
[x] Apply replacements to simple chat-channel commands
```

Then a rule table.

Suggested columns:

```text
Enabled | Find | Replace With | Match | Case Sensitive | Delete
```

`Find` accepts multiple terms separated by `|` (e.g. `yes|yea`), all replaced with the same `Replace With` value.

Example:

```text
[x] | nice      | bad                | Whole word | [ ] | X
[x] | yes|yea   | qi                 | Whole word | [ ] | X
[x] | very nice | absolutely awful   | Whole word | [ ] | X
```

Button:

```text
+ Add Rule
```

## 20.2 Preview/test area

Include a small preview at the bottom.

```text
Test message:
[ Today is a nice day                       ]

Would send:
Today is a bad day
```

This preview should invoke **the exact same `ReplacementEngine`** used by the outgoing hook.

Never create a second replacement implementation just for UI preview.

## 20.3 Validation

Warn or disable saving/execution for:

* empty `Find` / no effective find terms;
* duplicate rules with identical matching semantics (per find term).

Replacement may be empty.

Do not reject spaces, punctuation, non-English characters, or phrases.

---

# 21. Plugin Lifecycle

`Plugin.cs` should:

1. load configuration;
2. normalize/migrate configuration if necessary;
3. create `ReplacementEngine`;
4. create `ChatInputPolicy`;
5. create config window;
6. create and enable `ChatBoxHook`;
7. register `/aliasxiv`;
8. register UI callbacks.

The official SamplePlugin demonstrates the current command registration, `UiBuilder` subscriptions, configuration loading, and explicit cleanup pattern.

On `Dispose()`:

1. unregister UI callbacks;
2. remove command handlers;
3. remove/dispose windows;
4. dispose `ChatBoxHook`.

The native hook MUST be disposed.

A current implementation of this exact chat hook disposes the `Hook` object during teardown.

---

# 22. Logging and Privacy

Do NOT log the user's outgoing messages by default.

Bad:

```text
INFO Replaced "I secretly hate John" with ...
```

Good:

```text
DEBUG Applied 2 outgoing replacement(s).
```

Exception logging should describe the failure without including the actual chat message.

Example:

```csharp
log.Error(ex, "Failed while processing outgoing chat replacement.");
```

not:

```csharp
log.Error(ex, $"Failed processing: {originalText}");
```

Chat may contain private tells, personal information, linkshell messages, etc.

There is no operational reason for this plugin to persist chat contents.

---

# 23. Performance Requirements

All replacement work occurs synchronously inside the outgoing-chat processing path.

Therefore:

* no network calls;
* no filesystem access;
* no `Task.Run`;
* no async work;
* no locks unless genuinely necessary;
* no expensive recompilation for every message.

A typical transform should complete essentially immediately.

Configuration changes may rebuild a cached set of normalized rule matchers.

However, premature optimization is unnecessary because:

* chat strings are short;
* sends are infrequent;
* expected rule counts are small.

Correctness is more important than micro-optimizing string matching.

---

# 24. Threading

The hook should be treated as game-thread/native-call code.

Do the entire transformation synchronously.

Do not hold native `UIModule*` or `Utf8String*` pointers for use in a later asynchronous callback.

Do not schedule the actual send to another thread.

EchoXIV needs asynchronous handling because translation involves network operations; AliasXIV has no such requirement and should be dramatically simpler. Its current implementation shows the additional complexity required when a hook defers work, which this plugin should avoid.

---

# 25. Compatibility With Other Chat Plugins

Other plugins may hook the same FFXIV function.

Potential examples include outgoing translators.

The exact order of transformations may depend on hook/plugin load order.

Therefore:

* never assume AliasXIV is the only `ProcessChatBoxEntry` hook;
* always call `hook.Original`, not the raw game pointer independently;
* avoid global static state indicating a message is "ours" unless necessary;
* don't attempt to disable other hooks;
* don't perform packet/network interception.

Document that interaction with another outgoing-chat transformation plugin may be order-dependent.

Example:

```text
AliasXIV:
nice -> bad

Translator:
English -> German
```

Depending on hook ordering, another plugin might receive either:

```text
Today is a nice day
```

or:

```text
Today is a bad day
```

This is acceptable for MVP.

---

# 26. Scope Explicitly Excluded From MVP

Do NOT implement these unless the core plugin is complete:

* regular-expression rules;
* cloud translation;
* ChatGPT/LLM transformations;
* incoming-message replacement;
* packet interception;
* automatic message generation;
* timed/automated chat;
* live character-by-character rewriting;
* Chat 2 custom input integration;
* rule profiles per character;
* rule profiles per chat channel;
* capitalization preservation;
* replacement statistics;
* syncing rules to the cloud;
* external configuration databases.

The first version should do one thing well.

---

# 27. Suggested Future Features

Once MVP is stable:

## Phase 2A — Better command parsing

Support safely transforming payloads for:

```text
/tell
/reply
linkshell commands
cross-world linkshell commands
```

without touching recipient/channel syntax.

## Phase 2B — Preserve capitalization

Optional:

```text
nice -> bad

nice -> bad
Nice -> Bad
NICE -> BAD
```

## Phase 2C — Per-rule channel filters

Example:

```text
Rule only applies in:
[x] Say
[x] Party
[ ] Tell
[ ] FC
```

This requires reliably determining the active/current outgoing channel.

Do not build it into MVP unless requested.

## Phase 2D — Regex

Only add regex if users actually need it.

If implemented:

* compile regexes when configuration changes;
* use a strict execution timeout;
* catch regex timeout exceptions;
* never allow malformed/catastrophic regex to prevent the user's message being sent.

## Phase 2E — Import/export

Rules could serialize to JSON for easy sharing.

---

# 28. Unit Test Requirements

`ReplacementEngine` should be comprehensively tested without FFXIV running.

Minimum tests:

### Simple replacement

```text
Rule: nice -> bad

Input:
Today is a nice day

Expected:
Today is a bad day
```

### Repeated replacement

```text
Input:
nice nice nice

Expected:
bad bad bad
```

### Whole-word boundary

```text
Input:
nice nicely niceness

Expected:
bad nicely niceness
```

### Punctuation

```text
Input:
nice, nice! (nice)

Expected:
bad, bad! (bad)
```

### Case insensitive

```text
Rule:
nice -> bad
CaseSensitive = false

Input:
nice Nice NICE

Expected:
bad bad bad
```

### Case sensitive

```text
Rule:
nice -> bad
CaseSensitive = true

Input:
nice Nice NICE

Expected:
bad Nice NICE
```

### Phrase

```text
Rule:
very nice -> very bad

Input:
That was very nice.

Expected:
That was very bad.
```

### No cascading

```text
Rules:
nice -> bad
bad -> awful

Input:
nice bad

Expected:
bad awful
```

### Longest same-position match

```text
Rules:
nice -> bad
nice day -> awful evening

Input:
nice day

Expected:
awful evening
```

### Empty replacement

```text
Rule:
really -> ""

Input:
I really like it

Expected:
I  like it
```

Whitespace cleanup is NOT the replacement engine's responsibility.

### Empty find

Rule is ignored/rejected.

### Disabled rule

No transformation.

### Unicode

Test at minimum:

```text
café
Straße
Japanese text
accented Latin text
emoji surrounding a match
```

### Identical result

```text
nice -> nice
```

should not cause unnecessary native-string reconstruction.

---

# 29. ChatInputPolicy Tests

Examples:

```text
Input:
Today is nice

Transform payload:
Today is nice
```

```text
Input:
/p Today is nice

Prefix:
/p 

Transform payload:
Today is nice
```

```text
Input:
/echo nice

Result:
bypass
```

```text
Input:
/ac "Nice Ability"

Result:
bypass
```

```text
Input:
/tell Character Name@World nice

MVP result:
bypass
```

Unknown slash commands must always bypass.

---

# 30. In-Game Integration Test Checklist

Before calling version 1 complete, manually verify:

* [ ] Plugin loads without errors.
* [ ] `/aliasxiv` opens configuration.
* [ ] Rules persist after `/xlplugins` reload.
* [ ] Master enable/disable works.
* [ ] `nice -> bad` changes an ordinary outgoing Say message.
* [ ] Other players actually receive the transformed string.
* [ ] Local chat history displays the transformed string FFXIV sent.
* [ ] Multiple rules work in one message.
* [ ] Multiple occurrences work.
* [ ] Pasted text is transformed.
* [ ] Editing text before pressing Enter works.
* [ ] Unknown slash commands remain completely untouched.
* [ ] Supported `/p`, `/s`, etc. command payloads work if enabled.
* [ ] Disabling the plugin restores normal behavior immediately.
* [ ] Reloading/unloading the plugin leaves chat functional.
* [ ] An artificial exception in the transformer causes the original message to send.
* [ ] Unicode messages work.
* [ ] Auto-Translate tokens are tested.
* [ ] Item/map links are tested.
* [ ] Oversized transformed output fails open instead of truncating unexpectedly.
* [ ] No outgoing chat content appears in logs.
* [ ] Test alongside at least one other plugin that hooks/modifies outgoing chat if practical.

---

# 31. Definition of Done

MVP is complete when this exact scenario works reliably:

Configuration:

```text
Enabled: yes

Rule 1
Find: nice
Replace: bad
Mode: Whole word
Case sensitive: no
```

Player types into normal FFXIV chat:

```text
Today is a nice day
```

FFXIV processes/sends:

```text
Today is a bad day
```

with:

* no command required;
* no visible intermediate rewrite required;
* no duplicate message;
* no original message sent first;
* no flicker;
* no network service;
* no manual copy/paste;
* no modification to incoming chat.

---

# 32. Recommended Implementation Order for Cursor

Implement in this order.

### Step 1 — Bootstrap project

Start from the current goatcorp SamplePlugin structure.

Set:

```xml
<Project Sdk="Dalamud.NET.Sdk/15.0.0">
```

and:

```xml
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

Do not manually add a separate FFXIVClientStructs package. The current official SamplePlugin confirms the SDK version, while Dalamud itself currently targets .NET 10/x64.

### Step 2 — Configuration and rule model

Implement:

```text
Configuration
ReplacementRule
MatchMode
```

with JSON/Dalamud config persistence.

### Step 3 — Pure ReplacementEngine

Build and unit-test all replacement behavior before touching native code.

The native hook should not be used to debug basic string logic.

### Step 4 — ChatInputPolicy

Implement:

```text
normal text -> transform
known simple chat prefix -> preserve prefix, transform payload
unknown slash command -> bypass
tell/reply -> bypass initially
```

Unit-test it.

### Step 5 — Native ChatBoxHook

Implement the current generated:

```text
UIModule.Delegates.ProcessChatBoxEntry
```

hook using:

```text
IGameInteropProvider
UIModule.MemberFunctionPointers.ProcessChatBoxEntry
```

The current working API-15 reference implementation uses precisely this mechanism.

### Step 6 — Fail-open handling

Before doing UI polish, intentionally make `ReplacementEngine.Transform()` throw and verify the original message still reaches FFXIV.

### Step 7 — Configuration UI

Implement the editable rule table and preview.

### Step 8 — Special payload testing

Test Auto-Translate/item/map-link messages.

Bypass anything that cannot safely round-trip.

### Step 9 — Cleanup

Verify all:

```text
commands
UI callbacks
windows
native hooks
```

are removed/disposed on plugin unload.

---

# 33. Instructions to the Coding Agent

Follow these constraints while implementing:

1. **Do not search for or copy obsolete Dalamud hook examples when current generated FFXIVClientStructs members exist.**
2. Use `Dalamud.NET.Sdk/15.0.0` unless the locally current official SamplePlugin has advanced beyond it when implementation begins.
3. If the SDK version has advanced, update to the version used by the current official goatcorp SamplePlugin rather than pinning this document's value blindly.
4. Let the current Dalamud SDK provide its compatible FFXIVClientStructs.
5. Use the generated `UIModule.Delegates.ProcessChatBoxEntry` delegate and `UIModule.MemberFunctionPointers.ProcessChatBoxEntry`.
6. Confirm the native detour parameters directly from the generated delegate before coding the hook.
7. Keep all native-pointer code isolated in `ChatBoxHook.cs`.
8. Keep replacement logic fully managed and independently unit-testable.
9. Call `hook.Original`, not a separately resolved raw function, when continuing the hook chain.
10. Never alter slash-command names.
11. Never log outgoing message bodies.
12. Never swallow a message because replacement failed.
13. Never truncate a transformed message silently.
14. Never perform network or asynchronous work inside the hook.
15. Never modify the incoming native `Utf8String` buffer in place when a temporary outgoing string is sufficient.
16. Dispose all native resources and hooks cleanly.
17. Prefer a small clear implementation over abstraction-heavy architecture.
18. Do not implement unrequested features before MVP passes all acceptance tests.

---

# 34. Reference Architecture

The strongest current implementation references for this project are conceptually:

```text
Official goatcorp SamplePlugin
        │
        ├── project/API/lifecycle pattern
        │
        ▼
AliasXIV
        │
        ├── native outgoing hook idea
        │       derived from current EchoXIV pattern
        │
        └── replacement safety lessons
                informed by current QuickSymbols behavior
```

The official template currently establishes the API-15 project and normal plugin lifecycle.

EchoXIV currently demonstrates hooking `UIModule.ProcessChatBoxEntry` before outgoing chat processing using the generated FFXIVClientStructs delegate and Dalamud `IGameInteropProvider`.

QuickSymbols demonstrates why manipulating the live chat field is a poorer fit here: whole-field replacement proved visually/behaviorally problematic enough that it instead performs native input editing and only considers trailing replacements.

For this plugin, the EchoXIV-style pre-send hook plus a small synchronous replacement engine is therefore the preferred design.
