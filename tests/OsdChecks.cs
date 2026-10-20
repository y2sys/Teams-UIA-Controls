public static class MeetingOsdChecks {
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] struct Info { public int size; public Rect monitor, work; public uint flags; }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref Info info);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Run(string imagePath) {
        var before = GetForegroundWindow();
        var monitor = MonitorFromWindow(before, 2);
        Check(MeetingOsd.Show("microphone", true), "OSD request failed");
        Thread.Sleep(200);
        Check(MeetingOsd.Error == null, "OSD rendering failed: " + MeetingOsd.Error);
        Check(GetForegroundWindow() == before, "OSD changed foreground window");
        var window = (Window)typeof(MeetingOsd).GetField("window", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
        window.Dispatcher.Invoke(new Action(() => {
            Check(window.IsVisible, "OSD is not visible");
            var hwnd = new WindowInteropHelper(window).Handle;
            int style = GetWindowLong(hwnd, -20);
            Check((style & 0x08000020) == 0x08000020, "OSD must not activate or intercept clicks");
            Rect bounds;
            var info = new Info { size = Marshal.SizeOf(typeof(Info)) };
            Check(GetWindowRect(hwnd, out bounds) && GetMonitorInfo(monitor, ref info), "OSD bounds unavailable");
            Check(Math.Abs((bounds.left + bounds.right) - (info.monitor.left + info.monitor.right)) <= 4 &&
                Math.Abs((bounds.top + bounds.bottom) - (info.monitor.top + info.monitor.bottom)) <= 4, "OSD is not centered");
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var stream = System.IO.File.Create(imagePath)) encoder.Save(stream);
        }));
        Thread.Sleep(1200);
        Check(window.Dispatcher.Invoke(new Func<bool>(() => !window.IsVisible)), "OSD did not dismiss automatically");
        Check(GetForegroundWindow() == before, "OSD dismissal changed foreground window");
    }
}
