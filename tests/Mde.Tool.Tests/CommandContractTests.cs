using System.Text.Json;
using Mde.Tool.Commands;
using Mde.Tool.Configuration;
using Mde.Tool.Integrations;
using Mde.Tool.Platform;
using Spectre.Console;

namespace Mde.Tool.Tests;

public sealed class CommandContractTests {
    [Fact]
    public async Task Init_persists_only_a_valid_non_secret_organization() {
        var directory = CreateScratchDirectory();
        try {
            var platform = new FakePlatform { ConfigurationDirectory = directory };
            platform.ProcessResults.Enqueue(new PlatformProcessResult(0, "", ""));
            platform.ProcessResults.Enqueue(new PlatformProcessResult(0, "", ""));
            var output = new StringWriter();
            var app = new CommandApplication([new InitCommandModule(ConsoleFor(output), platform, new JsonUserConfigurationStore(platform))]);

            var exitCode = await app.InvokeAsync(["init", "--organization", "contoso-dev"]);

            Assert.Equal(0, exitCode);
            var json = await File.ReadAllTextAsync(Path.Combine(directory, "config.json"));
            using var config = JsonDocument.Parse(json);
            Assert.Equal("contoso-dev", config.RootElement.GetProperty("azureDevOpsOrganization").GetString());
            Assert.Equal(JsonValueKind.String, config.RootElement.GetProperty("azureDevOpsOrganization").ValueKind);
            Assert.True(config.RootElement.TryGetProperty("preferredIde", out var preferredIde));
            Assert.Equal(JsonValueKind.Null, preferredIde.ValueKind);
            Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        }
        finally { DeleteScratchDirectory(directory); }
    }

