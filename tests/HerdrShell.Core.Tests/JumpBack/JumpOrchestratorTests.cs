using HerdrShell.Core.Client;
using HerdrShell.Core.JumpBack;
using HerdrShell.Core.Protocol;
using Xunit;

namespace HerdrShell.Core.Tests.JumpBack;

public sealed class JumpOrchestratorTests
{
    private sealed class FakeControl : IHerdrControl
    {
        public List<string> Calls { get; } = [];
        public bool FocusRejected { get; set; }
        public ClientWindowTitleReason SetReason { get; set; } = ClientWindowTitleReason.Set;
        public string? StampedTitle { get; private set; }

        public Task<HerdrPong> PingAsync(CancellationToken ct = default) =>
            Task.FromResult(new HerdrPong(20, "test"));

        public Task<IReadOnlyList<HerdrPaneAgent>> ListAgentsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<HerdrPaneAgent>>([]);

        public Task FocusAgentAsync(string target, CancellationToken ct = default)
        {
            Calls.Add($"focus:{target}");
            if (FocusRejected)
                throw new HerdrApiException("not_found", "no such agent");
            return Task.CompletedTask;
        }

        public Task<ClientWindowTitleResult> SetClientWindowTitleAsync(
            string title, CancellationToken ct = default)
        {
            Calls.Add("title.set");
            StampedTitle = title;
            return Task.FromResult(new ClientWindowTitleResult(
                SetReason == ClientWindowTitleReason.Set, SetReason));
        }

        public Task<ClientWindowTitleResult> ClearClientWindowTitleAsync(
            CancellationToken ct = default)
        {
            Calls.Add("title.clear");
            return Task.FromResult(new ClientWindowTitleResult(true, ClientWindowTitleReason.Cleared));
        }
    }

    private sealed class FakeWindows : ITerminalWindowActivator
    {
        public bool Result { get; set; } = true;
        public List<(string Process, string? Title)> Calls { get; } = [];

        public bool ActivateWindow(string processName, string? titleContains)
        {
            Calls.Add((processName, titleContains));
            return Result;
        }
    }

    private sealed class FakeTabs : ITerminalTabActivator
    {
        public bool Result { get; set; } = true;
        public Exception? Throws { get; set; }
        public List<(string Process, string Marker)> Calls { get; } = [];

        public bool TryActivateTabByTitle(string processName, string titleMarker)
        {
            Calls.Add((processName, titleMarker));
            if (Throws is not null)
                throw Throws;
            return Result;
        }
    }

    private sealed class FakeWezTerm : IWezTermCli
    {
        public bool Result { get; set; } = true;
        public List<string> ActivatedPanes { get; } = [];

        public Task<bool> TryActivatePaneAsync(string paneId, CancellationToken ct = default)
        {
            ActivatedPanes.Add(paneId);
            return Task.FromResult(Result);
        }
    }

    private static readonly TerminalHostConfig WindowsTerminalHost =
        new(TerminalHostKind.WindowsTerminal, "WindowsTerminal");

    private static readonly TerminalHostConfig WezTermHost =
        new(TerminalHostKind.WezTerm, "wezterm-gui", WezTermPaneId: "7");

    private static (JumpOrchestrator Jump, FakeControl Control, FakeWindows Windows,
        FakeTabs Tabs, FakeWezTerm Wez) Build()
    {
        var control = new FakeControl();
        var windows = new FakeWindows();
        var tabs = new FakeTabs();
        var wez = new FakeWezTerm();
        var jump = new JumpOrchestrator(control, windows, tabs, wez, () => "nonce-1");
        return (jump, control, windows, tabs, wez);
    }

    [Fact]
    public async Task FocusRejected_ReturnsNotFoundWithoutTouchingTerminal()
    {
        var (jump, control, windows, tabs, _) = Build();
        control.FocusRejected = true;

        var result = await jump.JumpAsync("ghost", WindowsTerminalHost);

        Assert.Equal(JumpOutcome.NotFound, result.Outcome);
        Assert.Empty(windows.Calls);
        Assert.Empty(tabs.Calls);
        Assert.DoesNotContain("title.set", control.Calls);
    }

