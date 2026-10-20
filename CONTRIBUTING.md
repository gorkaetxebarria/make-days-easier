# Contributing to make-days-easier

Thanks for helping improve `mde`. This guide covers how to set up the repository, build and test it, pick or open issues, and get changes merged. For what the tool does, see the [user guide](docs/user-guide.md); for internals, see the [technical reference](docs/technical-reference.md).

## 1. Prerequisites

| Requirement | Needed for | Notes |
| --- | --- | --- |
| .NET SDK 10.0.401 or a later patch | Building, testing, packing | Pinned in `global.json` with `rollForward: latestPatch`. Check with `dotnet --version`. |
| Git | Cloning and branching | Required. |
| Azure CLI (`az`) | Running `mde init` / `mde pr` against Azure DevOps | Not needed to build or test. |
| Visual Studio or VS Code | Optional | Any editor works. |

## 2. Repository setup

```bash
git clone https://github.com/gorkaetxebarria/make-days-easier.git
cd make-days-easier
dotnet restore Mde.slnx
```

Package versions are managed centrally in `Directory.Packages.props`. NuGet sources are declared in `NuGet.Config` (public nuget.org only). Do not add credentials to any file in the repository.

## 3. Build, run, and test

Run these from the repository root.

| Task | Command |
| --- | --- |
| Restore | `dotnet restore Mde.slnx` |
| Build | `dotnet build Mde.slnx -c Release --no-restore` |
| Test | `dotnet test Mde.slnx -c Release --no-restore` |
| Run from source | `dotnet run --project src/Mde.Tool -c Release -- --help` |
| Pack the tool | `dotnet pack src/Mde.Tool/Mde.Tool.csproj -c Release -o artifacts -p:PackageVersion=0.1.0` |
| Install the packed tool | `dotnet tool install --global Mde.Tool --version 0.1.0 --add-source artifacts` |

Notes:

- The tests are fake-driven. They never contact Azure DevOps and never read credentials, so they run without `az login`.
- Pack output goes to `artifacts/`, `bin/`, and `obj/`. These are git-ignored; do not commit them.
- Before installing globally, uninstall any existing global `Mde.Tool` with `dotnet tool uninstall --global Mde.Tool`.
- After installing, `mde --help` and `mde about` confirm the install.

### Formatting and validation

The repository has no formatter or linter configured, and CI does not run one. Validation means:

- A clean `dotnet build` and a passing `dotnet test`.
- A PR title that passes the title check (see section 6). Run it locally with `bash scripts/release/pr-title.sh "[12] docs: add contributor guide"`.

## 4. Selecting or creating issues

- Browse [open issues](https://github.com/gorkaetxebarria/make-days-easier/issues). Labels indicate the kind of work (`type:bug`, `type:feature`, `type:docs`, `type:chore`, `type:spike`) and priority (`priority:p0`–`priority:p2`). `squad:*` labels show the assigned team member.
- Check the issue for an existing assignee or linked PR before starting, to avoid duplicate work.
- To propose work that has no issue, open one first. Describe the problem, the expected behavior, and any alternatives you considered.

## 5. Branch and pull request flow

All work follows this path. `main` is protected: **never push directly to `main`**.

1. **Branch from `dev`** and name the branch `squad/{issue-number}-{kebab-case-slug}`:
   ```bash
   git checkout dev
   git pull origin dev
   git checkout -b squad/12-docs-contributor-guide
   ```
2. **Make focused changes** and commit them (section 7).
3. **Open a pull request into `dev`** (not `main`). Use the title format in section 6 and put `Closes #{issue-number}` in the description:
   ```bash
   git push -u origin squad/12-docs-contributor-guide
   gh pr create --base dev --title "[12] docs: add contributor guide" --body "Closes #12"
   ```
4. **Wait for the CI checks** (`Validate PR title` and the three `Build and test` jobs) to pass.
5. **Merge into `dev`.** When `dev` is ready for a stable release, a maintainer opens a PR from `dev` into `main`. Merging into `main` publishes a stable package and creates a `vX.Y.Z` tag only when the PR title carries a release bump (section 6).
6. **Clean up** the branch after it is merged.

Hotfixes branch from `main` as `hotfix/{slug}` and open a PR into `dev`.

## 6. Pull request titles

Titles must match:

```text
[Work-Item-ID] type[!]: description
```

- `Work-Item-ID` is the issue number, for example `[12]`. It may contain letters, digits, `.`, `_`, and `-`, and must start with a letter or digit.
- `type` is one of `feat`, `fix`, `bug`, `refactor`, `perf`, `docs`, `style`, `test`, `build`, `ci`, `chore`, `revert`.
- `!` marks a breaking change.
- The description must not be empty.

The `Validate PR title` job in the `Build validation` workflow checks the title on every PR update. The title also decides the release bump on merge to `main`: `!` is major, `feat` is minor, and `fix`/`bug` is patch. Other types publish nothing.

## 7. Commits and PR expectations

- Keep each PR focused on one issue. Do not mix unrelated changes.
- Stage only the files you intend to change, then review them before committing: `git add -- <path>` and `git diff --cached`.
- Reference the issue in the commit message or PR, for example `docs: add contributor guide (#12)`.
- Never commit tokens, passwords, or other credentials. Tokens are never stored by the tool, and the repository must not contain them either.
- Update documentation with the change:
  - New or changed user-visible command or option: update the [user guide](docs/user-guide.md).
  - New command or internal behavior: update the [technical reference](docs/technical-reference.md). The technical reference describes the extension steps for new commands.
- Add tests for behavior you add or fix. Follow the existing fake-driven tests in `tests/Mde.Tool.Tests/`.
- Ensure build, test, and the title check pass before requesting review.

## 8. Reporting bugs and proposing changes

- **Bugs:** open an issue with the `bug` label. Include the `mde` version, your operating system, the exact command you ran, the expected and actual output, and the steps to reproduce.
- **Proposals:** open an issue describing the problem and the proposed change before writing code for anything non-trivial.
- Redact tokens, organization details, and any other sensitive values from logs and output before posting them.

Thank you for contributing.
