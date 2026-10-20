$ErrorActionPreference = 'Stop'
$guardSource = Get-Content (Join-Path $PSScriptRoot '..\src\FocusReturnGuard.cs') -Raw
Add-Type -TypeDefinition ($guardSource + @'
public sealed class FakeMeetingFocusApi : IMeetingFocusApi {
    public IntPtr Current = new IntPtr(1);
    public uint Input = 10;
    public bool Available = true, Reused, Allow = true;
    public int Activations;
    public IntPtr Foreground() { return Current; }
    public uint Owner(IntPtr w) { return w.ToInt32() == 2 ? 200u : w.ToInt32() == 3 ? 300u : Reused ? 999u : 100u; }
    public bool Usable(IntPtr w) { return Available; }
    public bool LastInput(out uint stamp) { stamp = Input; return true; }
    public bool Activate(IntPtr w) { Activations++; if (Allow) Current = w; return Allow; }
}
public static class MeetingFocusGuardTests {
    static long now;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Run() {
        var api = new FakeMeetingFocusApi(); now = 0;
        var guard = FocusReturnGuard.Capture(api, 200, () => now);
        Check(guard.TryRestore() && api.Activations == 0, "Untouched foreground must not be activated");
        api.Current = new IntPtr(2);
        Check(!guard.TryRestore() && guard.Outcome == "Restored" && api.Current.ToInt32() == 1, "Teams activation must restore original");
        guard.TryRestore(); Check(api.Activations == 1, "Restoration must happen once");
        api = new FakeMeetingFocusApi(); guard = FocusReturnGuard.Capture(api, 200, () => now);
        api.Current = new IntPtr(3); guard.TryRestore(); Check(api.Activations == 0, "Never steal focus from a third app");
        api = new FakeMeetingFocusApi(); guard = FocusReturnGuard.Capture(api, 200, () => now);
        api.Current = new IntPtr(2); api.Input++; guard.TryRestore(); Check(api.Activations == 0, "User input must cancel restoration");
        api = new FakeMeetingFocusApi(); guard = FocusReturnGuard.Capture(api, 200, () => now);
        api.Current = new IntPtr(2); api.Available = false; guard.TryRestore(); Check(api.Activations == 0, "Closed/minimized original must be left alone");
        api = new FakeMeetingFocusApi(); guard = FocusReturnGuard.Capture(api, 200, () => now);
        api.Current = new IntPtr(2); api.Reused = true; guard.TryRestore(); Check(api.Activations == 0, "Reused window handle must be left alone");
        api = new FakeMeetingFocusApi(); guard = FocusReturnGuard.Capture(api, 200, () => now);
        api.Current = new IntPtr(2); now = 701; guard.TryRestore(); Check(api.Activations == 0, "Expired guard must not steal focus");
        api = new FakeMeetingFocusApi(); api.Current = new IntPtr(2);
        Check(FocusReturnGuard.Capture(api, 200, () => now) == null, "Already-active Teams requires no guard");
    }
}
'@)
[MeetingFocusGuardTests]::Run()
Write-Output 'Focus guard behavior checks passed'
