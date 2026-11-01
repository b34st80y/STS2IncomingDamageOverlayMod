using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace STS2IncomingDamageOverlay;

internal sealed class OverlayTrayIcon : IDisposable
{
    private readonly Icon _icon;
    private readonly Forms.NotifyIcon _tray;
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly Forms.ToolStripMenuItem _status = new("Connecting…") { Enabled = false };
    private readonly Forms.ToolStripMenuItem _toggle = new("Hide overlay");

    public OverlayTrayIcon(Action toggle, Action exit)
    {
        _icon = CreateIcon();
        _menu.Items.Add(new Forms.ToolStripMenuItem("STS2 Incoming Damage") { Enabled = false });
        _menu.Items.Add(_status);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(_toggle);
        _toggle.Click += (_, _) => toggle();
        _menu.Items.Add("Exit", null, (_, _) => exit());
        _tray = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "STS2 Incoming Damage — running",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => toggle();
    }

    public void Update(string status, bool visible)
    {
        string tooltip = $"STS2 Incoming Damage{(visible ? "" : " (hidden)")}\n{status}";
        // A short tooltip works across supported Windows notification-area versions.
        if (tooltip.Length > 63) tooltip = tooltip[..60] + "…";
        if (_tray.Text != tooltip) _tray.Text = tooltip;
        if (_status.Text != status) _status.Text = status;
        _toggle.Text = visible ? "Hide overlay" : "Show overlay";
    }

    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var fill = new SolidBrush(Color.FromArgb(245, 151, 38));
        graphics.FillPolygon(fill, [new(4, 3), new(28, 3), new(26, 21), new(16, 30), new(6, 21)]);
        using var pen = new Pen(Color.White, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawLine(pen, 16, 8, 16, 22);
        graphics.DrawLine(pen, 11, 17, 21, 17);
        nint handle = bitmap.GetHicon();
        try { using var temporary = Icon.FromHandle(handle); return (Icon)temporary.Clone(); }
        finally { DestroyIcon(handle); }
    }

    public void Dispose()
    {
        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);
}
