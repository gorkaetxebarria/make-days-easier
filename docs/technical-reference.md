# mde technical reference (internal)

Audience: maintainers. This document maps the current implementation so new commands and changes can follow existing design decisions. For installation and daily use, see the [user guide](user-guide.md).

## 1. Stack

| Concern | Choice | Where |
| --- | --- | --- |
| Runtime | .NET 10 (`net10.0`) | `Mde.Tool.csproj` |
| SDK pin | 10.0.401, `rollForward: latestPatch`, prereleases disabled | `global.json` |
| Command parsing | System.CommandLine 2.0.13 | `Directory.Packages.props` |
| Console output | Spectre.Console 0.57.2 | `Directory.Packages.props` |
| Tests | xunit 2.9.3, xunit.runner.visualstudio 3.0.2, Microsoft.NET.Test.Sdk 17.12.0 | `Directory.Packages.props` |
| Package | .NET global tool, `PackageId` `Mde.Tool`, `ToolCommandName` `mde`, MIT | `Mde.Tool.csproj` |

Central package management is enabled, so all versions live in `Directory.Packages.props`. `NuGet.Config` lists only nuget.org and contains no credentials. Users add the GitHub Packages feed themselves (see the user guide).

## 2. Solution layout

```
Mde.slnx                          solution (src/Mde.Tool, tests/Mde.Tool.Tests)
src/Mde.Tool/
  Program.cs                      composition root: builds services and command modules
  Commands/                       one ICommandModule per command (about, init, pr)
  Configuration/                  UserConfiguration, JSON store, OrganizationName, PreferredIde
  Integrations/                   Azure CLI token, ADO REST client, Git, IDE launch, PR preparation
  Platform/                       IPlatform abstraction and DefaultPlatform
scripts/shell/                    mde-pr.sh (bash/zsh), mde-pr.ps1 (PowerShell)
scripts/release/                  pr-title.sh, next-version.sh
tests/Mde.Tool.Tests/             24 fake-driven tests
.github/workflows/                test.yml, pr-title.yml, prerelease.yml, release.yml
```

## 3. Architecture boundaries

- **Commands** parse options, call services, and format output. They do not call processes or HTTP directly.
- **Integrations** wrap external systems: `AzureCliTokenProvider` (az), `AdoPullRequestClient` (ADO REST), `GitClient` and `PullRequestRepositoryPreparer` (git), `IdeLauncher` (code, devenv).
- **Platform** (`IPlatform`) isolates OS facts: configuration directory, Windows detection, interactive-terminal detection, process execution, and IDE detection.
- **Configuration** is a small JSON file; only non-secret settings are stored.
- `Program.cs` is the only place where concrete implementations are wired together. Everything else depends on interfaces, which lets tests substitute fakes.

## 4. Extension model: adding a command

1. Create a class implementing `ICommandModule` in `src/Mde.Tool/Commands/` (see `AboutCommandModule.cs` for the smallest example).
2. Build the `System.CommandLine` command inside the module and validate arguments before any external call.
3. Put external behavior behind an interface in `Integrations/` or `Platform/`, and inject it through the constructor.
4. Register the module in the module list in `Program.cs`. `CommandApplication` assembles the root command from those modules.
5. Add fake-driven tests in `tests/Mde.Tool.Tests/` using the existing `Fake*` helpers. Never call real Azure DevOps or read real credentials in tests.
6. Update the user guide for any new user-visible command or option.

## 5. User configuration and no-secret policy

- Location: `Environment.GetFolderPath(SpecialFolder.ApplicationData)` + `mde/config.json`. On macOS this resolves to `~/Library/Application Support/mde/config.json`; on Windows it is the roaming AppData folder.
- Schema (camelCase, indented): `{"azureDevOpsOrganization": string|null, "preferredIde": null}`.
- Only `azureDevOpsOrganization` is written by `init`. `preferredIde` is reserved: no command sets it yet.
- Organization names are validated against `^[A-Za-z0-9][A-Za-z0-9._-]{0,49}$`; URLs and e-mail addresses are rejected before any write.
- **No-secret rule:** tokens, passwords, and credentials are never written to config, logs, or output. A test asserts the config JSON contains neither "token" nor "secret".
- **No telemetry:** the tool sends no usage or diagnostic data anywhere. Its only network traffic is the Azure DevOps REST calls made by `mde pr`.

## 6. Platform abstraction

`IPlatform` exposes `GetUserConfigurationDirectory`, `IsWindows`, `IsInteractiveTerminal` (true only when stdin, stdout, and stderr are all not redirected), `RunProcessAsync`, and `DetectInstalledIdes`.

- `DetectInstalledIdes()` currently returns an empty list. `--ide vs` on Windows relies on a solution file, not on detection.
- Processes start with `UseShellExecute = false`, arguments in `ArgumentList` (never a shell string), and stdout and stderr redirected by default. Redirection is what keeps child output out of `mde`'s JSON stdout.

