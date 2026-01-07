using System.Diagnostics;
using RalphController;
using RalphController.Models;
using Spectre.Console;

// Check for test modes
if (args.Contains("--single-run"))
{
    // Run one iteration without TUI to test the full loop
    AnsiConsole.MarkupLine("[yellow]Running single iteration (no TUI)...[/]\n");

    var singleDir = args.FirstOrDefault(a => !a.StartsWith("-")) ?? Directory.GetCurrentDirectory();
    if (!Directory.Exists(singleDir)) singleDir = Directory.GetCurrentDirectory();

    var singleConfig = new RalphConfig
    {
        TargetDirectory = singleDir,
        Provider = AIProvider.Claude,
        ProviderConfig = AIProviderConfig.ForClaude(),
        MaxIterations = 1
    };

    using var singleController = new LoopController(singleConfig);

    singleController.OnOutput += line =>
    {
        var escaped = line.Replace("[", "[[").Replace("]", "]]");
        AnsiConsole.MarkupLine($"[green]OUT:[/] {escaped}");
    };
    singleController.OnError += line =>
    {
        var escaped = line.Replace("[", "[[").Replace("]", "]]");
        AnsiConsole.MarkupLine($"[red]ERR:[/] {escaped}");
    };
    singleController.OnIterationStart += iter =>
    {
        AnsiConsole.MarkupLine($"[blue]>>> Starting iteration {iter}[/]");
    };
    singleController.OnIterationComplete += (iter, result) =>
    {
        AnsiConsole.MarkupLine($"[blue]<<< Iteration {iter} complete: {(result.Success ? "SUCCESS" : "FAILED")}[/]");
    };

    AnsiConsole.MarkupLine($"[dim]Directory: {singleDir}[/]");
    AnsiConsole.MarkupLine($"[dim]Prompt: {singleConfig.PromptFilePath}[/]\n");

    await singleController.StartAsync();

    AnsiConsole.MarkupLine("\n[green]Done![/]");
    return 0;
}

if (args.Contains("--test-aiprocess"))
{
    AnsiConsole.MarkupLine("[yellow]Testing AIProcess class...[/]\n");

    var testConfig = new RalphConfig
    {
        TargetDirectory = Directory.GetCurrentDirectory(),
        Provider = AIProvider.Claude,
        ProviderConfig = AIProviderConfig.ForClaude()
    };

    using var aiProcess = new AIProcess(testConfig);

    var outputReceived = false;
    aiProcess.OnOutput += line =>
    {
        outputReceived = true;
        var escaped = line.Replace("[", "[[").Replace("]", "]]");
        AnsiConsole.MarkupLine($"[green]OUTPUT:[/] {escaped}");
    };
    aiProcess.OnError += line =>
    {
        var escaped = line.Replace("[", "[[").Replace("]", "]]");
        AnsiConsole.MarkupLine($"[red]ERROR:[/] {escaped}");
    };
    aiProcess.OnExit += code =>
    {
        AnsiConsole.MarkupLine($"[dim]EXIT:[/] {code}");
    };

    AnsiConsole.MarkupLine("[blue]Running AIProcess.RunAsync('Say hello')...[/]\n");
    var result = await aiProcess.RunAsync("Say hello");

    AnsiConsole.MarkupLine($"\n[yellow]Result:[/]");
    AnsiConsole.MarkupLine($"  Success: {result.Success}");
    AnsiConsole.MarkupLine($"  Exit Code: {result.ExitCode}");
    AnsiConsole.MarkupLine($"  Output length: {result.Output.Length}");
    AnsiConsole.MarkupLine($"  Error length: {result.Error.Length}");
    AnsiConsole.MarkupLine($"  Output received via events: {outputReceived}");

    if (result.Output.Length > 0)
    {
        AnsiConsole.MarkupLine($"\n[yellow]Output content:[/]");
        var escaped = result.Output.Replace("[", "[[").Replace("]", "]]");
        AnsiConsole.MarkupLine(escaped);
    }

    return 0;
}

