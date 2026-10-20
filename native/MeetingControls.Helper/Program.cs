using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeetingControls.Helper;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [MTAThread]
    private static int Main(string[] args)
    {
        Console.InputEncoding = new UTF8Encoding(false);
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args is ["--osd-preview"])
        {
            var shown = MeetingOsd.Show("microphone", true);
            Thread.Sleep(1400);
            if (!shown || MeetingOsd.Error is not null) return 1;
            Console.WriteLine("Packaged helper OSD preview completed");
            return 0;
        }
        var controller = new TeamsController();
        var notices = new MeetingTransitionNotices();
        var input = new ConcurrentQueue<string>();
        var ready = new AutoResetEvent(false);
        var closed = 0;
        var reader = new Thread(() =>
        {
            try
            {
                while (Console.ReadLine() is { } line) { input.Enqueue(line); ready.Set(); }
            }
            catch (IOException) { }
            finally { Interlocked.Exchange(ref closed, 1); ready.Set(); }
        }) { IsBackground = true };
        reader.Start();
        MeetingOsd.Start();
        FocusReturnGuard? guard = null;
        var refreshes = new List<long>();
        long nextPoll = 0;

        void Send<T>(T value) { Console.WriteLine(JsonSerializer.Serialize(value, Json)); }
        void Publish()
        {
            var sessions = controller.Snapshot();
            Send(new StateRecord(sessions, controller.Healthy ? "" : "UI Automation read failed"));
            var now = TeamsController.Now;
            notices.Prune(now);
            if (!controller.Healthy) return;
            foreach (var session in sessions)
            {
                if (session.Picker || session.Transition) continue;
                foreach (var kind in new[] { "microphone", "camera" })
                {
                    var notice = notices.Observe(session.Id, kind, kind == "microphone" ? session.Muted : session.CameraOn, now);
                    if (notice is not null) MeetingOsd.Show(notice.Kind, notice.State);
                }
            }
        }

        try
        {
            while (Volatile.Read(ref closed) == 0)
            {
                if (guard is not null && !guard.TryRestore()) guard = null;
                while (input.TryDequeue(out var line))
                {
                    HelperCommand? command = null;
                    try
                    {
                        command = JsonSerializer.Deserialize<HelperCommand>(line, Json) ?? throw new InvalidOperationException();
                        guard = null;
                        var (session, element, process) = controller.Resolve(command);
                        if (command.Kind is "microphone" or "camera") guard = FocusReturnGuard.Capture(process);
                        try { controller.Invoke(command, element); }
                        finally { if (guard is not null && !guard.TryRestore()) guard = null; }
                        if (command.Kind is "microphone" or "camera")
                            notices.Begin(command.SessionId, command.Kind, (command.Kind == "microphone" ? session.Muted : session.CameraOn)!.Value, TeamsController.Now);
                        Publish();
                        Send(new ResultRecord(command.RequestId, true));
                        var now = TeamsController.Now;
                        refreshes = [now + 80, now + 200, now + 450];
                    }
                    catch { Send(new ResultRecord(command?.RequestId ?? "", false, "Meeting control unavailable")); }
                }
                var current = TeamsController.Now;
                if (current >= nextPoll || refreshes.Any(time => time <= current))
                {
                    refreshes.RemoveAll(time => time <= current);
                    try { Publish(); }
                    catch { Send(new StateRecord([], "UI Automation unavailable")); }
                    nextPoll = TeamsController.Now + 750;
                }
                var wake = refreshes.Aggregate(nextPoll, Math.Min);
                ready.WaitOne((int)Math.Clamp(wake - TeamsController.Now, 0, 750));
            }
        }
        catch (IOException) { return 0; }
        return 0;
    }
}
