using Mde.Tool.Commands;
using Mde.Tool.Configuration;
using Mde.Tool.Integrations;
using Mde.Tool.Platform;
using Spectre.Console;

var platform = new DefaultPlatform();
var configurationStore = new JsonUserConfigurationStore(platform);
var processLauncher = new PlatformProcessLauncher(platform);
var fileSystem = new DefaultFileSystem();
using var httpClient = new HttpClient();
var ideLauncher = new IdeLauncher(processLauncher, platform);
var workspaceService = new PullRequestWorkspaceService(
    new AdoPullRequestClient(httpClient, new AzureCliTokenProvider(processLauncher)),
    new PullRequestRepositoryPreparer(new GitClient(processLauncher), fileSystem),
    ideLauncher);
var stderrConsole = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) });
var modules = new ICommandModule[]
{
    new AboutCommandModule(AnsiConsole.Console, configurationStore),
    new InitCommandModule(AnsiConsole.Console, platform, configurationStore),
    new PullRequestCommandModule(AnsiConsole.Console, stderrConsole, configurationStore, platform, workspaceService, ideLauncher)
};

var application = new CommandApplication(modules);
return await application.InvokeAsync(args);
