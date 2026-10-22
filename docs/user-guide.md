# mde user guide

`mde` is a command-line helper for daily Azure DevOps pull request work. It can set up your Azure DevOps organization, check your tools, and prepare a local copy of a pull request's source branch in your IDE.

For the internal design, see the [technical reference](technical-reference.md).

Placeholders in angle brackets, such as `<org>` or `<X.Y.Z>`, must be replaced with your own values. Never paste a real token into a file you share or commit.

## 1. Prerequisites

| Requirement | Needed for | Notes |
| --- | --- | --- |
| .NET SDK 10 | Installing the tool | Version 10.0.401 or a later patch. Check with `dotnet --version`. |
| Azure CLI (`az`) | `mde init`, `mde pr` | Sign in with `az login`. `mde` requests an access token from it; it never stores one. |
| Git | `mde pr` | Must be on your PATH. |
| Git Credential Manager (GCM) | Git access to Azure Repos over HTTPS | Usually bundled with Git for Windows; on macOS and Linux install it from the GCM releases or your package manager. |
| VS Code (`code` on PATH) | Default IDE for `mde pr` | Optional if you always use `--ide vs` on Windows. |
| Visual Studio (Windows) | `--ide vs` | Optional. Needs `devenv` on PATH. |
| GitHub personal access token (classic or fine-grained) with `read:packages` | Installing from GitHub Packages | Use the minimum scope. Store it in your password manager, not in files. |

### Azure CLI and Git Credential Manager are separate

- **Azure CLI** signs you in to Azure. `mde` uses it only to obtain a short-lived access token for the Azure DevOps API. The token stays in memory and is discarded when the command ends.
- **Git Credential Manager** handles authentication when Git talks to Azure Repos over HTTPS. `az login` does **not** sign Git in. If `git clone` or `git fetch` asks for credentials, the problem is GCM, not Azure CLI.

`mde` never reads, stores, or prints credentials, and it does not send telemetry.

## 2. Install from GitHub Packages

Releases are published to the GitHub Packages NuGet feed of the repository owner. Replace `<owner>` with the owner name in lowercase, and `<GITHUB_USERNAME>` with your GitHub username.

Feed URL pattern:

```text
https://nuget.pkg.github.com/<owner>/index.json
```

### Step 1: add the feed with credentials

```bash
dotnet nuget add source "https://nuget.pkg.github.com/<owner>/index.json" \
  --name github-<owner> \
  --username <GITHUB_USERNAME> \
  --password <PAT_WITH_read:packages> \
  --store-password-in-clear-text
```

- `--store-password-in-clear-text` is required on macOS and Linux, where NuGet cannot encrypt the stored token. On Windows, omit it so NuGet encrypts the password.
- Anyone who can read your user profile can read that token. Use a token with only `read:packages`, set an expiry date, and remove the source when you no longer need it.
- Do not write the token into a `NuGet.Config` that lives in a repository.

### Step 2: install the tool

```bash
dotnet tool install --global Mde.Tool --version <X.Y.Z> --add-source "https://nuget.pkg.github.com/<owner>/index.json"
```

Check it:

```bash
mde --version
mde --help
```

If `mde` is not found, your global tool path is missing from PATH. See troubleshooting.

### Update

```bash
dotnet tool update --global Mde.Tool --version <X.Y.Z> --add-source "https://nuget.pkg.github.com/<owner>/index.json"
```

Omit `--version` to take the latest stable release. Add `--prerelease` only if your team asks you to test a pre-release build.

### Uninstall

```bash
dotnet tool uninstall --global Mde.Tool
```

To also remove the feed credentials:

```bash
dotnet nuget remove source github-<owner>
```

Your configuration file is not removed by uninstalling. Delete it manually if you want a clean state (see section 4).

## 3. Release process (for context)

Releases are published only after a maintainer approves the `github-packages` deployment. A merged pull request into `main` creates a stable version according to its title (`feat` → minor, `fix`/`bug` → patch, `!` → major). Pre-release builds from other branches are published the same way but do not create tags. If a version you expect is missing from the feed, check the release workflow run and its approval status with the maintainers.