    [Fact]
    public async Task WindowsTerminal_HappyPath_StampsSelectsRaisesAndClears()
    {
        var (jump, control, windows, tabs, _) = Build();

        var result = await jump.JumpAsync("p1", WindowsTerminalHost);

        Assert.Equal(JumpOutcome.Activated, result.Outcome);
        Assert.Equal("nonce-1", control.StampedTitle);
        Assert.Equal(("WindowsTerminal", "nonce-1"), Assert.Single(tabs.Calls));
        Assert.Equal(("WindowsTerminal", "nonce-1"), Assert.Single(windows.Calls));
        Assert.Equal(["focus:p1", "title.set", "title.clear"], control.Calls);
    }

    [Fact]
    public async Task WindowsTerminal_NoForegroundClient_FallsBackToWindowWithoutClear()
    {
        var (jump, control, windows, tabs, _) = Build();
        control.SetReason = ClientWindowTitleReason.NoForegroundClient;

        var result = await jump.JumpAsync("p1", WindowsTerminalHost);

        Assert.Equal(JumpOutcome.ActivatedWindowOnly, result.Outcome);
        Assert.Empty(tabs.Calls);
        Assert.Equal(("WindowsTerminal", null), Assert.Single(windows.Calls));
        // Nothing was stamped, so nothing must be cleared.
        Assert.DoesNotContain("title.clear", control.Calls);
    }

    [Fact]
    public async Task WindowsTerminal_TabMarkerMissing_FallsBackAndClears()
    {
        var (jump, control, windows, tabs, _) = Build();
        tabs.Result = false;

        var result = await jump.JumpAsync("p1", WindowsTerminalHost);

        Assert.Equal(JumpOutcome.ActivatedWindowOnly, result.Outcome);
        Assert.Single(tabs.Calls);
        Assert.Contains("title.clear", control.Calls);
    }

    [Fact]
    public async Task WindowsTerminal_UiaThrows_DegradesToWindowAndStillClears()
    {
        var (jump, control, windows, tabs, _) = Build();
        tabs.Throws = new InvalidOperationException("UIA tree churn");

        var result = await jump.JumpAsync("p1", WindowsTerminalHost);

        Assert.Equal(JumpOutcome.ActivatedWindowOnly, result.Outcome);
        Assert.Contains("UIA tree churn", result.Detail);
        Assert.Contains("title.clear", control.Calls);
    }

    [Fact]
    public async Task WindowsTerminal_EverythingUnavailable_ReportsHerdrFocusedOnly()
    {
        var (jump, control, windows, tabs, _) = Build();
        tabs.Result = false;
        windows.Result = false;

        var result = await jump.JumpAsync("p1", WindowsTerminalHost);

        Assert.Equal(JumpOutcome.HerdrFocusedOnly, result.Outcome);
        Assert.Contains("title.clear", control.Calls);
    }

    [Fact]
    public async Task WezTerm_ExactPaneActivation()
    {
        var (jump, _, windows, _, wez) = Build();

        var result = await jump.JumpAsync("p1", WezTermHost);

        Assert.Equal(JumpOutcome.Activated, result.Outcome);
        Assert.Equal(["7"], wez.ActivatedPanes);
        Assert.Equal(("wezterm-gui", null), Assert.Single(windows.Calls));
    }

    [Fact]
    public async Task WezTerm_MissingPaneId_IsWindowOnly()
    {
        var (jump, _, _, _, wez) = Build();

        var result = await jump.JumpAsync(
            "p1", WezTermHost with { WezTermPaneId = null });

        Assert.Equal(JumpOutcome.ActivatedWindowOnly, result.Outcome);
        Assert.Empty(wez.ActivatedPanes);
    }

    [Fact]
    public async Task WezTerm_WindowNotRaised_IsHerdrFocusedOnly()
    {
        var (jump, _, windows, _, _) = Build();
        windows.Result = false;

        var result = await jump.JumpAsync("p1", WezTermHost);

        Assert.Equal(JumpOutcome.HerdrFocusedOnly, result.Outcome);
    }

    [Fact]
    public async Task GenericWindow_UsesConfiguredTitleFilter()
    {
        var (jump, _, windows, tabs, _) = Build();
        var host = new TerminalHostConfig(
            TerminalHostKind.GenericWindow, "alacritty", WindowTitleContains: "herdr");

        var result = await jump.JumpAsync("p1", host);

        Assert.Equal(JumpOutcome.ActivatedWindowOnly, result.Outcome);
        Assert.Equal(("alacritty", "herdr"), Assert.Single(windows.Calls));
        Assert.Empty(tabs.Calls);
    }
}
