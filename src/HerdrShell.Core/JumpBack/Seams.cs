namespace HerdrShell.Core.JumpBack;

/// <summary>
/// SetForegroundWindow route (agent-grid JumpBack staged resolution).
/// Must never activate an arbitrary fallback window: on ambiguity return false.
/// </summary>
public interface ITerminalWindowActivator
{
    bool ActivateWindow(string processName, string? titleContains);
}

/// <summary>
/// UIA route for Windows Terminal: find the TabItem whose name contains the
/// marker (background tabs are visible to UIA, unlike the HWND title) and
/// select it. Real implementation is Windows-only; Core sees only this seam.
/// </summary>
public interface ITerminalTabActivator
{
    bool TryActivateTabByTitle(string processName, string titleMarker);
}

/// <summary>wezterm CLI route: exact pane activation by WEZTERM_PANE id.</summary>
public interface IWezTermCli
{
    Task<bool> TryActivatePaneAsync(string paneId, CancellationToken ct = default);
}
