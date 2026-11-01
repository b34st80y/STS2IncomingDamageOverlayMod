using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace STS2IncomingDamageOverlay;

internal sealed class OverlayWindow : Window
{
    private readonly TextBlock _text = new() { FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
    private readonly TextBlock _hint = new() { Text = "External overlay · Ctrl: drag / right-click to exit", FontSize = 11, Foreground = Brushes.LightGray };
    private readonly Border _panel;
    private readonly CombatMemoryReader _reader = new();
    private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly CancellationTokenSource _stop = new();
    private ReadResult _latest = new("Connecting…");
    private long _lastRead = Environment.TickCount64;
    private nint _handle;
    private bool _editing;
    private bool _overlayVisible = true;
    private OverlayTrayIcon? _tray;
    private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "STS2IncomingDamageOverlay", "position.json");

    public OverlayWindow()
    {
        Title = "STS2 Incoming Damage — External";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        Left = 200; Top = 120;
        var stack = new StackPanel();
        stack.Children.Add(_text); stack.Children.Add(_hint);
        _panel = new Border { Background = new SolidColorBrush(Color.FromArgb(205, 20, 23, 31)), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 9, 14, 9), Child = stack };
        Content = _panel;
        try
        {
            var p = JsonSerializer.Deserialize<double[]>(File.ReadAllText(SettingsPath));
            if (p is { Length: 2 } && p.All(double.IsFinite))
            {
                Left = Math.Clamp(p[0], SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100);
                Top = Math.Clamp(p[1], SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60);
            }
        }
        catch (Exception) { /* Missing or invalid position uses the default. */ }
        MouseLeftButtonDown += (_, _) => { if (_editing) { DragMove(); SavePosition(); } };
        MouseRightButtonUp += (_, _) => { if (_editing) Close(); };
        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            _tray = new OverlayTrayIcon(
                () => Dispatcher.Invoke(() => { _overlayVisible = !_overlayVisible; UpdateDisplay(); }),
                () => Dispatcher.Invoke(Close));
            SetWindowLongPtr(_handle, -20, GetWindowLongPtr(_handle, -20) | 0x80000 | 0x20 | 0x80 | 0x08000000);
            _uiTimer.Tick += (_, _) => UpdateDisplay();
            _uiTimer.Start();
            _ = PollAsync();
        };
        Closed += (_, _) => { _stop.Cancel(); _uiTimer.Stop(); _tray?.Dispose(); SavePosition(); };
    }

    private async Task PollAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var result = await Task.Run(_reader.Read);
                if (_stop.IsCancellationRequested) break;
                _latest = result; _lastRead = Environment.TickCount64;
                UpdateDisplay();
                await Task.Delay(250, _stop.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally { _reader.Dispose(); }
    }

    private void UpdateDisplay()
    {
        bool editing = _overlayVisible && (GetAsyncKeyState(0x11) & 0x8000) != 0;
        if (editing != _editing)
        {
            _editing = editing;
            nint style = GetWindowLongPtr(_handle, -20);
            SetWindowLongPtr(_handle, -20, editing ? style & ~0x20 : style | 0x20);
        }
        _panel.Opacity = 1;
        _hint.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        bool stale = Environment.TickCount64 - _lastRead > 4000;
        var damage = stale ? null : _latest.Damage;
        _text.Text = stale ? "Waiting for a fresh reading…" : damage?.Text ?? _latest.Status;
        _text.Foreground = damage?.IsLethal == true ? Brushes.OrangeRed : damage?.Character switch
        {
            "Ironclad" => Brushes.Salmon,
            "Silent" => Brushes.LightGreen,
            "Regent" => Brushes.Orange,
            "Necrobinder" => Brushes.Violet,
            "Defect" => Brushes.DeepSkyBlue,
            _ => Brushes.White
        };
        if (!_overlayVisible) _panel.Opacity = 0;
        _tray?.Update(_text.Text, _overlayVisible);
    }
    private void SavePosition()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!); File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new[] { Left, Top })); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
