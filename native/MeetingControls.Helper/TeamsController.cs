using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace MeetingControls.Helper;

public sealed class TeamsController
{
    private sealed record History(MeetingSession Row, MeetingSession? Stable, long Seen);
    private readonly Dictionary<string, AutomationElement> windows = [];
    private readonly Dictionary<string, Dictionary<string, AutomationElement?>> controls = [];
    private Dictionary<string, History> previous = [];
    private static readonly Regex Compact = new(@"^(会議のコンパクト ビュー|Compact meeting view)\s*\|", RegexOptions.Compiled);
    public bool Healthy { get; private set; } = true;
    public static long Now => (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);

    private static AutomationElement? Find(AutomationElement window, string id) => window.FindFirst(
        TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, id));

    public static bool? ReadLabel(string? label, string[] trueLabels, string[] falseLabels)
    {
        var name = label?.Trim();
        if (name is null) return null;
        if (trueLabels.Contains(name, StringComparer.OrdinalIgnoreCase)) return true;
        if (falseLabels.Contains(name, StringComparer.OrdinalIgnoreCase)) return false;
        return null;
    }

    private static bool CanInvoke(AutomationElement? element) => element is not null && element.Current.IsEnabled &&
        element.TryGetCurrentPattern(InvokePattern.Pattern, out _);

    private static bool CanLeave(AutomationElement? element) => CanInvoke(element) ||
        (element is not null && element.Current.IsEnabled && element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out _));

    private static bool IsPicker(AutomationElement window) => window.FindFirst(TreeScope.Descendants, new OrCondition(
        new PropertyCondition(AutomationElement.NameProperty, "サウンドを含める"),
        new PropertyCondition(AutomationElement.NameProperty, "Include sound"))) is not null;

    public static MeetingSession TransitionFrom(MeetingSession old, MeetingSession? stable, bool picker) => new()
    {
        Id = old.Id, Title = old.Title, Picker = picker, Transition = true, LastKnown = stable,
        Capabilities = new() { ["microphone"] = false, ["camera"] = false, ["share"] = false, ["leave"] = false }
    };

    public IReadOnlyList<MeetingSession> Snapshot()
    {
        Healthy = true;
        windows.Clear(); controls.Clear();
        var rows = new List<MeetingSession>();
        var next = new Dictionary<string, History>();
        var processes = Process.GetProcessesByName("ms-teams");
        var ids = processes.Select(process => process.Id).ToHashSet();
        foreach (var process in processes) process.Dispose();
        if (ids.Count == 0) { previous.Clear(); return rows; }
        try
        {
            var desktopWindows = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
            foreach (AutomationElement window in desktopWindows)
            {
                try
                {
                    var info = window.Current;
                    if (!ids.Contains(info.ProcessId) || info.ClassName != "TeamsWebView") continue;
                    var id = $"teams:{info.ProcessId}:{info.NativeWindowHandle}";
                    windows[id] = window;
                    var leave = Find(window, "hangup-button");
                    if (leave is not null)
                    {
                        var mic = Find(window, "microphone-button");
                        var camera = Find(window, "video-button");
                        var share = Find(window, "share-button");
                        var muted = ReadLabel(mic?.Current.Name,
                            ["マイクのミュートを解除", "Unmute mic", "Unmute microphone", "Unmute"],
                            ["マイクのミュート", "Mute mic", "Mute microphone", "Mute"]);
                        var cameraOn = ReadLabel(camera?.Current.Name,
                            ["カメラをオフにする", "カメラをオフ", "Turn camera off"],
                            ["カメラをオン", "カメラをオンにする", "Turn camera on"]);
                        var sharing = ReadLabel(share?.Current.Name, ["共有を停止", "共有停止", "Stop sharing"], ["共有", "Share", "Share content"]);
                        var row = new MeetingSession
                        {
                            Id = id, Title = info.Name, Muted = muted, CameraOn = cameraOn, Sharing = sharing,
                            Capabilities = new()
                            {
                                ["microphone"] = muted.HasValue && CanInvoke(mic), ["camera"] = cameraOn.HasValue && CanInvoke(camera),
                                ["share"] = sharing.HasValue && CanInvoke(share), ["leave"] = CanLeave(leave)
                            }
                        };
                        var stable = previous.GetValueOrDefault(id)?.Stable;
                        if (muted.HasValue && cameraOn.HasValue && sharing.HasValue) stable = row;
                        else if (IsPicker(window)) { row.Picker = true; row.LastKnown = stable; }
                        rows.Add(row);
                        next[id] = new History(row, stable, Now);
                        controls[id] = new() { ["microphone"] = mic, ["camera"] = camera, ["share"] = share, ["leave"] = leave };
                    }
                    else if (previous.TryGetValue(id, out var old))
                    {
                        var picker = IsPicker(window);
                        if (picker || Now - old.Seen < 3000)
                        {
                            var row = TransitionFrom(old.Row, old.Stable, picker);
                            rows.Add(row); next[id] = new History(row, old.Stable, old.Seen);
                        }
                    }
                }
                catch { Healthy = false; }
            }
        }
        catch { Healthy = false; }
        previous = next;
        return Deduplicate(rows);
    }

    public static IReadOnlyList<MeetingSession> Deduplicate(IReadOnlyList<MeetingSession> rows)
    {
        var full = rows.Where(row => !Compact.IsMatch(row.Title)).ToArray();
        return rows.Where(row => !Compact.IsMatch(row.Title) || !full.Any(main => row.Title.Contains(main.Title, StringComparison.Ordinal))).ToArray();
    }

    // Refresh and validate the exact selected session. Never route to another call.
    public (MeetingSession Session, AutomationElement Element, uint ProcessId) Resolve(HelperCommand command)
    {
        if (command.Kind is not ("microphone" or "camera" or "share" or "leave")) throw new InvalidOperationException();
        var sessions = Snapshot().Where(row => row.Id == command.SessionId).ToArray();
        if (!Healthy || sessions.Length != 1 || sessions[0].Picker ||
            !sessions[0].Capabilities.GetValueOrDefault(command.Kind) ||
            !controls.TryGetValue(command.SessionId, out var buttons) || buttons[command.Kind] is not { } element)
            throw new InvalidOperationException();
        return (sessions[0], element, (uint)windows[command.SessionId].Current.ProcessId);
    }

    public IntPtr MeetingWindow(string sessionId) => new(windows[sessionId].Current.NativeWindowHandle);

    public void Invoke(HelperCommand command, AutomationElement element)
    {
        if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern)) { ((InvokePattern)pattern).Invoke(); return; }
        if (command.Kind != "leave") throw new InvalidOperationException();
        var expand = (ExpandCollapsePattern)element.GetCurrentPattern(ExpandCollapsePattern.Pattern);
        expand.Expand();
        Thread.Sleep(200);
        var candidates = new List<AutomationElement>();
        foreach (AutomationElement item in windows[command.SessionId].FindAll(TreeScope.Descendants, Condition.TrueCondition))
        {
            var info = item.Current;
            if (info.AutomationId is not ("hangup-button" or "hangup-button-more-options") &&
                new[] { "退出", "退出します", "会議から退出", "Leave", "Leave meeting" }.Contains(info.Name, StringComparer.OrdinalIgnoreCase) && CanInvoke(item))
                candidates.Add(item);
        }
        if (candidates.Count != 1) { try { expand.Collapse(); } catch { } throw new InvalidOperationException(); }
        ((InvokePattern)candidates[0].GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }
}
