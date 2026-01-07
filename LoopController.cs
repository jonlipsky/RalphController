using RalphController.Models;

namespace RalphController;

/// <summary>
/// Controls the Ralph loop - manages state, iterations, and lifecycle
/// </summary>
public class LoopController : IDisposable
{
    private readonly RalphConfig _config;
    private readonly LoopStatistics _statistics;
    private AIProcess? _currentProcess;
    private CancellationTokenSource? _loopCts;
    private TaskCompletionSource? _pauseTcs;
    private string? _injectedPrompt;
    private readonly object _stateLock = new();
    private bool _disposed;

    /// <summary>Current state of the loop</summary>
    public LoopState State { get; private set; } = LoopState.Idle;

    /// <summary>Statistics for the current run</summary>
    public LoopStatistics Statistics => _statistics;

    /// <summary>Configuration for this controller</summary>
    public RalphConfig Config => _config;

    /// <summary>Fired when an iteration starts</summary>
    public event Action<int>? OnIterationStart;

    /// <summary>Fired when an iteration completes</summary>
    public event Action<int, AIResult>? OnIterationComplete;

    /// <summary>Fired when output is received from AI</summary>
    public event Action<string>? OnOutput;

    /// <summary>Fired when error output is received from AI</summary>
    public event Action<string>? OnError;

    /// <summary>Fired when the loop state changes</summary>
    public event Action<LoopState>? OnStateChanged;

    /// <summary>Fired when the loop completes (finished or stopped)</summary>
    public event Action<bool>? OnLoopComplete;

    public LoopController(RalphConfig config)
    {
        _config = config;
        _statistics = new LoopStatistics { CostPerHour = config.CostPerHour };
    }

    /// <summary>
    /// Start the Ralph loop
    /// </summary>
    public async Task StartAsync(CancellationToken externalCancellation = default)
    {
        lock (_stateLock)
        {
            if (State != LoopState.Idle)
            {
                throw new InvalidOperationException($"Cannot start loop from state: {State}");
            }
            SetState(LoopState.Running);
        }

        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellation);
        _statistics.Reset();

        try
        {
            await RunLoopAsync(_loopCts.Token);
            OnLoopComplete?.Invoke(true);
        }
        catch (OperationCanceledException)
        {
            OnLoopComplete?.Invoke(false);
        }
        finally
        {
            lock (_stateLock)
            {
                SetState(LoopState.Idle);
            }
        }
    }

    /// <summary>
    /// Pause the loop after the current iteration completes
    /// </summary>
    public void Pause()
    {
        lock (_stateLock)
        {
            if (State != LoopState.Running)
            {
                return;
            }
            SetState(LoopState.Paused);
            _pauseTcs = new TaskCompletionSource();
        }
    }

    /// <summary>
    /// Resume a paused loop
    /// </summary>
    public void Resume()
    {
        lock (_stateLock)
        {
            if (State != LoopState.Paused)
            {
                return;
            }
            SetState(LoopState.Running);
            _pauseTcs?.TrySetResult();
            _pauseTcs = null;
        }
    }

    /// <summary>
    /// Stop the loop after the current iteration completes
    /// </summary>
    public void Stop()
    {
        lock (_stateLock)
        {
            if (State == LoopState.Idle || State == LoopState.Stopping)
            {
                return;
            }

            SetState(LoopState.Stopping);

            // If paused, release the pause wait
            _pauseTcs?.TrySetResult();
            _pauseTcs = null;
        }
    }

    /// <summary>
    /// Force stop immediately (kills current process)
    /// </summary>
    public async Task ForceStopAsync()
    {
        Stop();

        if (_currentProcess is not null)
        {
            await _currentProcess.StopAsync(TimeSpan.FromSeconds(2));
        }

        _loopCts?.Cancel();
    }

    /// <summary>
    /// Inject a one-time prompt to use for the next iteration
    /// </summary>
    public void InjectPrompt(string prompt)
    {
        _injectedPrompt = prompt;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Check if we should stop
            if (State == LoopState.Stopping)
            {
                break;
            }

            // Check for max iterations
            if (_config.MaxIterations.HasValue && _statistics.CurrentIteration >= _config.MaxIterations.Value)
            {
                break;
            }

            // Handle pause
            if (State == LoopState.Paused)
            {
                var pauseTcs = _pauseTcs;
                if (pauseTcs is not null)
                {
                    await pauseTcs.Task;
                }
                continue;
            }

            // Run an iteration
            await RunIterationAsync(cancellationToken);

            // Delay between iterations
            if (_config.IterationDelayMs > 0 && State == LoopState.Running)
            {
                await Task.Delay(_config.IterationDelayMs, cancellationToken);
            }
        }
    }

    private async Task RunIterationAsync(CancellationToken cancellationToken)
    {
        _statistics.StartIteration();
        OnIterationStart?.Invoke(_statistics.CurrentIteration);

        // Get prompt (injected or from file)
        string prompt;
        if (_injectedPrompt is not null)
        {
            prompt = _injectedPrompt;
            _injectedPrompt = null;
        }
        else
        {
            prompt = await GetPromptAsync();
        }

        // Create and run process
        _currentProcess = new AIProcess(_config);
        _currentProcess.OnOutput += line => OnOutput?.Invoke(line);
        _currentProcess.OnError += line => OnError?.Invoke(line);

        try
        {
            var result = await _currentProcess.RunAsync(prompt, cancellationToken);
            _statistics.CompleteIteration(result.Success);
            OnIterationComplete?.Invoke(_statistics.CurrentIteration, result);
        }
        catch (OperationCanceledException)
        {
            _statistics.CompleteIteration(false);
            throw;
        }
        finally
        {
            _currentProcess.Dispose();
            _currentProcess = null;
        }
    }

    private async Task<string> GetPromptAsync()
    {
        var promptPath = _config.PromptFilePath;
        if (!File.Exists(promptPath))
        {
            throw new FileNotFoundException($"Prompt file not found: {promptPath}");
        }

        return await File.ReadAllTextAsync(promptPath);
    }

    private void SetState(LoopState newState)
    {
        if (State != newState)
        {
            State = newState;
            OnStateChanged?.Invoke(newState);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _currentProcess?.Dispose();
        _pauseTcs?.TrySetCanceled();

        GC.SuppressFinalize(this);
    }
}
