using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace MeetingControls.Helper;

public sealed record RdpFocusTarget(IntPtr Window, IntPtr Input, IntPtr Bar, IntPtr Toolbar,
    uint ProcessId, bool BarVisible);

public interface IRdpFocusApi
{
    RdpFocusTarget? Detect();
    IntPtr Foreground { get; }
    bool InputAvailable { get; }
    uint InputVersion { get; }
    bool ModifiersUp { get; }
    bool Valid(RdpFocusTarget target);
    bool IsTeams(IntPtr window, uint process);
    bool ShowBar(RdpFocusTarget target, bool visible);
    bool FocusBar(RdpFocusTarget target);
    void FocusTeams(IntPtr window);
    void Restore(RdpFocusTarget target);
}

// Shared pre-operation scope for all four actions. It never sends keyboard input
// or changes RDP settings. Unknown clients/window structures use normal UIA.
public sealed class RdpFocusScope : IDisposable
{
    internal static void Trace(string message)
    {
        if (Environment.GetEnvironmentVariable("TEAMS_UIA_DIAGNOSTICS") == "1") Console.Error.WriteLine("rdp: " + message);
    }
    readonly IRdpFocusApi api;
    readonly RdpFocusTarget target;
    readonly IntPtr teamsWindow;
    readonly uint teamsProcess, input;
    readonly bool restoreAfterAction;
    readonly Func<long> clock;
    readonly long started;
    bool changedVisibility, actionCompleted, disposed;

    RdpFocusScope(IRdpFocusApi api, RdpFocusTarget target, IntPtr teamsWindow,
        uint teamsProcess, bool restoreAfterAction, Func<long> clock)
    {
        this.api = api; this.target = target; this.teamsWindow = teamsWindow;
        this.teamsProcess = teamsProcess; this.restoreAfterAction = restoreAfterAction;
        this.clock = clock; input = api.InputVersion; started = clock();
    }

    public static RdpFocusScope? Begin(IntPtr window, uint process, bool restoreAfterAction,
        IRdpFocusApi? api = null, Func<long>? clock = null)
    {
        api ??= new NativeRdpFocusApi();
        var target = api.Detect();
        Trace("targetDetected=" + (target is not null));
        if (target is null || !api.Valid(target) || !api.IsTeams(window, process) ||
            !api.InputAvailable || !api.ModifiersUp) return null;
        var scope = new RdpFocusScope(api, target, window, process, restoreAfterAction,
            clock ?? (() => TeamsController.Now));
        try
        {
            scope.CheckUser();
            if (!target.BarVisible)
            {
                scope.changedVisibility = true;
                if (!api.ShowBar(target, true)) { scope.CheckUser(); scope.Dispose(); return null; }
            }
            scope.CheckUser();
            if (!api.FocusBar(target)) { scope.CheckUser(); scope.Dispose(); return null; }
            scope.CheckUser();
            api.FocusTeams(window);
            scope.EnsureReadyToInvoke();
            Trace("prepared elapsedMs=" + (scope.clock() - scope.started));
            return scope;
        }
        catch (OperationCanceledException) { scope.Dispose(); throw; }
        catch
        {
            try { scope.CheckUser(); }
            finally { scope.Dispose(); }
            return null;
        }
        finally { scope.HideTemporaryBar(); }
    }

