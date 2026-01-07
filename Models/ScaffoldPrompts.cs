namespace RalphController.Models;

/// <summary>
/// Contains prompt templates for scaffolding new Ralph projects.
/// These prompts follow the Ralph Wiggum principles - concise, focused, one task at a time.
/// </summary>
public static class ScaffoldPrompts
{
    /// <summary>
    /// Prompt to generate agents.md - the self-improvement file for the agent
    /// </summary>
    public const string AgentsMd = """
        Create an agents.md file for this project.

        This file is the agent's self-improvement notes. When the agent learns something new about:
        - How to build/run/test the project
        - Common errors and their solutions
        - Project-specific commands or patterns

        The agent should update this file to help future iterations.

        Requirements:
        1. Start with a brief project description placeholder
        2. Include sections for: Build Commands, Test Commands, Common Issues, Learnings
        3. Keep it brief - this file is read every loop iteration
        4. Use markdown format

        Write the file to agents.md
        """;

    /// <summary>
    /// Prompt to generate the specs directory structure
    /// </summary>
    public const string SpecsDirectory = """
        Create a specs/ directory with initial specification files.

        Specs are the source of truth for what needs to be built. Each spec file describes:
        - What the feature/component should do
        - Technical requirements
        - Acceptance criteria

        Create:
        1. specs/README.md - explaining how to write specs
        2. specs/example.md - a template spec file

        The specs should be detailed enough that an agent can implement from them,
        but concise enough to fit in context. Follow the principle: specs + stdlib = generate.
        """;

    /// <summary>
    /// Prompt to generate the initial prompt.md
    /// </summary>
    public const string PromptMd = """
        Create a prompt.md file - the main instruction file for the Ralph loop.

        This prompt is read every iteration. It should be CONCISE (under 200 words is ideal).
        Less is more - a simple prompt outperforms a complex one.

        The prompt should instruct the agent to:
        1. Study agents.md to learn project context
        2. Study specs/* for requirements
        3. Study implementation_plan.md for current progress
        4. Choose the most important incomplete task
        5. Implement ONE thing per iteration
        6. Run tests after changes
        7. Update implementation_plan.md with progress
        8. Commit on success

        Key Ralph principles to include:
        - "Choose the most important thing"
        - "Don't assume not implemented - search first"
        - "After implementing, run tests for that unit"
        - "Update the plan with learnings"

        Write the file to prompt.md
        """;

    /// <summary>
    /// Prompt to generate the implementation plan
    /// </summary>
    public const string ImplementationPlan = """
        Create an implementation_plan.md file - the TODO list for the project.

        This file tracks:
        - What has been completed [x]
        - What is in progress [ ] <- CURRENT
        - What is pending [ ]
        - Bugs discovered during implementation
        - Items that need investigation

        The agent updates this file every iteration to:
        - Mark completed items
        - Add new items discovered during work
        - Note blockers or issues

        Create an initial plan with:
        1. A "Completed" section (empty)
        2. A "In Progress" section (empty)
        3. A "Pending" section with placeholder tasks
        4. A "Bugs/Issues" section (empty)
        5. A "Notes" section for learnings

        Keep items brief - one line each. This file is the agent's memory across iterations.

        Write the file to implementation_plan.md
        """;
}
