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
        // Use stdin for real-time streaming output
        Arguments = "-p --dangerously-skip-permissions",
        UsesStdin = true
    };

    public static AIProviderConfig ForCodex(string? executablePath = null) => new()
    {
        Provider = AIProvider.Codex,
        ExecutablePath = executablePath ?? "codex",
        // exec for non-interactive mode, - to read prompt from stdin
        // --dangerously-bypass-approvals-and-sandbox for full auto mode
        Arguments = "exec --dangerously-bypass-approvals-and-sandbox -",
        UsesStdin = true  // Codex exec reads from stdin when using "-"
    };
}
