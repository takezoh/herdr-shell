namespace HerdrShell.Core.JumpBack;

public enum JumpOutcome
{
    /// <summary>herdr focused the agent and the exact terminal tab/pane is foreground.</summary>
    Activated,

    /// <summary>herdr focused the agent; only the terminal window (not the exact tab) was raised.</summary>
    ActivatedWindowOnly,

    /// <summary>herdr focused the agent but the terminal window could not be raised.</summary>
    HerdrFocusedOnly,

    /// <summary>The agent target was not found / focus was rejected. Nothing was activated.</summary>
    NotFound,
}

public sealed record JumpResult(JumpOutcome Outcome, string? Detail = null);

public enum TerminalHostKind
{
    /// <summary>Tab located by stamping a nonce title via client.window_title.set, then UIA.</summary>
    WindowsTerminal,

    /// <summary>Pane activated precisely through the wezterm CLI.</summary>
    WezTerm,

    /// <summary>Window-level activation only (dedicated-window setups).</summary>
    GenericWindow,
}

public sealed record TerminalHostConfig(
    TerminalHostKind Kind,
    string ProcessName,
    string? WezTermPaneId = null,
    string? WindowTitleContains = null);