## 4. First-time setup

1. Sign in to Azure CLI:

   ```bash
   az login
   ```

2. Set your Azure DevOps organization. Use the organization name only, not a URL:

   ```bash
   mde init --organization <org>
   ```

   `mde init` checks that the name is valid, saves it, and checks Azure CLI. In an interactive terminal it also asks for your preferred IDE (VS Code, VS Code Insiders, Visual Studio, Rider, or `Other / skip`, which leaves the saved value unchanged). In scripts, pass `--ide code|insiders|vs|rider` to save it, or `--ide none` to clear it; when omitted non-interactively, the saved value is unchanged. The setting is only stored for now; `mde pr` still uses `--ide`. If you are not signed in, it asks whether to start `az login` now (default: no). In a script or other non-interactive session it prints guidance and exits with an error instead.

3. Confirm the setup:

   ```bash
   mde about
   ```

   Example output:

   ```text
   mde is ready.
   Azure DevOps organization: <org>
   Preferred IDE: not configured
   ```

   `mde about` always prints "mde is ready." even if the organization is not configured. Check the organization line.

The configuration file contains only the organization name and preferred IDE:

- macOS: `~/Library/Application Support/mde/config.json`
- Windows: `%APPDATA%\mde\config.json`
- Linux: under the .NET `ApplicationData` location for your user (typically `~/.config/mde/config.json`; verify on your machine before relying on it).

Do not add tokens or passwords to this file. `mde` does not use them.

## 5. Prepare a pull request: `mde pr <id>`

Run from the folder where you want the repository to be:

```bash
mde pr <id>
```

`<id>` is the numeric pull request id from Azure DevOps (a positive integer).

What it does:

1. Reads the pull request from your organization.
2. Finds the repository:
   - If your current folder is a clone of the same repository, it uses that folder.
   - Otherwise, if a folder with the repository's name exists here and is a clone of the same repository, it uses that folder.
   - Otherwise it clones the repository **into the current folder**. The current folder must be **empty**; otherwise Git refuses to clone.
3. If the working tree has uncommitted or untracked changes, asks whether to stash them (interactive text mode only); stops if you decline or the session is not interactive.
4. Fetches the pull request's source branch and checks it out.
5. Opens the repository in your IDE.
6. Prints a summary and the repository path.

### Options

| Option | Values | Default | Meaning |
| --- | --- | --- | --- |
| `--ide` | `code`, `insiders`, `vs` | `code` | `code` opens VS Code, `insiders` opens VS Code Insiders (`code-insiders`). `vs` opens Visual Studio and works only on Windows. |
| `--output` | `text`, `json` | `text` | `json` prints machine-readable output (section 6). |

Examples:

```bash
mde pr <id>
mde pr <id> --ide code
mde pr <id> --ide insiders
mde pr <id> --ide vs          # Windows only
mde pr <id> --output json
```

### Safety behavior

- **Folder collision:** If a folder with the repository's name exists but is a different repository, `mde` stops and changes nothing. Move or rename that folder, then run the command again.
- **Dirty working tree:** If your repository has uncommitted or untracked changes, `mde` asks whether to stash them (`git stash push --include-untracked`) before fetching or checking out. Restore them later with `git stash pop`. If you decline, or the session is non-interactive or uses `--output json`, `mde` stops and changes nothing. `mde` never discards changes.
- **Visual Studio on macOS or Linux:** `--ide vs` fails immediately with exit code 1, before any network call.
- **No solution for `--ide vs`:** On Windows, if no `.sln` or `.slnx` file is found in the repository root or one folder below it, `mde` prints that it is falling back to VS Code and opens VS Code.
- **IDE not opening:** If the IDE fails to start, `mde` prints a warning and still succeeds, because the repository is ready. Open the printed path manually.
- **Missing organization:** Run `mde init` first. `mde pr` does not contact Azure DevOps without it.
- **Pull request not found:** The command fails and does not clone or check out anything.

