# AliasXIV Release Procedure

This document describes how to publish AliasXIV to the custom Dalamud plugin repository hosted on GitHub.

## Repository

| Field | Value |
|---|---|
| GitHub owner | `L1ghth0us3` |
| GitHub repo | `AliasXIV` |
| GitHub URL | https://github.com/L1ghth0us3/AliasXIV |
| Default branch | `master` |

## Project Paths

| Field | Path |
|---|---|
| Solution | `AliasXIV.sln` |
| Plugin project | `AliasXIV/AliasXIV.csproj` |
| Plugin manifest stub | `AliasXIV/AliasXIV.json` |
| Custom repository manifest | `pluginmaster.json` (repo root) |
| Test project | `AliasXIV.Tests/AliasXIV.Tests.csproj` |

## Version Source

The plugin version is defined in `AliasXIV/AliasXIV.csproj`:

```xml
<Version>1.0.0.0</Version>
```

Before each release, update this value. The same version must appear in:

- the generated plugin manifest inside the ZIP (`AssemblyVersion`)
- `pluginmaster.json` (`AssemblyVersion`)
- the Git tag (`v{Version}`, e.g. `v1.0.0.0`)

## SDK and Packaging

- SDK: `Dalamud.NET.Sdk/15.0.0` (Dalamud API 15)
- Packaging: handled automatically by DalamudPackager via the SDK
- Release build output ZIP: `AliasXIV/bin/x64/Release/AliasXIV/latest.zip`

The ZIP should contain only plugin files (typically `AliasXIV.dll`, `AliasXIV.json`, and `AliasXIV.deps.json`). It must **not** include Dalamud framework DLLs.

If the ZIP contains stale artifacts from an old project name, clean before building:

```bash
dotnet clean -c Release
rm -rf AliasXIV/bin AliasXIV/obj
```

## Custom Repository URL

Users add this stable URL in Dalamud (`/xlplugins` → Settings → Custom Plugin Repositories):

```
https://raw.githubusercontent.com/L1ghth0us3/AliasXIV/master/pluginmaster.json
```

This URL does not change between releases. Only `pluginmaster.json` is updated on each release.

## Tag and Asset Conventions

| Item | Convention |
|---|---|
| Git tag | `v{Version}` from csproj (e.g. `v1.0.0.0`) |
| Release asset filename | `latest.zip` |
| Download URL pattern | `https://github.com/L1ghth0us3/AliasXIV/releases/download/v{Version}/latest.zip` |

## Identity Verification (Required Before Remote Writes)

All GitHub pushes, tags, and releases must use the personal `L1ghth0us3` account. Verify before publishing:

```bash
git config user.name
git config user.email
git remote -v
gh auth status
gh api graphql -f query='{ viewer { login } }'
```

Expected:

- `gh auth status` shows logged in as `L1ghth0us3`
- GraphQL viewer login is `L1ghth0us3`
- Repo-local git identity is `L1ghth0us3` with the GitHub noreply email

Do not publish if authentication belongs to a different account.

## Release Workflow

### 1. Prepare

1. Read this file.
2. Inspect changes since the previous release.
3. Update `<Version>` in `AliasXIV/AliasXIV.csproj` if needed.
4. Ensure the working tree contains only intentional changes.

### 2. Build and Verify

```bash
dotnet test -c Release
dotnet clean -c Release
rm -rf AliasXIV/bin AliasXIV/obj   # if stale build artifacts are present
dotnet build -c Release
```

Verify:

```bash
unzip -l AliasXIV/bin/x64/Release/AliasXIV/latest.zip
cat AliasXIV/bin/x64/Release/AliasXIV/AliasXIV.json
```

Confirm:

- build and tests succeed
- ZIP contains only plugin files (no Dalamud DLLs, no stale artifacts)
- packaged `AssemblyVersion` matches csproj `<Version>`
- packaged `DalamudApiLevel` is correct (currently `15`)

### 3. Update pluginmaster.json

Edit `pluginmaster.json` at the repo root:

- set `AssemblyVersion` to the new version
- set `DownloadLinkInstall` and `DownloadLinkUpdate` to the new release asset URL
- set `LastUpdate` to the current Unix epoch (`date +%s`)

### 4. Commit and Push

```bash
git add AliasXIV/AliasXIV.csproj pluginmaster.json
git commit -m "Release v{Version}"
git push origin master
```

For the first release or when creating a new remote:

```bash
gh repo create L1ghth0us3/AliasXIV --public --source . --remote origin --push
```

### 5. Create Tag and GitHub Release

Verify the tag does not already exist:

```bash
git tag -l "v{Version}"
gh release view "v{Version}" 2>/dev/null || true
```

Create and push the tag:

```bash
git tag "v{Version}"
git push origin "v{Version}"
```

Create the GitHub Release and upload the ZIP:

```bash
gh release create "v{Version}" \
  --title "v{Version}" \
  --notes "Release v{Version}" \
  "AliasXIV/bin/x64/Release/AliasXIV/latest.zip#latest.zip"
```

### 6. Post-Release Verification

```bash
curl -sI "https://raw.githubusercontent.com/L1ghth0us3/AliasXIV/master/pluginmaster.json" | head -1
curl -sI "https://github.com/L1ghth0us3/AliasXIV/releases/download/v{Version}/latest.zip" | head -1
```

Both should return `HTTP/2 200`.

Confirm `pluginmaster.json` on `master` references the correct version and download URL.

### 7. In-Game Test

1. Add the custom repository URL to Dalamud.
2. Open `/xlplugins`.
3. Install or update AliasXIV.
4. Confirm the plugin loads in-game.

## Safety Rules

- Do not force-push unless explicitly authorized.
- Do not overwrite existing release tags.
- Do not delete existing GitHub releases without asking.
- Do not publish a failed build.
- Do not publish a ZIP whose version differs from `pluginmaster.json`.
- Do not expose authentication tokens or secrets in this document or commits.

## Automation

No GitHub Actions release workflow is configured. Releases are performed manually using the steps above. A workflow may be added later if it materially improves reliability.
