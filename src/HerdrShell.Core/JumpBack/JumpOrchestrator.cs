using HerdrShell.Core.Client;
using HerdrShell.Core.Protocol;

namespace HerdrShell.Core.JumpBack;

/// <summary>
/// Two-layer jump: (1) agent.focus activates the session inside herdr —
/// deterministic on every terminal; (2) the hosting terminal is raised by a
/// per-terminal route. The Windows Terminal route stamps a unique nonce on
/// the hosting tab via client.window_title.set, locates the TabItem through
/// UIA, and always clears the stamp afterwards, even when activation throws.
/// </summary>
public sealed class JumpOrchestrator
{
    private readonly IHerdrControl _herdr;
    private readonly ITerminalWindowActivator _windows;
    private readonly ITerminalTabActivator _tabs;
    private readonly IWezTermCli _wezterm;
    private readonly Func<string> _nonceFactory;

    public JumpOrchestrator(
        IHerdrControl herdr,
        ITerminalWindowActivator windows,
        ITerminalTabActivator tabs,
        IWezTermCli wezterm,
        Func<string>? nonceFactory = null)
    {
        _herdr = herdr;
        _windows = windows;
        _tabs = tabs;
        _wezterm = wezterm;
        _nonceFactory = nonceFactory ?? (() => $"herdr-shell-jump-{Guid.NewGuid():N}");
    }

    public async Task<JumpResult> JumpAsync(
        string agentTarget, TerminalHostConfig host, CancellationToken ct = default)
    {
        try
        {
            await _herdr.FocusAgentAsync(agentTarget, ct).ConfigureAwait(false);
        }
        catch (HerdrApiException ex)
        {
            return new JumpResult(JumpOutcome.NotFound, $"agent.focus rejected: {ex.Message}");
        }

        return host.Kind switch
        {
            TerminalHostKind.WezTerm => await JumpWezTermAsync(host, ct).ConfigureAwait(false),
            TerminalHostKind.WindowsTerminal =>
                await JumpWindowsTerminalAsync(host, ct).ConfigureAwait(false),
            _ => JumpGenericWindow(host),
        };
    }

    private async Task<JumpResult> JumpWezTermAsync(
        TerminalHostConfig host, CancellationToken ct)
    {
        var paneActivated = host.WezTermPaneId is not null &&
            await _wezterm.TryActivatePaneAsync(host.WezTermPaneId, ct).ConfigureAwait(false);
        var windowRaised = _windows.ActivateWindow(host.ProcessName, host.WindowTitleContains);
        if (!windowRaised)
            return new JumpResult(JumpOutcome.HerdrFocusedOnly, "wezterm window not raised");
        return paneActivated
            ? new JumpResult(JumpOutcome.Activated, "wezterm-pane")
            : new JumpResult(JumpOutcome.ActivatedWindowOnly, "wezterm pane id unavailable");
    }

    private async Task<JumpResult> JumpWindowsTerminalAsync(
        TerminalHostConfig host, CancellationToken ct)
    {
        var nonce = _nonceFactory();
        ClientWindowTitleResult stamped;
        try
        {
            stamped = await _herdr.SetClientWindowTitleAsync(nonce, ct).ConfigureAwait(false);
        }
        catch (HerdrApiException ex)
        {
            return FallbackToWindow(host, $"title stamp rejected: {ex.Message}");
        }

        if (stamped.Reason == ClientWindowTitleReason.NoForegroundClient)
            return FallbackToWindow(host, "no foreground herdr client for title stamp");

        try
        {
            if (_tabs.TryActivateTabByTitle(host.ProcessName, nonce))
            {
                // After tab selection the stamped nonce is the window title,
                // which pins the foreground call to the right window.
                return _windows.ActivateWindow(host.ProcessName, nonce)
                    ? new JumpResult(JumpOutcome.Activated, "wt-tab-uia")
                    : new JumpResult(JumpOutcome.ActivatedWindowOnly, "tab selected, window not raised");
            }
            return FallbackToWindow(host, "tab marker not found via UIA");
        }
        catch (Exception ex)
        {
            // UIA failures are environmental (tree churn, permissions);
            // degrade to window-level activation instead of failing the jump.
            return FallbackToWindow(host, $"tab activation failed: {ex.Message}");
        }
        finally
        {
            await ClearStampAsync(ct).ConfigureAwait(false);
        }
    }

    private JumpResult JumpGenericWindow(TerminalHostConfig host) =>
        _windows.ActivateWindow(host.ProcessName, host.WindowTitleContains)
            ? new JumpResult(JumpOutcome.ActivatedWindowOnly, "window-level")
            : new JumpResult(JumpOutcome.HerdrFocusedOnly, "window not found");

    private JumpResult FallbackToWindow(TerminalHostConfig host, string reason) =>
        _windows.ActivateWindow(host.ProcessName, host.WindowTitleContains)
            ? new JumpResult(JumpOutcome.ActivatedWindowOnly, reason)
            : new JumpResult(JumpOutcome.HerdrFocusedOnly, reason);

    private async Task ClearStampAsync(CancellationToken ct)
    {
        try
        {
            await _herdr.ClearClientWindowTitleAsync(ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The stamp is display-only; a failed clear must not mask the jump outcome.
        }
    }
}
