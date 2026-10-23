using MeetingControls.Helper;

static class RdpChecks
{
    public static void Run(Action<bool, string> check)
    {
        var api = new FakeRdpApi();
        var teams = new IntPtr(2);
        var scope = RdpFocusScope.Begin(teams, 200, true, api, () => 0)!;
        check(scope is not null && api.Current == teams && !api.BarVisible, "Hidden bar is usable and hidden before the action");
        scope!.EnsureReadyToInvoke(); scope.MarkActionCompleted(); scope.Dispose();
        check(api.Current == new IntPtr(1) && api.Restores == 1, "Mic/camera return to original RDP input");
        scope.Dispose(); check(api.Restores == 1, "RDP cleanup is idempotent");

        foreach (var kind in new[] { "share", "leave" })
        {
            api = new(); scope = RdpFocusScope.Begin(teams, 200, false, api, () => 0)!;
            scope.MarkActionCompleted(); scope.Dispose();
            check(api.Restores == 0 && !api.BarVisible, kind + " keeps action-specific foreground behavior");
        }
        api = new() { IsRdp = false };
        check(RdpFocusScope.Begin(teams, 200, true, api) is null && api.Touches == 0, "Other apps bypass RDP preparation");
        api = new() { TargetValid = false };
        check(RdpFocusScope.Begin(teams, 200, true, api) is null && api.Touches == 0, "Unknown/reused RDP handles are untouched");
        api = new() { KeysUp = false };
        check(RdpFocusScope.Begin(teams, 200, true, api) is null && api.Touches == 0, "Held modifier keys do not attach input threads");
        api = new() { HooksAvailable = false };
        check(RdpFocusScope.Begin(teams, 200, true, api) is null && api.Touches == 0, "Unavailable input monitoring skips the workaround");
        api = new() { BarVisible = true };
        scope = RdpFocusScope.Begin(teams, 200, true, api, () => 0)!; scope.Dispose();
        check(api.VisibilityChanges == 0 && api.BarVisible, "Originally visible bars stay visible");
        api = new() { FailShow = true };
        check(RdpFocusScope.Begin(teams, 200, true, api, () => 0) is null && api.Current == new IntPtr(1), "Failed bar display safely falls back");
        api = new() { InputOnBar = true };
        bool canceled = false;
        try { RdpFocusScope.Begin(teams, 200, true, api, () => 0); }
        catch (OperationCanceledException) { canceled = true; }
        check(canceled && api.TeamsActivations == 0 && api.Restores == 0, "User input during preparation cancels the action");
        api = new() { ThirdAppOnTeams = true };
        canceled = false;
        try { RdpFocusScope.Begin(teams, 200, true, api, () => 0); }
        catch (OperationCanceledException) { canceled = true; }
        check(canceled && api.Current == new IntPtr(9) && api.Restores == 0 && !api.BarVisible, "Third-app switch is preserved and temporary bar cleaned");
        api = new(); scope = RdpFocusScope.Begin(teams, 200, true, api, () => 0)!;
        api.Input++; canceled = false;
        try { scope.EnsureReadyToInvoke(); } catch (OperationCanceledException) { canceled = true; }
        scope.Dispose(); check(canceled && api.Restores == 0, "Input before Invoke cancels without stealing focus");
        api = new(); long now = 0;
        scope = RdpFocusScope.Begin(teams, 200, true, api, () => now)!;
        now = 701; scope.Dispose();
        check(api.Restores == 0, "Expired focus restoration never activates an app");
        api = new(); now = 0;
        scope = RdpFocusScope.Begin(teams, 200, true, api, () => now)!;
        scope.MarkActionCompleted();
        check(scope.TryRestore() && api.Current == new IntPtr(1), "RDP restore watcher returns to RDP immediately");
        api.Current = teams; now = 100;
        check(scope.TryRestore() && api.Current == new IntPtr(1) && api.Restores == 2, "Delayed Teams activation is restored during the grace period");
        api.Input++; api.Current = teams;
        check(!scope.TryRestore(), "Physical input cancels delayed restoration");
        scope.Dispose(); check(api.Restores == 2, "Disposal does not override physical input");
        api = new(); scope = RdpFocusScope.Begin(teams, 200, false, api, () => 0)!;
        scope.Dispose(); check(api.Restores == 1, "An aborted share/leave restores the original app");
    }

    sealed class FakeRdpApi : IRdpFocusApi
    {
        public IntPtr Current = new(1);
        public uint Input = 10;
        public bool IsRdp = true, TargetValid = true, KeysUp = true, HooksAvailable = true;
        public bool BarVisible, FailShow, InputOnBar, ThirdAppOnTeams;
        public int Restores, Touches, VisibilityChanges, TeamsActivations;
        public RdpFocusTarget? Detect() => IsRdp ? new(new(1), new(10), new(3), new(4), 100, BarVisible) : null;
        public IntPtr Foreground => Current;
        public bool InputAvailable => HooksAvailable;
        public uint InputVersion => Input;
        public bool ModifiersUp => KeysUp;
        public bool Valid(RdpFocusTarget target) => TargetValid;
        public bool IsTeams(IntPtr window, uint process) => window == new IntPtr(2) && process == 200;
        public bool ShowBar(RdpFocusTarget target, bool visible)
        { Touches++; if (FailShow && visible) return false; VisibilityChanges++; BarVisible = visible; return true; }
        public bool FocusBar(RdpFocusTarget target) { Touches++; Current = target.Bar; if (InputOnBar) Input++; return true; }
        public void FocusTeams(IntPtr window) { Touches++; TeamsActivations++; Current = ThirdAppOnTeams ? new(9) : window; }
        public void Restore(RdpFocusTarget target) { Restores++; Current = target.Window; }
    }
}
