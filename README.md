# make-days-easier
dotnet toolkit que hace mis dias mas faciles

`mde` is a .NET global tool (`Mde.Tool`) packaged for GitHub Packages.

## Build from source

Requires the SDK pinned in `global.json` (10.0.401, roll-forward to later patches).
Package versions are managed centrally in `Directory.Packages.props`; NuGet sources are declared in `NuGet.Config` (public nuget.org only, no credentials).

```bash
dotnet restore Mde.slnx
dotnet build Mde.slnx -c Release --no-restore
dotnet test Mde.slnx -c Release --no-restore
dotnet pack src/Mde.Tool/Mde.Tool.csproj -c Release -o artifacts -p:PackageVersion=0.1.0
dotnet tool install --global Mde.Tool --version 0.1.0 --add-source artifacts
```

Run these from the repository root. The last command installs the package you just packed from `artifacts/`; if a global `Mde.Tool` is already installed, run `dotnet tool uninstall --global Mde.Tool` first.

The test suite (24 fake-driven tests) never contacts Azure DevOps or reads credentials. CI runs it on Ubuntu, macOS, and Windows; see the [technical reference](docs/technical-reference.md#11-testing).

## Install, use, and documentation

- **[User guide](docs/user-guide.md)**: prerequisites, GitHub Packages install/update/uninstall, first-time setup, `mde pr`, JSON output, shell wrappers, troubleshooting.
- **[Technical reference](docs/technical-reference.md)**: architecture, command extension model, configuration and no-secret policy, PR flow, testing and CI, release and versioning.

Quick check after installing:

```bash
mde --help
mde about
```

## Pull request titles

PR titles must match `[Work-Item-ID] type[!]: description`. The `Validate PR title` job in the `Build validation` workflow checks them on every PR update; configure it as a required status check. Details and bump rules are in the [technical reference](docs/technical-reference.md#12-release-versioning-and-publishing).

## CI and publishing

Two product workflows exist:

- **Build validation** (`build-validation.yml`): on every PR update, validates the title and runs restore, build, and test on Windows, macOS, and Linux. Never packages or publishes.
- **Publish** (`publish.yml`): repeats restore, build, and test on all three OSes, then packs the tool. Only the publish job waits for approval in the `github-packages` environment.

Versioning:

- **Stable releases** run when a PR is merged into `main`. Only titles with a bump (`!`, `feat`, `fix`/`bug`) publish a package and tag `vX.Y.Z`.
- **Alpha builds** run on pushes to other branches, publish `X.Y.(Z+1)-alpha.<run_id>.<run_attempt>` (the title is not read for pushes), and create no tags.
- Known limitation: publish and tag are not atomic; see the [technical reference](docs/technical-reference.md#12-release-versioning-and-publishing) for recovery.
