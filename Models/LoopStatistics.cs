namespace RalphController.Models;

/// <summary>
/// Tracks statistics for the Ralph loop execution
/// </summary>
public class LoopStatistics
{
    private readonly DateTime _startTime;
    private readonly object _lock = new();

    public LoopStatistics()
    {
        _startTime = DateTime.UtcNow;
    }

    /// <summary>Current iteration number (1-based)</summary>
    public int CurrentIteration { get; private set; }

    /// <summary>Total completed iterations</summary>
    public int CompletedIterations { get; private set; }

    /// <summary>Number of failed iterations</summary>
    public int FailedIterations { get; private set; }

    /// <summary>Total duration the loop has been running</summary>
    public TimeSpan TotalDuration => DateTime.UtcNow - _startTime;

    /// <summary>Cost per hour estimate (default $10.50 based on Sonnet)</summary>
    public double CostPerHour { get; set; } = 10.50;

    /// <summary>Estimated total cost based on duration</summary>
    public double EstimatedCost => TotalDuration.TotalHours * CostPerHour;

    /// <summary>Average iteration duration</summary>
    public TimeSpan AverageIterationDuration =>
        CompletedIterations > 0
            ? TimeSpan.FromTicks(TotalDuration.Ticks / CompletedIterations)
            : TimeSpan.Zero;

    /// <summary>When the current iteration started</summary>
    public DateTime? CurrentIterationStartTime { get; private set; }

    /// <summary>Duration of current iteration</summary>
    public TimeSpan CurrentIterationDuration =>
        CurrentIterationStartTime.HasValue
            ? DateTime.UtcNow - CurrentIterationStartTime.Value
            : TimeSpan.Zero;

    public void StartIteration()
    {
        lock (_lock)
        {
            CurrentIteration++;
            CurrentIterationStartTime = DateTime.UtcNow;
        }
    }

    public void CompleteIteration(bool success)
    {
        lock (_lock)
        {
            if (success)
                CompletedIterations++;
            else
                FailedIterations++;

            CurrentIterationStartTime = null;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            CurrentIteration = 0;
            CompletedIterations = 0;
            FailedIterations = 0;
            CurrentIterationStartTime = null;
        }
    }

    public string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        return $"{duration.Seconds}s";
    }

    public string FormatCost(double cost) => $"${cost:F2}";
}
