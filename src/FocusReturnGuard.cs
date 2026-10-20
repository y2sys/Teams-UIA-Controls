using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

public interface IMeetingFocusApi {
    IntPtr Foreground();
    uint Owner(IntPtr window);
    bool Usable(IntPtr window);
    bool LastInput(out uint stamp);
    bool Activate(IntPtr window);
}

// One short-lived guard per mic/camera operation. Never used for sharing/leave.
public sealed class FocusReturnGuard {
    readonly IMeetingFocusApi api;
    readonly Func<long> clock;
    readonly IntPtr original;
    readonly uint owner, teams, input;
    readonly long started;
    bool finished, attempted;
    public string Outcome { get; private set; }

    FocusReturnGuard(IMeetingFocusApi api, uint teams, Func<long> clock,
        IntPtr original, uint owner, uint input) {
        this.api = api; this.teams = teams; this.clock = clock;
        this.original = original; this.owner = owner; this.input = input;
        started = clock(); Outcome = "Watching";
    }
    public static FocusReturnGuard? Capture(uint teams) {
        return Capture(new NativeMeetingFocusApi(), teams,
            () => (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency));
    }
    public static FocusReturnGuard? Capture(IMeetingFocusApi api, uint teams, Func<long> clock) {
        var original = api.Foreground();
        uint owner = api.Owner(original), stamp;
        if (original == IntPtr.Zero || owner == 0 || owner == teams ||
            !api.Usable(original) || !api.LastInput(out stamp)) return null;
        return new FocusReturnGuard(api, teams, clock, original, owner, stamp);
    }
    // Returns true only while waiting for a possible delayed Teams activation.
    public bool TryRestore() {
        if (finished) return false;
        uint stamp;
        if (clock() - started > 700) return Finish("Expired");
        if (!api.LastInput(out stamp) || stamp != input) return Finish("UserInput");
        if (!api.Usable(original) || api.Owner(original) != owner) return Finish("OriginalUnavailable");
        var current = api.Foreground();
        if (current == original) return attempted ? Finish("Restored") : true;
        if (api.Owner(current) != teams) return Finish("OtherWindow");
        attempted = true;
        api.Activate(original);
        if (api.Foreground() == original) return Finish("Restored");
        Outcome = "ActivationRestricted";
        return true;
    }
    bool Finish(string outcome) { Outcome = outcome; finished = true; return false; }
}

public sealed class NativeMeetingFocusApi : IMeetingFocusApi {
    public NativeMeetingFocusApi() { MeetingPhysicalInput.Start(); }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    public IntPtr Foreground() { return GetForegroundWindow(); }
    public uint Owner(IntPtr window) {
        uint process;
        GetWindowThreadProcessId(window, out process);
        return process;
    }
    public bool Usable(IntPtr window) { return IsWindow(window) && IsWindowVisible(window) && !IsIconic(window); }
    public bool LastInput(out uint stamp) {
        stamp = MeetingPhysicalInput.Version;
        return MeetingPhysicalInput.Available;
    }
    public bool Activate(IntPtr window) { return SetForegroundWindow(window); }
}

// Observe only physical-input occurrence, never key values, text or positions.
// UIA providers can synthesize input, so GetLastInputInfo alone cannot tell
// whether the user intervened. The LL injected flags distinguish those events.
public static class MeetingPhysicalInput {
    delegate IntPtr Hook(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] struct Message {
        public IntPtr hwnd; public uint message; public UIntPtr wParam;
        public IntPtr lParam; public uint time; public int x, y; public uint reserved;
    }
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int kind, Hook callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] static extern int GetMessage(out Message message, IntPtr hwnd, uint min, uint max);
    [DllImport("kernel32.dll", CharSet=CharSet.Auto)] static extern IntPtr GetModuleHandle(string? name);
    static readonly object Sync = new object();
    static readonly ManualResetEvent Ready = new ManualResetEvent(false);
    static readonly Hook Keyboard = (code, message, data) => Observe(code, message, data, 8, 0x10);
    static readonly Hook Mouse = (code, message, data) => Observe(code, message, data, 12, 1);
    static bool started;
    static int version, injected;
    public static bool Available { get; private set; }
    public static uint Version { get { return unchecked((uint)Volatile.Read(ref version)); } }
    public static int InjectedCount { get { return Volatile.Read(ref injected); } }
    static IntPtr Observe(int code, IntPtr message, IntPtr data, int offset, int flag) {
        if (code >= 0) {
            if ((Marshal.ReadInt32(data, offset) & flag) == 0) Interlocked.Increment(ref version);
            else Interlocked.Increment(ref injected);
        }
        return CallNextHookEx(IntPtr.Zero, code, message, data);
    }
    public static void Start() {
        lock (Sync) {
            if (!started) {
                started = true;
                var thread = new Thread(() => {
                    var keyboard = SetWindowsHookEx(13, Keyboard, GetModuleHandle(null), 0);
                    var mouse = SetWindowsHookEx(14, Mouse, GetModuleHandle(null), 0);
                    Available = keyboard != IntPtr.Zero && mouse != IntPtr.Zero;
                    Ready.Set();
                    try { if (Available) { Message message; while (GetMessage(out message, IntPtr.Zero, 0, 0) > 0) {} } }
                    finally {
                        Available = false;
                        if (keyboard != IntPtr.Zero) UnhookWindowsHookEx(keyboard);
                        if (mouse != IntPtr.Zero) UnhookWindowsHookEx(mouse);
                    }
                });
                thread.IsBackground = true;
                thread.Start();
            }
        }
        Ready.WaitOne(1000);
    }
}
