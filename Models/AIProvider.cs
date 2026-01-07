namespace RalphController.Models;

/// <summary>
/// Supported AI providers
/// </summary>
public enum AIProvider
{
    /// <summary>Anthropic Claude CLI</summary>
    Claude,

    /// <summary>OpenAI Codex CLI</summary>
    Codex
}

/// <summary>
/// Configuration for an AI provider
/// </summary>
public record AIProviderConfig
{
    public required AIProvider Provider { get; init; }
    public required string ExecutablePath { get; init; }
    public required string Arguments { get; init; }

    /// <summary>Whether this provider reads prompt from stdin (false = pass as argument)</summary>
    public bool UsesStdin { get; init; } = false;

    /// <summary>Argument prefix for prompt (if not using stdin)</summary>
    public string? PromptArgument { get; init; }

    /// <summary>Whether to write prompt to a temp file and reference it</summary>
    public bool UsesTempFile { get; init; } = false;

    public static AIProviderConfig ForClaude(string? executablePath = null) => new()
    {
        Provider = AIProvider.Claude,
        ExecutablePath = executablePath ?? "claude",
        // -p for print mode (non-interactive), --dangerously-skip-permissions for auto-approve
        Arguments = "-p --dangerously-skip-permissions",
        UsesStdin = false,  // Pass prompt as argument (more reliable)
        PromptArgument = null  // Claude takes prompt as positional argument after options
    };

    public static AIProviderConfig ForCodex(string? executablePath = null) => new()
    {
        Provider = AIProvider.Codex,
        ExecutablePath = executablePath ?? "codex",
        // --yolo for non-interactive auto-approve mode
        Arguments = "--yolo",
        UsesStdin = false,
        PromptArgument = null  // Codex takes prompt as positional argument
    };
}