    [Theory]
    [InlineData("code", "visualStudioCode")]
    [InlineData("insiders", "visualStudioCodeInsiders")]
    [InlineData("vs", "visualStudio")]
    [InlineData("rider", "rider")]
    public async Task Init_persists_the_selected_ide_and_keeps_the_organization(string ide, string expected) {
        var directory = CreateScratchDirectory();
        try {
            var platform = new FakePlatform { ConfigurationDirectory = directory };
            var store = new JsonUserConfigurationStore(platform);
            await store.SaveAsync(new UserConfiguration("contoso"));
            var app = new CommandApplication([new InitCommandModule(ConsoleFor(new StringWriter()), platform, store)]);

            Assert.Equal(0, await app.InvokeAsync(["init", "--ide", ide]));

            using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "config.json")));
            Assert.Equal(expected, config.RootElement.GetProperty("preferredIde").GetString());
            Assert.Equal("contoso", config.RootElement.GetProperty("azureDevOpsOrganization").GetString());
        }
        finally { DeleteScratchDirectory(directory); }
    }

    [Fact]
    public async Task Init_without_ide_succeeds_and_leaves_the_saved_ide_unchanged() {
        var directory = CreateScratchDirectory();
        try {
            var platform = new FakePlatform { ConfigurationDirectory = directory };
            var store = new JsonUserConfigurationStore(platform);
            await store.SaveAsync(new UserConfiguration("contoso", PreferredIde.Rider));
            var app = new CommandApplication([new InitCommandModule(ConsoleFor(new StringWriter()), platform, store)]);

            Assert.Equal(0, await app.InvokeAsync(["init"]));
            Assert.Equal(PreferredIde.Rider, (await store.LoadAsync()).PreferredIde);

            Assert.Equal(0, await app.InvokeAsync(["init", "--ide", "none"]));
            Assert.Null((await store.LoadAsync()).PreferredIde);
        }
        finally { DeleteScratchDirectory(directory); }
    }

    [Fact]
    public async Task Legacy_configuration_without_preferred_ide_loads_and_init_adds_it() {
        var directory = CreateScratchDirectory();
        try {
            var platform = new FakePlatform { ConfigurationDirectory = directory };
            await File.WriteAllTextAsync(Path.Combine(directory, "config.json"), "{\"azureDevOpsOrganization\":\"legacy\"}");
            var store = new JsonUserConfigurationStore(platform);

            var legacy = await store.LoadAsync();
            Assert.Equal("legacy", legacy.AzureDevOpsOrganization);
            Assert.Null(legacy.PreferredIde);

            var app = new CommandApplication([new InitCommandModule(ConsoleFor(new StringWriter()), platform, store)]);
            Assert.Equal(0, await app.InvokeAsync(["init", "--ide", "code"]));
            var updated = await store.LoadAsync();
            Assert.Equal("legacy", updated.AzureDevOpsOrganization);
            Assert.Equal(PreferredIde.VisualStudioCode, updated.PreferredIde);
        }
        finally { DeleteScratchDirectory(directory); }
    }

    [Fact]
    public async Task Init_rejects_unknown_ide_without_writing_configuration() {
        var directory = CreateScratchDirectory();
        try {
            var platform = new FakePlatform { ConfigurationDirectory = directory };
            var app = new CommandApplication([new InitCommandModule(ConsoleFor(new StringWriter()), platform, new JsonUserConfigurationStore(platform))]);

            Assert.NotEqual(0, await app.InvokeAsync(["init", "--ide", "emacs"]));
            Assert.False(File.Exists(Path.Combine(directory, "config.json")));
        }
        finally { DeleteScratchDirectory(directory); }
    }
    [Theory]
    [InlineData("https://dev.azure.com/contoso")]
    [InlineData("contoso@example.com")]
    public async Task Init_rejects_URL_like_organization_without_writing_configuration(string organization) {
        var directory = CreateScratchDirectory();
        try {
            var output = new StringWriter();
            var platform = new FakePlatform { ConfigurationDirectory = directory };
            var app = new CommandApplication([new InitCommandModule(ConsoleFor(output), platform, new JsonUserConfigurationStore(platform))]);

            var exitCode = await app.InvokeAsync(["init", "--organization", organization]);

            Assert.Equal(1, exitCode);
            Assert.Contains("Invalid organization name.", output.ToString());
            Assert.False(File.Exists(Path.Combine(directory, "config.json")));
        }
        finally { DeleteScratchDirectory(directory); }
    }

    [Fact]
    public async Task Pr_without_an_organization_gives_setup_guidance_and_no_success_json() {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        await WithConsoleOutAsync(stdout, async () => {
            var app = CreatePrApplication(new UserConfiguration(), new FakePlatform(), stderr, out _, out _);
            var exitCode = await app.InvokeAsync(["pr", "42", "--output", "json"]);

            Assert.Equal(1, exitCode);
        });

        Assert.Empty(stdout.ToString());
        Assert.Contains("mde init", stderr.ToString());
        Assert.Contains("organization", stderr.ToString());
    }

    [Fact]
    public async Task Pr_json_stdout_is_one_object_and_summary_is_diagnostic_only() {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var repository = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "prepared-repo"));
        await WithConsoleOutAsync(stdout, async () => {
            var app = CreatePrApplication(new UserConfiguration("contoso"), new FakePlatform(), stderr, out _, out var git, repository);
            var exitCode = await app.InvokeAsync(["pr", "42", "--output", "json"]);

            Assert.Equal(0, exitCode);
            Assert.Contains($"fetch:{repository}:origin:refs/heads/feature/one", git.Calls);
        });

        using var json = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(repository, json.RootElement.GetProperty("repositoryPath").GetString());
        Assert.DoesNotContain("PR #42", stdout.ToString());
        Assert.Contains("Add safe tests", stderr.ToString());
        Assert.Contains("src/changed.cs", stderr.ToString());
    }

    [Fact]
    public async Task Pr_human_summary_ends_with_the_absolute_repository_path() {
        var stdout = new StringWriter();
        var diagnostics = new StringWriter();
        var repository = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "human-repo"));
        await WithConsoleOutAsync(stdout, async () => {
            var app = CreatePrApplication(new UserConfiguration("contoso"), new FakePlatform(), diagnostics, out _, out _, repository);
            Assert.Equal(0, await app.InvokeAsync(["pr", "42"]));
        });

        Assert.EndsWith(repository + Environment.NewLine, stdout.ToString(), StringComparison.Ordinal);
        var summary = diagnostics.ToString();
        Assert.Contains("Author: Ada Lovelace", summary);
        Assert.Contains("Target branch: main", summary);
        Assert.Contains("Changed files (1):", summary);
    }

    [Fact]
    public async Task Pr_ado_failure_emits_no_json_and_does_not_touch_git() {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        await WithConsoleOutAsync(stdout, async () => {
            var app = CreatePrApplication(
                new UserConfiguration("contoso"), new FakePlatform(), stderr, out var ado, out var git,
                adoResult: AdoPullRequestLookupResult.Failed("ado-auth-failed", "Azure DevOps rejected the access token."));
            Assert.Equal(1, await app.InvokeAsync(["pr", "42", "--output", "json"]));
            Assert.Single(ado.Requests);
            Assert.Empty(git.Calls);
        });

        Assert.Empty(stdout.ToString());
        Assert.Contains("Azure DevOps rejected the access token.", stderr.ToString());
    }

    [Fact]
    public async Task Pr_rejects_visual_studio_on_non_windows_before_ado_or_git() {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        await WithConsoleOutAsync(stdout, async () => {
            var app = CreatePrApplication(new UserConfiguration("contoso"), new FakePlatform { Windows = false }, stderr, out var ado, out var git);
            Assert.Equal(1, await app.InvokeAsync(["pr", "42", "--ide", "vs", "--output", "json"]));
            Assert.Empty(ado.Requests);
            Assert.Empty(git.Calls);
        });

        Assert.Empty(stdout.ToString());
        Assert.Contains("only supported on Windows", stderr.ToString());
    }

    [Fact]
    public async Task Pr_warns_but_succeeds_when_default_code_launch_fails() {
        var stdout = new StringWriter();
        var diagnostics = new StringWriter();
        await WithConsoleOutAsync(stdout, async () => {
            var app = CreatePrApplication(
                new UserConfiguration("contoso"), new FakePlatform(), diagnostics, out _, out _, ideResult: IdeLaunchResult.Warn("code is missing"));
            Assert.Equal(0, await app.InvokeAsync(["pr", "42"]));
        });

        Assert.Single(stdout.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("could not open VS Code: code is missing. The repository is ready.", diagnostics.ToString());
    }

    [Fact]
    public async Task Pr_on_windows_falls_back_to_code_when_visual_studio_solution_is_missing() {
        var scratch = CreateScratchDirectory();
        var stdout = new StringWriter();
        var diagnostics = new StringWriter();
        try {
            await WithConsoleOutAsync(stdout, async () => {
                var app = CreatePrApplication(
                    new UserConfiguration("contoso"), new FakePlatform { Windows = true }, diagnostics, out _, out _, scratch);
                Assert.Equal(0, await app.InvokeAsync(["pr", "42", "--ide", "vs"]));
            });

            Assert.Contains("falling back to VS Code", diagnostics.ToString());
        }
        finally { DeleteScratchDirectory(scratch); }
    }

    private static CommandApplication CreatePrApplication(
        UserConfiguration configuration,
        IPlatform platform,
        StringWriter diagnostics,
        out FakeAdoClient ado,
        out FakeGitClient git,
        string? repository = null,
        AdoPullRequestLookupResult? adoResult = null,
        IdeLaunchResult? ideResult = null) {
        repository ??= Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "repo"));
        git = new FakeGitClient();
        git.Roots[repository] = GitCommandResult<string>.Succeeded(repository);
        git.Remotes[repository] = GitCommandResult<string>.Succeeded("git@ssh.dev.azure.com:v3/contoso/project/repo");
        ado = new FakeAdoClient(adoResult ?? AdoPullRequestLookupResult.Succeeded(PullRequest(repository)));
        var preparer = new PullRequestRepositoryPreparer(git, new FakeFileSystem(repository));
        var launcher = new FakeIdeLauncher(ideResult ?? IdeLaunchResult.Success());
        var service = new PullRequestWorkspaceService(ado, preparer, launcher);
        return new CommandApplication([
            new PullRequestCommandModule(ConsoleFor(diagnostics), ConsoleFor(diagnostics),
                new MemoryConfigurationStore(configuration), platform, service, launcher)
        ]);
    }

    private static AdoPullRequestDetails PullRequest(string repository) => new(
        42, "Add safe tests", "Ada Lovelace", "main", "refs/heads/main", "feature/one", "refs/heads/feature/one",
        "commit", "repo", "repo-id", "project", "project-id", "https://dev.azure.com/contoso/project/_git/repo",
        "https://dev.azure.com/contoso/project/_git/repo", ["src/changed.cs"]);

    private static IAnsiConsole ConsoleFor(StringWriter writer) =>
        AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer) });

    private static async Task WithConsoleOutAsync(StringWriter writer, Func<Task> action) {
        var original = Console.Out;
        Console.SetOut(writer);
        try { await action(); }
        finally { Console.SetOut(original); }
    }

    private static string CreateScratchDirectory() {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "test-scratch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteScratchDirectory(string path) {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