    bool InputUnchanged => api.InputAvailable && api.InputVersion == input;
    bool OwnsForeground => api.Foreground == target.Window || api.Foreground == target.Bar ||
        api.IsTeams(api.Foreground, teamsProcess);
    void CheckUser()
    {
        if (!InputUnchanged || !OwnsForeground || !api.Valid(target) || clock() - started > 700)
            throw new OperationCanceledException("Foreground operation interrupted");
    }
    public void EnsureReadyToInvoke()
    {
        CheckUser();
        if (api.Foreground != teamsWindow || !api.IsTeams(teamsWindow, teamsProcess))
            throw new OperationCanceledException("Teams activation was not confirmed");
    }
    public void MarkActionCompleted() => actionCompleted = true;
    public bool TryRestore()
    {
        if (disposed || !restoreAfterAction || !actionCompleted || !InputUnchanged || !api.ModifiersUp ||
            !api.Valid(target) || clock() - started > 700 || !OwnsForeground) return false;
        if (api.Foreground != target.Window) api.Restore(target);
        HideTemporaryBar();
        return true; // Observe briefly for a delayed UIA-provider activation.
    }
    void HideTemporaryBar()
    {
        if (!changedVisibility) return;
        try
        {
            // Do not hide the bar if the user has started interacting with it.
            if (api.Valid(target) && (InputUnchanged || api.Foreground != target.Bar))
                if (api.ShowBar(target, false)) changedVisibility = false;
        }
        catch { /* Cleanup must not mask the operation result. */ }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            if ((restoreAfterAction || !actionCompleted) && InputUnchanged && api.ModifiersUp &&
                api.Valid(target) && clock() - started <= 700 && OwnsForeground &&
                api.Foreground != target.Window) { api.Restore(target); Trace("restored=" + (api.Foreground == target.Window)); }
        }
        catch { /* Never retry against a changed window. */ }
        finally { HideTemporaryBar(); }
    }
}