## 7. Azure CLI, ADO, Git, and Git Credential Manager

These four components authenticate separately. Keep them separate in code and documentation.

| Component | Used for | Credential handling |
| --- | --- | --- |
| Azure CLI (`az`) | Obtains an Entra access token for the Azure DevOps resource | `az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken --output tsv --only-show-errors`. The token is kept in memory, sent as a Bearer header, and never stored or logged. Failures: `azure-cli-unavailable`, `azure-cli-token-command-failed` (stderr passed as details), `azure-cli-empty-token`. |
| ADO REST (`dev.azure.com`) | Reads PR metadata, iterations, and changed files | Bearer token from Azure CLI. Endpoints: `/_apis/git/pullrequests/{id}?api-version=7.1`, the iterations endpoint, and the iteration changes endpoint with `$top=2000`. Changed files come from the highest iteration. |
| Git | Clones, fetches, and checks out the source branch | Git uses its own configured credential helper. `mde` never passes or reads Git credentials. |
| Git Credential Manager (GCM) | Supplies Git credentials for HTTPS remotes | Installed and configured by Git for Windows or the GCM package. Azure CLI login does not authenticate Git. |

Error mapping for ADO: 404 → `ado-pr-not-found`; 401/403 → `ado-auth-failed`; other statuses → `ado-unexpected-status`; transport failures → `ado-request-failed`. Response bodies are shown as details.

## 8. `mde init` flow

1. Validate `--organization` (if given). Invalid names fail with "Invalid organization name." and exit 1 without writing.
2. Persist `azureDevOpsOrganization` only.
3. Check `az version`, then `az account show`.
4. If Azure CLI is not signed in:
   - Interactive session: asks "Start az login now?" (default no). Accepting runs `az login`.
   - Non-interactive session: prints guidance and exits 1. It never waits for input.
5. Always print the note that Git authentication is handled by Git Credential Manager.

## 9. `mde pr <id>` flow

Options: `--ide code|vs` (default `code`), `--output text|json` (default `text`). `id` must be a positive integer.

1. **Early validation:** invalid id or `--ide vs` on non-Windows fails with exit 1 before any ADO or Git call.
2. **Organization check:** no configured organization → guidance, no JSON, exit 1.
3. **ADO lookup:** get the PR, its iterations, and the changed files (section 7).
4. **Repository preparation** (`PullRequestRepositoryPreparer`):
   1. Validate the PR's `remoteUrl` with `AdoRemoteNormalizer`. Unsupported URL → `ado-remote-invalid`, nothing else runs.
   2. If the current directory is a repo whose `origin` matches the PR repository (organization, project, repository, compared case-insensitively; HTTPS, SSH, and scp forms are equivalent), use it.
   3. Otherwise, if a folder named after the repository exists, check it. A matching repo is used. A non-matching folder fails with `repo-folder-collision`, and nothing is modified.
   4. Otherwise run `git clone <cloneUrl> .` in the current directory. This clones into the current directory itself, so **the current directory must be empty**, or git refuses.
   5. **Dirty-tree stop:** `git status --porcelain --untracked-files=normal` must be empty. Otherwise fail with `repo-dirty-worktree`. Nothing is stashed, reset, or discarded.
   6. `git fetch origin <sourceRef>`.
   7. Local branch = source ref without `refs/heads/`. Existing local branch: `git checkout <branch>`. Otherwise `git checkout --track -b <branch> origin/<branch>`.
   Git failures report the exit code and trimmed stderr.
5. **IDE launch** (`IdeLauncher`), skipped only when preparation fails:
   - `code`: runs `code <repoPath>`.
   - `vs` (Windows only): runs `devenv <solution>`. The solution is the first `.sln` or `.slnx` (alphabetical) in the repo root or one non-hidden directory below. If none exists, prints "No .sln or .slnx file found in the repository; falling back to VS Code." and uses `code`.
   - Launch failure never fails the command: "Warning: could not open VS Code: <reason>. The repository is ready." Exit 0.
6. **Output:**
   - Text: summary (PR number, title, author, target branch, checked-out branch, changed files) on stdout. The final stdout line is the absolute repository path.
   - JSON: diagnostics and summary on stderr. Stdout is one line: `{"schemaVersion":1,"repositoryPath":"<absolute path>"}`. Handled failures print nothing to stdout and exit 1.

### Known discrepancy: argument-parse errors in JSON mode

Verified on macOS: when `--ide` or the id is invalid, System.CommandLine prints the command help to **stdout**, and the validation message goes to stderr, even when `--output json` is given. Handled failures are clean, but argument-parse failures violate the "stdout is only JSON" contract. The shell wrappers are protected because they fail to parse non-JSON output and leave the directory unchanged. Not fixed here (code change out of scope); tracked as a follow-up.

