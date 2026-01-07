namespace RalphController.Models;

/// <summary>
/// Represents the current state of the Ralph loop
/// </summary>
public enum LoopState
{
    /// <summary>Loop is not running</summary>
    Idle,

    /// <summary>Loop is actively running iterations</summary>
    Running,

    /// <summary>Loop is paused between iterations</summary>
    Paused,

    /// <summary>Loop is stopping after current iteration</summary>
    Stopping
}

/// <summary>
/// Represents the status of required project files
/// </summary>
public record ProjectStructure
{
    public required string TargetDirectory { get; init; }
    public bool HasAgentsMd { get; init; }
    public bool HasSpecsDirectory { get; init; }
    public bool HasPromptMd { get; init; }
    public bool HasImplementationPlan { get; init; }

    public bool IsComplete => HasAgentsMd && HasSpecsDirectory && HasPromptMd && HasImplementationPlan;

    public List<string> MissingItems
    {
        get
        {
            var missing = new List<string>();
            if (!HasAgentsMd) missing.Add("agents.md");
            if (!HasSpecsDirectory) missing.Add("specs/");
            if (!HasPromptMd) missing.Add("prompt.md");
            if (!HasImplementationPlan) missing.Add("implementation_plan.md");
            return missing;
        }
    }
}