## 6. JSON output and shell wrappers

### JSON output

```bash
mde pr <id> --output json
```

On success, standard output is exactly one line of JSON:

```json
{"schemaVersion":1,"repositoryPath":"<absolute path to the repository>"}
```

- Progress and the summary go to standard error, so you can pipe standard output safely.
- On a handled failure, standard output is empty and the exit code is 1.
- Check `schemaVersion` before relying on the fields. Version `1` is the only one defined.

Known issue: if you pass an invalid argument (for example a non-numeric id or an unsupported `--ide` value), the command help may be printed to standard output even with `--output json`. Do not rely on standard output being JSON for argument errors; check the exit code first.

### Shell wrappers

The wrappers run `mde pr`, then change into the repository folder only when the result is valid. Source the wrapper for your shell from the repository's `scripts/shell` folder.

Bash or Zsh:

```bash
source <path-to-repo>/scripts/shell/mde-pr.sh
mde-pr <id>
mde-pr <id> --ide code
```

PowerShell:

```powershell
. <path-to-repo>\scripts\shell\mde-pr.ps1
mde-pr -Id <id>
mde-pr -Id <id> -Ide vs
```

If the output is unexpected, the wrapper prints `mde-pr: unexpected output ...; directory not changed.` and leaves you in your current folder. The PowerShell wrapper accepts only `-Ide code` or `-Ide vs`. It does not accept `--output`.

## 7. Troubleshooting

| Symptom | Likely cause | What to do |
| --- | --- | --- |
| `Invalid organization name.` | You passed a URL or e-mail address | Use only the organization name, for example `<org>`. |
| `mde pr` asks for guidance about the organization | `mde init` not run | Run `mde init --organization <org>`. |
| Azure CLI token error (`azure-cli-unavailable`) | `az` is not installed or not on PATH | Install Azure CLI and open a new terminal. |
| Azure CLI token error (`azure-cli-token-command-failed`) | Not signed in or session expired | Run `az login`, then retry. The details line shows the Azure CLI message. |
| `ado-auth-failed` (401/403) | Your account lacks access to the repository or the organization | Check your Azure DevOps permissions. Re-run `az login` if the account is wrong. |
| `ado-pr-not-found` (404) | Wrong id or wrong organization | Check the pull request id and the organization in `mde about`. |
| `repo-folder-collision` | A folder with the repository name holds a different repository | Rename or move that folder, then retry. |
| `repo-dirty-worktree` | Uncommitted or untracked changes | Commit or stash them, then retry. |
| Git asks for a username or password | Git Credential Manager has no valid credentials | Authenticate through GCM (for example, run `git fetch` in the repository and complete the prompt). `az login` does not fix this. |
| `ado-remote-invalid` | The pull request's repository URL is not in a supported form | Report it to the maintainers; you cannot fix it locally. |
| `Warning: could not open VS Code` | `code` not on PATH | Install the VS Code shell command, or open the printed path manually. The repository is still ready. |
| `mde` not found after install | Global tool path not on PATH | Add `~/.dotnet/tools` (macOS/Linux) or `%USERPROFILE%\.dotnet\tools` (Windows) to PATH and open a new terminal. |
| Install fails with 401/403 | Token missing, expired, or lacks `read:packages` | Create a new token with `read:packages`, then run `dotnet nuget update source github-<owner> --username <GITHUB_USERNAME> --password <PAT_WITH_read:packages> --store-password-in-clear-text` (or omit that flag on Windows). |
| Install cannot find `Mde.Tool` | Feed or version wrong | Check the feed URL and version with the maintainers. |
| `--ide vs` fails on macOS or Linux | Visual Studio exists only on Windows | Use the default `--ide code`. |

If a problem persists, collect the command you ran, the exit code, and the error text (remove any token or path you do not want to share), then contact the maintainers.
