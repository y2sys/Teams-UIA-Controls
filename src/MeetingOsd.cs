using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.Windows.Shapes.Path;

// A separate STA dispatcher keeps drawing and timers off the UIA command loop.
public static class MeetingOsd {
    static readonly object Sync = new object();
    static readonly ManualResetEvent Ready = new ManualResetEvent(false);
    static bool started;
    static Dispatcher dispatcher = null!;
    static Window window = null!;
    static TextBlock caption = null!;
    static Path glyph = null!;
    static DispatcherTimer timer = null!;
    public static string? Error { get; private set; }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int size; public Rect monitor, work; public uint flags; }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int index, int style);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);

    public static void Start() {
        lock (Sync) {
            if (!started) {
                started = true;
                var thread = new Thread(() => {
                    try {
                        dispatcher = Dispatcher.CurrentDispatcher;
                        CreateWindow(); Ready.Set(); Dispatcher.Run();
                    } catch (Exception ex) { Error = ex.GetType().Name; Ready.Set(); }
                });
                thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start();
            }
        }
    }
    static void CreateWindow() {
        window = new Window { Width = 240, Height = 176, WindowStyle = WindowStyle.None,
            AllowsTransparency = true, Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false, ShowActivated = false, Focusable = false, Topmost = true,
            IsHitTestVisible = false, Title = "Teams UIA Controls Status" };
        var stack = new StackPanel { Margin = new Thickness(18, 12, 18, 14) };
        stack.Children.Add(new TextBlock { Text = "Teams", Foreground = new SolidColorBrush(Color.FromRgb(166, 180, 201)), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center });
        glyph = new Path { StrokeThickness = 5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round, Stretch = Stretch.Uniform, Width = 68, Height = 68, Margin = new Thickness(0, 10, 0, 10) };
        stack.Children.Add(glyph);
        caption = new TextBlock { FontFamily = new FontFamily("Yu Gothic UI"), FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(caption);
        window.Content = new Border { CornerRadius = new CornerRadius(20), Background = new SolidColorBrush(Color.FromArgb(235, 17, 24, 39)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 148, 163, 184)), BorderThickness = new Thickness(1), Child = stack };
        window.SourceInitialized += (sender, args) => {
            var hwnd = new WindowInteropHelper(window).Handle;
            // NOACTIVATE + TRANSPARENT + TOOLWINDOW: no focus, clicks or taskbar.
            SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x08000000 | 0x20 | 0x80);
        };
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
        timer.Tick += (sender, args) => { timer.Stop(); window.Hide(); };
    }
    public static bool Show(string kind, bool state) {
        if (kind != "microphone" && kind != "camera") return false;
        Start();
        if (!Ready.WaitOne(2000) || dispatcher == null || Error != null) return false;
        var monitor = MonitorFromWindow(GetForegroundWindow(), 2);
        dispatcher.BeginInvoke(new Action(() => {
            try {
                bool off = kind == "microphone" ? state : !state;
                caption.Text = kind == "microphone" ? (state ? "マイク ミュート" : "マイク オン") : (state ? "カメラ オン" : "カメラ オフ");
                glyph.Stroke = new SolidColorBrush(off ? Color.FromRgb(248, 113, 113) : Color.FromRgb(74, 222, 128));
                string shape = kind == "microphone"
                    ? "M62,38 A10,10 0 0 1 82,38 L82,61 A10,10 0 0 1 62,61 Z M51,58 L51,64 A21,21 0 0 0 93,64 L93,58 M72,85 L72,98 M58,98 L86,98"
                    : "M43,39 L79,39 Q86,39 86,46 L86,72 Q86,79 79,79 L43,79 Q36,79 36,72 L36,46 Q36,39 43,39 M86,51 L109,39 L109,79 L86,67";
                glyph.Data = Geometry.Parse(shape + (off ? " M36,29 L109,101" : ""));
                var hwnd = new WindowInteropHelper(window).EnsureHandle();
                var info = new MonitorInfo { size = Marshal.SizeOf(typeof(MonitorInfo)) };
                Rect size;
                if (GetMonitorInfo(monitor, ref info) && GetWindowRect(hwnd, out size)) {
                    int width = size.right - size.left, height = size.bottom - size.top;
                    SetWindowPos(hwnd, IntPtr.Zero, info.monitor.left + (info.monitor.right - info.monitor.left - width) / 2,
                        info.monitor.top + (info.monitor.bottom - info.monitor.top - height) / 2, 0, 0, 0x1 | 0x4 | 0x10);
                }
                window.Show(); timer.Stop(); timer.Start();
            } catch (Exception ex) { Error = ex.GetType().Name; }
        }));
        return true;
    }
}
