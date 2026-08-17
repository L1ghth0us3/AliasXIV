# AliasXIV

A Dalamud plugin that rewrites your outgoing FFXIV chat before the game sends it. You define rules like `nice -> bad`, type "Today is a nice day", and everyone receives "Today is a bad day".

## Install

1. In-game, open the Dalamud settings with `/xlsettings`
2. Go to the **Experimental** tab
3. Under **Custom Plugin Repositories**, paste this and press the **+**

   ```
   https://raw.githubusercontent.com/L1ghth0us3/AliasXIV/master/pluginmaster.json
   ```

4. Press **Save**
5. Open the Plugin Installer, search **AliasXIV**, and install it
6. Open the rules editor with `/aliasxiv`

## How it works

AliasXIV hooks the game's outgoing chat processing. When you press Enter, your message is transformed and the result is what FFXIV actually sends. There is no visible rewrite in the input field and no duplicate message.

### Rules

- Each rule maps one or more find terms to a replacement. Separate terms with `|`, so `yes|yea -> qi` replaces both words.
- **Whole word** matching is the default. `nice` matches `nice!` but not `nicely`. Switch to **Substring** if you want matches inside words.
- Matching is case insensitive unless you turn on case sensitivity per rule.
- An empty replacement deletes the matched word.
- Rules run against your original text once. A rule's output never triggers another rule.
- The editor has a live preview, so you can test a message before relying on a rule.

### Chance

Each rule can fire on a percentage roll instead of every time. The roll can apply once per message, once per occurrence, or be set per rule.

### Channels

Replacements only run in channels you enable, picked from Say, Party, Free Company, linkshells, and the rest. Slash commands for enabled channels work too, so `/p Today is nice` becomes `/p Today is bad`. The command itself is never touched, and unknown slash commands like `/echo` or `/ac` pass through untouched.

### Sharing rules

Rules can be exported to a JSON file and imported from one, so you can back them up or share them.

### Safety

Your message is never lost. If anything goes wrong during replacement, the original text is sent as typed. The same applies when a replacement would push the message over the game's length limit. Messages containing item links or Auto-Translate entries are passed through unchanged rather than risk corrupting them. Chat contents are never written to logs.

## Commands

| Command | Effect |
|---|---|
| `/aliasxiv` | Open the rules editor |
| `/aliasxiv on` | Enable replacements |
| `/aliasxiv off` | Disable replacements |

Built on [Dalamud](https://github.com/goatcorp/Dalamud). Not affiliated with Square Enix.