public sealed class NativeRdpFocusApi : IRdpFocusApi
{
    delegate bool EnumCallback(IntPtr window, IntPtr value);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] struct GuiInfo
    {
        public uint Size, Flags;
        public IntPtr Active, Focus, Capture, Menu, MoveSize, Caret;
        public Rect CaretRect;
    }
    [StructLayout(LayoutKind.Sequential)] struct Message
    {
        public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam;
        public uint Time; public int X, Y; public uint Reserved;
    }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback, IntPtr value);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumCallback callback, IntPtr value);
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr SetActiveWindow(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr window);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint from, uint to, bool attach);
    [DllImport("user32.dll")] static extern bool PeekMessage(out Message message, IntPtr window, uint min, uint max, uint flags);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

    public IntPtr Foreground => GetForegroundWindow();
    public bool InputAvailable { get { MeetingPhysicalInput.Start(); return MeetingPhysicalInput.Available; } }
    public uint InputVersion => MeetingPhysicalInput.Version;
    public bool ModifiersUp => new[] { 0x10, 0x11, 0x12, 0x5b, 0x5c }.All(key => (GetAsyncKeyState(key) & 0x8000) == 0);
    static uint Owner(IntPtr window) { GetWindowThreadProcessId(window, out var process); return process; }
    static string Class(IntPtr window) { var name = new StringBuilder(256); GetClassName(window, name, name.Capacity); return name.ToString(); }
    static bool FullScreen(IntPtr window)
    {
        if (!IsWindowVisible(window) || IsIconic(window) || (GetWindowLongPtr(window, -16).ToInt64() & 0x00c00000) != 0 ||
            !GetWindowRect(window, out var rect)) return false;
        var monitor = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(MonitorFromWindow(window, 2), ref monitor) &&
            rect.Left <= monitor.Monitor.Left && rect.Top <= monitor.Monitor.Top &&
            rect.Right >= monitor.Monitor.Right && rect.Bottom >= monitor.Monitor.Bottom;
    }
    public RdpFocusTarget? Detect()
    {
        var window = Foreground;
        RdpFocusScope.Trace("foregroundClass=" + Class(window) + " fullScreen=" + FullScreen(window));
        if (Class(window) != "TscShellContainerClass" || !FullScreen(window)) return null;
        var owner = Owner(window);
        try
        {
            using var process = Process.GetProcessById((int)owner);
            var executable = process.MainModule?.FileName;
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.Equals(executable, Path.Combine(windows, "System32", "mstsc.exe"), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(executable, Path.Combine(windows, "SysWOW64", "mstsc.exe"), StringComparison.OrdinalIgnoreCase)) return null;
        }
        catch { return null; }
        IntPtr bar = IntPtr.Zero, toolbar = IntPtr.Zero;
        EnumWindows((item, _) =>
        {
            if (Owner(item) != owner || GetWindow(item, 4) != window || Class(item) != "BBarWindowClass") return true;
            if (bar != IntPtr.Zero) { bar = IntPtr.Zero; return false; }
            bar = item; return true;
        }, IntPtr.Zero);
        if (bar == IntPtr.Zero) return null;
        EnumChildWindows(bar, (item, _) =>
        {
            if (Owner(item) != owner || Class(item) != "ToolbarWindow32") return true;
            toolbar = item; return false;
        }, IntPtr.Zero);
        if (toolbar == IntPtr.Zero) return null;
        var info = new GuiInfo { Size = (uint)Marshal.SizeOf<GuiInfo>() };
        var thread = GetWindowThreadProcessId(window, out _);
        if (!GetGUIThreadInfo(thread, ref info) || info.Focus == IntPtr.Zero || Owner(info.Focus) != owner) return null;
        return new(window, info.Focus, bar, toolbar, owner, IsWindowVisible(bar));
    }
    public bool Valid(RdpFocusTarget target) => IsWindow(target.Window) && IsWindow(target.Bar) &&
        IsWindow(target.Toolbar) && Owner(target.Window) == target.ProcessId && Owner(target.Bar) == target.ProcessId &&
        Owner(target.Toolbar) == target.ProcessId && IsChild(target.Bar, target.Toolbar) && GetWindow(target.Bar, 4) == target.Window &&
        Class(target.Window) == "TscShellContainerClass" && Class(target.Bar) == "BBarWindowClass" &&
        Class(target.Toolbar) == "ToolbarWindow32" && FullScreen(target.Window);
    public bool IsTeams(IntPtr window, uint process) => IsWindow(window) && Owner(window) == process &&
        Class(window) == "TeamsWebView" && IsWindowVisible(window) && !IsIconic(window);
    public bool ShowBar(RdpFocusTarget target, bool visible)
    {
        if (!Valid(target)) return false;
        ShowWindowAsync(target.Bar, visible ? 4 : 0);
        var watch = Stopwatch.StartNew();
        while (IsWindowVisible(target.Bar) != visible && watch.ElapsedMilliseconds < 75) Thread.Sleep(5);
        return IsWindowVisible(target.Bar) == visible;
    }
    bool WithInputThreads(IntPtr destination, Func<bool> action)
    {
        if (!ModifiersUp) return false;
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
        var current = GetCurrentThreadId();
        var foreground = GetWindowThreadProcessId(Foreground, out _);
        var target = GetWindowThreadProcessId(destination, out _);
        bool first = foreground != 0 && foreground != current && AttachThreadInput(current, foreground, true);
        bool second = target != 0 && target != current && target != foreground && AttachThreadInput(current, target, true);
        try { return action(); }
        finally
        {
            if (second) AttachThreadInput(current, target, false);
            if (first) AttachThreadInput(current, foreground, false);
        }
    }
    public bool FocusBar(RdpFocusTarget target) => Valid(target) && WithInputThreads(target.Toolbar, () =>
    {
        SetForegroundWindow(target.Bar); SetActiveWindow(target.Bar); SetFocus(target.Toolbar);
        var info = new GuiInfo { Size = (uint)Marshal.SizeOf<GuiInfo>() };
        return GetGUIThreadInfo(GetWindowThreadProcessId(target.Toolbar, out _), ref info) && info.Focus == target.Toolbar;
    });
    public void FocusTeams(IntPtr window) => AutomationElement.FromHandle(window).SetFocus();
    public void Restore(RdpFocusTarget target)
    {
        if (!Valid(target)) return;
        WithInputThreads(IntPtr.Zero, () =>
        {
            SetForegroundWindow(target.Window);
            return Foreground == target.Window;
        });
        if (Foreground == target.Window && IsWindow(target.Input) &&
            Owner(target.Input) == target.ProcessId && IsWindowVisible(target.Input))
            WithInputThreads(target.Input, () =>
            {
                if (Foreground != target.Window) return false;
                SetFocus(target.Input);
                return true;
            });
    }
}
