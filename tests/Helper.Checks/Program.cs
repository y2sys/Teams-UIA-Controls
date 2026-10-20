using MeetingControls.Helper;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}

// Physical-input cancellation and exact foreground ownership protect user work.
var api = new FakeFocusApi();
long now = 0;
var guard = FocusReturnGuard.Capture(api, 200, () => now)!;
Check(guard.TryRestore() && api.Activations == 0, "Do not activate an unchanged foreground");
api.Current = new IntPtr(2);
Check(!guard.TryRestore() && guard.Outcome == "Restored" && api.Current.ToInt32() == 1, "Restore the prior app after Teams activation");
guard.TryRestore(); Check(api.Activations == 1, "Do not repeatedly restore");
api = new(); guard = FocusReturnGuard.Capture(api, 200, () => now)!;
api.Current = new IntPtr(3); guard.TryRestore(); Check(api.Activations == 0, "Do not steal focus from a third app");
api = new(); guard = FocusReturnGuard.Capture(api, 200, () => now)!;
api.Current = new IntPtr(2); api.Input++; guard.TryRestore(); Check(api.Activations == 0, "User input cancels restoration");
api = new(); guard = FocusReturnGuard.Capture(api, 200, () => now)!;
api.Current = new IntPtr(2); api.Available = false; guard.TryRestore(); Check(api.Activations == 0, "Unavailable original window must be left alone");
api = new(); guard = FocusReturnGuard.Capture(api, 200, () => now)!;
api.Current = new IntPtr(2); api.Reused = true; guard.TryRestore(); Check(api.Activations == 0, "Reused handles must be left alone");
api = new(); guard = FocusReturnGuard.Capture(api, 200, () => now)!;
api.Current = new IntPtr(2); now = 701; guard.TryRestore(); Check(api.Activations == 0, "Expired restoration must not change focus");
api = new() { Current = new IntPtr(2) };
Check(FocusReturnGuard.Capture(api, 200, () => now) is null, "Active Teams needs no restoration");

// Notifications require the exact requested call's observed state transition.
var tracker = new MeetingTransitionNotices();
Check(tracker.Observe("a", "microphone", true, 0) is null, "No OSD for initial state");
tracker.Begin("a", "microphone", true, 100);
Check(tracker.Observe("a", "microphone", true, 120) is null, "No OSD for unchanged state");
Check(tracker.Observe("a", "microphone", null, 140) is null, "No OSD for unknown state");
Check(tracker.Observe("b", "microphone", false, 150) is null, "No OSD for a different session");
Check(tracker.Observe("a", "microphone", false, 160) is { State: false }, "Confirmed unmute creates notice");
Check(tracker.Observe("a", "microphone", false, 180) is null, "Notice only once");
tracker.Begin("a", "camera", false, 200);
Check(tracker.Observe("a", "camera", true, 3201) is null, "Late changes cannot confirm old commands");
tracker.Begin("a", "share", false, 3300);
Check(tracker.Observe("a", "share", true, 3400) is null, "Share has no mic/camera OSD");

Check(TeamsController.ReadLabel(" マイクのミュートを解除 ", ["マイクのミュートを解除"], ["マイクのミュート"]) == true, "Japanese mute label");
Check(TeamsController.ReadLabel("new label", ["Unmute"], ["Mute"]) is null, "Unrecognized labels remain unknown");
var full = new MeetingSession { Id = "a", Title = "Meeting A | Microsoft Teams", Muted = true, CameraOn = false, Sharing = false };
var compact = new MeetingSession { Id = "b", Title = "会議のコンパクト ビュー | Meeting A | Microsoft Teams" };
var other = new MeetingSession { Id = "c", Title = "Meeting C | Microsoft Teams" };
var deduped = TeamsController.Deduplicate([full, compact, other]);
Check(deduped.Count == 2 && deduped[0] == full && deduped[1] == other, "Compact duplicates cannot appear as simultaneous calls");
var transition = TeamsController.TransitionFrom(full, full, true);
Check(transition.InMeeting && transition.Picker && transition.LastKnown == full && transition.Capabilities.Values.All(value => !value), "Picker preserves display but disables commands");
Console.WriteLine($"C# helper: {checks} behavior checks passed");

if (args is ["--osd-preview", var image])
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(image))!);
    MeetingOsdChecks.Run(Path.GetFullPath(image));
    Console.WriteLine("OSD centered, non-activating, click-through and auto-dismiss checks passed");
}

sealed class FakeFocusApi : IMeetingFocusApi
{
    public IntPtr Current = new(1);
    public uint Input = 10;
    public bool Available = true, Reused;
    public int Activations;
    public IntPtr Foreground() => Current;
    public uint Owner(IntPtr window) => window.ToInt32() == 2 ? 200u : window.ToInt32() == 3 ? 300u : Reused ? 999u : 100u;
    public bool Usable(IntPtr window) => Available;
    public bool LastInput(out uint stamp) { stamp = Input; return true; }
    public bool Activate(IntPtr window) { Activations++; Current = window; return true; }
}
