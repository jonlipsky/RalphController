using RalphController;
using RalphController.Models;
using Spectre.Console;

// Parse command line arguments
var targetDir = args.Length > 0 ? args[0] : null;
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
