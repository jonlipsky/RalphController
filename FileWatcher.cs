namespace RalphController;

/// <summary>
/// Watches project files for changes (hot-reload support)
/// </summary>
public class FileWatcher : IDisposable
{
    private readonly RalphConfig _config;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly Dictionary<string, DateTime> _lastChangeTime = new();
    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(500);
    private bool _disposed;

    /// <summary>Fired when prompt.md changes</summary>
    public event Action? OnPromptChanged;

    /// <summary>Fired when implementation_plan.md changes</summary>
    public event Action? OnPlanChanged;

    /// <summary>Fired when agents.md changes</summary>
    public event Action? OnAgentsChanged;

    /// <summary>Fired when any file in specs/ changes</summary>
    public event Action<string>? OnSpecChanged;

    /// <summary>Whether prompt.md has changed since last acknowledged</summary>
    public bool PromptChanged { get; private set; }

    /// <summary>Whether implementation_plan.md has changed since last acknowledged</summary>
    public bool PlanChanged { get; private set; }

    public FileWatcher(RalphConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Start watching files
    /// </summary>
    public void Start()
    {
        // Watch prompt.md
        if (File.Exists(_config.PromptFilePath))
        {
            WatchFile(_config.PromptFilePath, () =>
            {
                PromptChanged = true;
                OnPromptChanged?.Invoke();
            });
        }

        // Watch implementation_plan.md
        if (File.Exists(_config.PlanFilePath))
        {
            WatchFile(_config.PlanFilePath, () =>
            {
                PlanChanged = true;
                OnPlanChanged?.Invoke();
            });
        }

        // Watch agents.md
        if (File.Exists(_config.AgentsFilePath))
        {
            WatchFile(_config.AgentsFilePath, () =>
            {
                OnAgentsChanged?.Invoke();
            });
        }

        // Watch specs directory
        if (Directory.Exists(_config.SpecsDirectoryPath))
        {
            WatchDirectory(_config.SpecsDirectoryPath, "*.md", path =>
            {
                OnSpecChanged?.Invoke(path);
            });
        }
    }

    /// <summary>
    /// Stop watching files
    /// </summary>
    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
        }
    }

    /// <summary>
    /// Acknowledge prompt change (reset flag)
    /// </summary>
    public void AcknowledgePromptChange()
    {
        PromptChanged = false;
    }

    /// <summary>
    /// Acknowledge plan change (reset flag)
    /// </summary>
    public void AcknowledgePlanChange()
    {
        PlanChanged = false;
    }

    /// <summary>
    /// Read current content of implementation_plan.md
    /// </summary>
    public async Task<string?> ReadPlanAsync()
    {
        if (!File.Exists(_config.PlanFilePath))
            return null;

        return await File.ReadAllTextAsync(_config.PlanFilePath);
    }

    /// <summary>
    /// Read last N lines of implementation_plan.md
    /// </summary>
    public async Task<string[]> ReadPlanLinesAsync(int lastNLines = 10)
    {
        if (!File.Exists(_config.PlanFilePath))
            return Array.Empty<string>();

        var lines = await File.ReadAllLinesAsync(_config.PlanFilePath);
        return lines.TakeLast(lastNLines).ToArray();
    }

    private void WatchFile(string filePath, Action onChange)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        var fileName = Path.GetFileName(filePath);

        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };

        watcher.Changed += (_, e) => HandleChange(e.FullPath, onChange);
        watcher.EnableRaisingEvents = true;

        _watchers.Add(watcher);
    }

    private void WatchDirectory(string directoryPath, string filter, Action<string> onChange)
    {
        var watcher = new FileSystemWatcher(directoryPath, filter)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            IncludeSubdirectories = true
        };

        watcher.Changed += (_, e) => HandleChange(e.FullPath, () => onChange(e.FullPath));
        watcher.Created += (_, e) => HandleChange(e.FullPath, () => onChange(e.FullPath));
        watcher.EnableRaisingEvents = true;

        _watchers.Add(watcher);
    }

    private void HandleChange(string path, Action onChange)
    {
        // Debounce - ignore rapid consecutive changes
        lock (_lastChangeTime)
        {
            if (_lastChangeTime.TryGetValue(path, out var lastTime))
            {
                if (DateTime.UtcNow - lastTime < _debounceDelay)
                {
                    return;
                }
            }
            _lastChangeTime[path] = DateTime.UtcNow;
        }

        // Fire change handler on thread pool to avoid blocking
        Task.Run(onChange);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }
        _watchers.Clear();

        GC.SuppressFinalize(this);
    }
}