if (args.Contains("--test-output"))
{
    AnsiConsole.MarkupLine("[yellow]Testing process output capture...[/]\n");

    // Test 1: Simple echo
    AnsiConsole.MarkupLine("[blue]Test 1: Simple echo command[/]");
    var psi = new ProcessStartInfo
    {
        FileName = "/bin/bash",
        Arguments = "-c \"echo 'Hello from bash'\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = new Process { StartInfo = psi };
    process.OutputDataReceived += (_, e) =>
    {
        if (e.Data != null)
            AnsiConsole.MarkupLine($"  [green]STDOUT:[/] {e.Data}");
    };
    process.ErrorDataReceived += (_, e) =>
    {
        if (e.Data != null)
            AnsiConsole.MarkupLine($"  [red]STDERR:[/] {e.Data}");
    };
    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    await process.WaitForExitAsync();
    AnsiConsole.MarkupLine($"  [dim]Exit code: {process.ExitCode}[/]\n");

    // Test 2: Claude command
    AnsiConsole.MarkupLine("[blue]Test 2: Claude command with temp file[/]");
    var tempFile = Path.GetTempFileName();
    await File.WriteAllTextAsync(tempFile, "Say 'Hello, World!' and nothing else.");

    var psi2 = new ProcessStartInfo
    {
        FileName = "/bin/bash",
        Arguments = $"-c \"claude -p --dangerously-skip-permissions < '{tempFile}'\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    var outputLines = 0;
    var errorLines = 0;

    using var process2 = new Process { StartInfo = psi2 };
    process2.OutputDataReceived += (_, e) =>
    {
        if (e.Data != null)
        {
            outputLines++;
            var escaped = e.Data.Replace("[", "[[").Replace("]", "]]");
            AnsiConsole.MarkupLine($"  [green]STDOUT:[/] {escaped}");
        }
    };
    process2.ErrorDataReceived += (_, e) =>
    {
        if (e.Data != null)
        {
            errorLines++;
            var escaped = e.Data.Replace("[", "[[").Replace("]", "]]");
            AnsiConsole.MarkupLine($"  [red]STDERR:[/] {escaped}");
        }
    };
    process2.Start();
    process2.BeginOutputReadLine();
    process2.BeginErrorReadLine();
    await process2.WaitForExitAsync();
    await Task.Delay(100); // Allow async events to complete
    File.Delete(tempFile);

    AnsiConsole.MarkupLine($"  [dim]Exit code: {process2.ExitCode}, stdout lines: {outputLines}, stderr lines: {errorLines}[/]\n");

    AnsiConsole.MarkupLine("[green]Test complete![/]");
    return 0;
}

// Parse command line arguments
var targetDir = args.Length > 0 && !args[0].StartsWith("-") ? args[0] : null;
var provider = AIProvider.Claude;

// Check for provider flag
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--provider" && i + 1 < args.Length)
    {
        provider = args[i + 1].ToLower() switch
        {
            "codex" => AIProvider.Codex,
            "claude" => AIProvider.Claude,
            _ => AIProvider.Claude
        };
    }
    else if (args[i] == "--codex")
    {
        provider = AIProvider.Codex;
    }
}

// Show banner
AnsiConsole.Write(new FigletText("Ralph")
    .LeftJustified()
    .Color(Color.Blue));
AnsiConsole.MarkupLine("[dim]Autonomous AI Coding Loop Controller[/]\n");

// Prompt for target directory if not provided
if (string.IsNullOrEmpty(targetDir))
{
    targetDir = AnsiConsole.Prompt(
        new TextPrompt<string>("[yellow]Enter target directory:[/]")
            .DefaultValue(Directory.GetCurrentDirectory())
            .Validate(dir =>
            {
                if (!Directory.Exists(dir))
                {
                    return ValidationResult.Error("[red]Directory does not exist[/]");
                }
                return ValidationResult.Success();
            }));
}

// Validate directory
if (!Directory.Exists(targetDir))
{
    AnsiConsole.MarkupLine($"[red]Error: Directory does not exist: {targetDir}[/]");
    return 1;
}

targetDir = Path.GetFullPath(targetDir);
AnsiConsole.MarkupLine($"[green]Target directory:[/] {targetDir}");

// Prompt for provider if not specified via command line
if (args.All(a => a != "--provider" && a != "--codex"))
{
    provider = AnsiConsole.Prompt(
        new SelectionPrompt<AIProvider>()
            .Title("[yellow]Select AI provider:[/]")
            .AddChoices(AIProvider.Claude, AIProvider.Codex));
}

AnsiConsole.MarkupLine($"[green]Provider:[/] {provider}");

// Create configuration
var providerConfig = provider switch
{
    AIProvider.Codex => AIProviderConfig.ForCodex(),
    _ => AIProviderConfig.ForClaude()
};

var config = new RalphConfig
{
    TargetDirectory = targetDir,
    Provider = provider,
    ProviderConfig = providerConfig
};

// Check project structure
var scaffolder = new ProjectScaffolder(config);
var structure = scaffolder.ValidateProject();

if (!structure.IsComplete)
{
    AnsiConsole.MarkupLine("\n[yellow]Missing project files:[/]");
    foreach (var item in structure.MissingItems)
    {
        AnsiConsole.MarkupLine($"  [red]- {item}[/]");
    }

    var action = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("\n[yellow]What would you like to do?[/]")
            .AddChoices(
                "Generate files using AI",
                "Create default template files",
                "Continue anyway",
                "Exit"));

    switch (action)
    {
        case "Generate files using AI":
            // Ask for project description - we can't generate without context
            AnsiConsole.MarkupLine("\n[yellow]To generate project files, I need to understand your project.[/]");
            AnsiConsole.MarkupLine("[dim]You can provide a description, or path to a document (README, spec, etc.)[/]\n");

            var projectContext = AnsiConsole.Prompt(
                new TextPrompt<string>("[yellow]Describe your project (or enter path to a doc):[/]")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(projectContext))
            {
                AnsiConsole.MarkupLine("[red]No project description provided. Using default templates instead.[/]");
                await scaffolder.CreateDefaultFilesAsync();
                AnsiConsole.MarkupLine("[green]Created default template files[/]");
                break;
            }

            // Check if it's a file path
            var contextPath = Path.IsPathRooted(projectContext)
                ? projectContext
                : Path.Combine(targetDir, projectContext);

            if (File.Exists(contextPath))
            {
                AnsiConsole.MarkupLine($"[dim]Reading context from {contextPath}...[/]");
                projectContext = await File.ReadAllTextAsync(contextPath);
            }

            scaffolder.ProjectContext = projectContext;

            AnsiConsole.MarkupLine("\n[blue]Generating project files using AI...[/]");

            scaffolder.OnScaffoldStart += file =>
                AnsiConsole.MarkupLine($"[dim]Generating {file}...[/]");
            scaffolder.OnScaffoldComplete += (file, success) =>
            {
                if (success)
                    AnsiConsole.MarkupLine($"[green]Created {file}[/]");
                else
                    AnsiConsole.MarkupLine($"[red]Failed to create {file}[/]");
            };
            scaffolder.OnOutput += line =>
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(line)}[/]");

            await scaffolder.ScaffoldMissingAsync();
            break;

        case "Create default template files":
            await scaffolder.CreateDefaultFilesAsync();
            AnsiConsole.MarkupLine("[green]Created default template files[/]");
            break;

        case "Exit":
            return 0;
    }
}

// Re-validate
structure = scaffolder.ValidateProject();
if (!structure.HasPromptMd)
{
    AnsiConsole.MarkupLine("[red]Error: prompt.md is required to run the loop[/]");
    return 1;
}

// Create components
using var fileWatcher = new FileWatcher(config);
using var controller = new LoopController(config);
using var ui = new ConsoleUI(controller, fileWatcher, config);

// Auto-start if project structure is complete
ui.AutoStart = structure.IsComplete;

// Start file watcher
fileWatcher.Start();

// Handle Ctrl+C
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    ui.Stop();
};

// Run UI
AnsiConsole.Clear();
await ui.RunAsync();

// Cleanup
AnsiConsole.MarkupLine("\n[yellow]Goodbye![/]");
return 0;
