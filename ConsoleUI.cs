using System.Collections.Concurrent;
using RalphController.Models;
using Spectre.Console;

namespace RalphController;

/// <summary>
/// Rich console UI using Spectre.Console
/// </summary>
public class ConsoleUI : IDisposable
{
    private readonly LoopController _controller;
    private readonly FileWatcher _fileWatcher;
    private readonly RalphConfig _config;
    private readonly ConcurrentQueue<string> _outputLines = new();
    private readonly int _maxOutputLines = 20;
    private CancellationTokenSource? _uiCts;
    private Task? _inputTask;
    private bool _disposed;

    /// <summary>
    /// Whether to automatically start the loop when RunAsync is called
    /// </summary>
    public bool AutoStart { get; set; }

    public ConsoleUI(LoopController controller, FileWatcher fileWatcher, RalphConfig config)
    {
        _controller = controller;
        _fileWatcher = fileWatcher;
        _config = config;

        // Subscribe to controller events
        _controller.OnOutput += AddOutputLine;
        _controller.OnError += line => AddOutputLine($"[red]{Markup.Escape(line)}[/]");
        _controller.OnIterationStart += iter => AddOutputLine($"[blue]>>> Starting iteration {iter}[/]");
        _controller.OnIterationComplete += (iter, result) =>
        {
            var status = result.Success ? "[green]SUCCESS[/]" : "[red]FAILED[/]";
            AddOutputLine($"[blue]<<< Iteration {iter} complete: {status}[/]");
        };
    }

    /// <summary>
    /// Start the UI and run until stopped
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _uiCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start input handler on background thread
        _inputTask = Task.Run(() => HandleInputAsync(_uiCts.Token), _uiCts.Token);

        // Auto-start the loop if enabled
        if (AutoStart && _controller.State == LoopState.Idle)
        {
            _ = _controller.StartAsync();
            AddOutputLine("[green]>>> Auto-starting loop...[/]");
        }

        // Run the live display
        await AnsiConsole.Live(BuildLayout())
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                while (!_uiCts.Token.IsCancellationRequested)
                {
                    ctx.UpdateTarget(BuildLayout());
                    ctx.Refresh();
                    await Task.Delay(100, _uiCts.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                }
            });
    }

    /// <summary>
    /// Stop the UI
    /// </summary>
    public void Stop()
    {
        _uiCts?.Cancel();
    }

    private Layout BuildLayout()
    {
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Main").SplitRows(
                    new Layout("Output").Ratio(3),
                    new Layout("Plan").Size(8)
                ),
                new Layout("Footer").Size(3)
            );

        layout["Header"].Update(BuildHeaderPanel());
        layout["Output"].Update(BuildOutputPanel());
        layout["Plan"].Update(BuildPlanPanel());
        layout["Footer"].Update(BuildFooterPanel());

        return layout;
    }

    private Panel BuildHeaderPanel()
    {
        var state = _controller.State;
        var stats = _controller.Statistics;
        var provider = _config.Provider;

        var stateColor = state switch
        {
            LoopState.Running => "green",
            LoopState.Paused => "yellow",
            LoopState.Stopping => "red",
            _ => "grey"
        };

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn("")
            .AddColumn("")
            .AddColumn("")
            .AddColumn("");

        table.AddRow(
            $"[bold blue]RALPH CONTROLLER[/]",
            $"[{stateColor}][[{state.ToString().ToUpper()}]][/]",
            $"[white]Iteration #{stats.CurrentIteration}[/]",
            $"[dim]{provider}[/]"
        );

        table.AddRow(
            $"[dim]{Markup.Escape(_config.TargetDirectory)}[/]",
            $"[dim]Duration: {stats.FormatDuration(stats.TotalDuration)}[/]",
            $"[dim]Est: {stats.FormatCost(stats.EstimatedCost)}[/]",
            _fileWatcher.PromptChanged ? "[yellow]PROMPT CHANGED[/]" : ""
        );

        return new Panel(table)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue);
    }

    private Panel BuildOutputPanel()
    {
        var lines = _outputLines.ToArray();
        var content = lines.Length > 0
            ? string.Join("\n", lines)
            : "[dim]Waiting for output...[/]";

        return new Panel(new Markup(content))
            .Header("[bold]Output[/]")
            .Border(BoxBorder.Rounded)
            .Expand();
    }

    private Panel BuildPlanPanel()
    {
        var planLines = _fileWatcher.ReadPlanLinesAsync(6).GetAwaiter().GetResult();
        var content = planLines.Length > 0
            ? string.Join("\n", planLines.Select(Markup.Escape))
            : "[dim]No implementation plan found[/]";

        return new Panel(new Markup(content))
            .Header($"[bold]{_config.PlanFile}[/]")
            .Border(BoxBorder.Rounded);
    }

    private Panel BuildFooterPanel()
    {
        // Double brackets to escape them in Spectre.Console markup
        var controls = _controller.State switch
        {
            LoopState.Running => "[[P]]ause  [[S]]top  [[F]]orce Stop  [[I]]nject  [[Q]]uit",
            LoopState.Paused => "[[R]]esume  [[S]]top  [[I]]nject  [[Q]]uit",
            LoopState.Idle => "[[Enter]] Start  [[Q]]uit",
            LoopState.Stopping => "[[F]]orce Stop  [[Q]]uit",
            _ => "[[Q]]uit"
        };

        return new Panel(new Markup($"[bold]{controls}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey);
    }

    private async Task HandleInputAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                await HandleKeyAsync(key);
            }
            await Task.Delay(50, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        switch (char.ToLower(key.KeyChar))
        {
            case 'p':
                if (_controller.State == LoopState.Running)
                {
                    _controller.Pause();
                    AddOutputLine("[yellow]>>> Loop paused[/]");
                }
                break;

            case 'r':
                if (_controller.State == LoopState.Paused)
                {
                    _controller.Resume();
                    AddOutputLine("[green]>>> Loop resumed[/]");
                }
                break;

            case 's':
                if (_controller.State == LoopState.Running || _controller.State == LoopState.Paused)
                {
                    _controller.Stop();
                    AddOutputLine("[red]>>> Stopping after current iteration...[/]");
                }
                break;

            case 'f':
                if (_controller.State != LoopState.Idle)
                {
                    await _controller.ForceStopAsync();
                    AddOutputLine("[red]>>> Force stopped![/]");
                }
                break;

            case 'i':
                await HandleInjectAsync();
                break;

            case 'q':
                Stop();
                break;

            case '\r':
            case '\n':
                if (_controller.State == LoopState.Idle)
                {
                    // Start loop in background
                    _ = _controller.StartAsync();
                    AddOutputLine("[green]>>> Starting loop...[/]");
                }
                break;
        }
    }

    private async Task HandleInjectAsync()
    {
        // For now, use a simple text prompt
        // In a more advanced version, this could open an editor
        AnsiConsole.Clear();
        var prompt = AnsiConsole.Prompt(
            new TextPrompt<string>("[yellow]Enter prompt to inject (or empty to cancel):[/]")
                .AllowEmpty());

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            _controller.InjectPrompt(prompt);
            AddOutputLine($"[yellow]>>> Injected prompt: {Markup.Escape(prompt.Length > 50 ? prompt[..50] + "..." : prompt)}[/]");
        }
    }

    private void AddOutputLine(string line)
    {
        _outputLines.Enqueue(line);

        // Keep only last N lines
        while (_outputLines.Count > _maxOutputLines)
        {
            _outputLines.TryDequeue(out _);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _uiCts?.Cancel();
        _uiCts?.Dispose();

        GC.SuppressFinalize(this);
    }
}