## 10. JSON and wrapper contract

- `schemaVersion` is `1`. Consumers must reject other values.
- `mde-pr` (bash/zsh, `scripts/shell/mde-pr.sh`) runs `command mde pr "$id" "$@" --output json`, parses the single line, and changes directory only if the output matches the schema and the path is an existing directory. Otherwise it prints "mde-pr: unexpected output ...; directory not changed." and returns 1 (or the `mde` exit code).
- `mde-pr` (PowerShell, `scripts/shell/mde-pr.ps1`) takes `-Id` and `-Ide code|vs`, runs `& mde pr $Id --ide $Ide --output json`, checks `$LASTEXITCODE`, parses with `ConvertFrom-Json`, requires `schemaVersion -eq 1`, and calls `Set-Location -LiteralPath`. Any failure leaves the location unchanged. The wrapper does not expose `--output`.

## 11. Testing

- 24 tests in `tests/Mde.Tool.Tests`, all fake-driven:
  - `CommandContractTests` (9 cases): init persistence and rejection, `pr` without org, JSON purity, text ending with path, ADO failure without JSON, `--ide vs` rejection on non-Windows, IDE warning, Windows fallback.
  - `RepositoryPreparationTests` (6): equivalent remotes, clone into empty cwd then fetch and track, folder collision without mutation, dirty stop before fetch, fetch failure stops checkout, unsupported URL.
  - `ExternalIntegrationTests` (2): az failure never echoes a token; ADO 401 via a stub handler.
  - `ShellWrapperTests` (2 methods): bash and zsh behavior (POSIX hosts only, skipped when the shell is absent); PowerShell wrapper checked statically.
- Fakes: `FakeAdoClient`, `FakeGitClient`, `FakeIdeLauncher`, `FakeFileSystem`, `FakePlatform`, `MemoryConfigurationStore`, `RecordingProcessLauncher`, `StubHttpHandler`. Scratch directories live under `artifacts/`.
- **No live Azure DevOps coverage.** Nothing in the suite contacts `dev.azure.com`.
- Local run (macOS, this change): `dotnet test Mde.slnx -c Release --no-restore` → 24 passed, 0 failed.

### CI matrix (`.github/workflows/test.yml`)

- Triggers: push and pull_request.
- Matrix: `ubuntu-latest`, `macos-latest`, `windows-latest` (`fail-fast: false`).
- Setup: `actions/setup-dotnet@v4` with `global-json-file: global.json`.
- Command: `dotnet test Mde.slnx --configuration Release`.
- Windows only: runs the PowerShell wrapper with a fake `mde` function; success changes location, failure keeps it.
- Linux and Windows results are not verified in this document; check the workflow run history.

## 12. Release, versioning, and publishing

- **PR titles** (`pr-title.yml`, on opened/edited/reopened): `[Work-Item-ID] type[!]: description`. ID matches `[A-Za-z0-9][A-Za-z0-9._-]*`. Types: `feat`, `fix`, `bug`, `refactor`, `perf`, `docs`, `style`, `test`, `build`, `ci`, `chore`, `revert`.
- **Bump rules:** `!` → major; `feat` → minor; `fix`/`bug` → patch; other types → no release. A `!` on any type (for example `refactor!`) is major and publishes.
- **Stable release** (`release.yml`, on PR merged into `main`):
  1. Build job parses the title and computes the bump. Non-publishing types only write a step summary.
  2. For a bump, `next-version.sh` computes the version from the highest stable `vX.Y.Z` tag (pre-release tags ignored; 0.0.0 if none). Fails if the tag already exists locally or on origin.
  3. Packs and uploads the `.nupkg` artifact.
  4. Publish job uses the `github-packages` environment, which requires approval. It re-checks the tag, pushes to `https://nuget.pkg.github.com/<owner>/index.json` with `GITHUB_TOKEN` (`packages: write`), then creates and pushes annotated tag `vX.Y.Z`.
- **Pre-release** (`prerelease.yml`, on push to any branch except `main`): version `X.Y.(Z+1)-alpha.<run_id>.<run_attempt>`. Publishes through the same protected environment. Creates no tags. No title validation.
- **Known limitation:** publish and tag are not atomic. If the tag push fails after a successful publish, re-runs collide on the feed. Recovery: create the tag manually at the published commit.
- Approval is the control point: a maintainer approves the `github-packages` deployment before any package reaches the feed.
- **Admin setup** (repository settings, not in YAML): Settings → Environments → `github-packages`:
  1. Add **Required reviewers**; workflow YAML cannot name reviewers.
  2. Under **Deployment branches and tags**, allow all branches, because alpha builds publish from any non-`main` branch. An allow-list blocks alpha publishing for unlisted branches.
- Make the `PR title` workflow a required status check in branch protection so invalid titles cannot merge.
