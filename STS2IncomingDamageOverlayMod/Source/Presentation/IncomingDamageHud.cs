using Godot;
using STS2IncomingDamageOverlayMod.Application;
using STS2IncomingDamageOverlayMod.Domain;
using STS2IncomingDamageOverlayMod.Infrastructure;

namespace STS2IncomingDamageOverlayMod.Presentation;

public partial class IncomingDamageHud : CanvasLayer
{
    private readonly PanelContainer _panel = new();
    private readonly Label _label = new();
    private readonly OverlayConfig _config = OverlayConfig.Load();
    private readonly IncomingDamageOverlayService _overlayService = new(new CombatDamageReader());
    private bool _isDragging;
    private bool _wasEditMode;
    private Vector2 _dragOffset;
    private double _nextScanAt;

    public override void _Ready()
    {
        Layer = 128;
        ConfigurePanel();
        ConfigureLabel();
        _panel.AddChild(_label);
        AddChild(_panel);
    }

    public override void _Process(double delta)
    {
        UpdateEditMode();

        if (Time.GetTicksMsec() / 1000.0 >= _nextScanAt)
        {
            _nextScanAt = Time.GetTicksMsec() / 1000.0 + 0.20;
            UpdateIncomingDamage();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsEditMode())
        {
            return;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton)
        {
            HandleMouseButton(mouseButton);
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            _panel.Position = ClampToViewport(mouseMotion.Position - _dragOffset);
            GetViewport().SetInputAsHandled();
        }
    }

    private void ConfigurePanel()
    {
        _panel.Name = "STS2IncomingDamageOverlayModPanel";
        _panel.Position = _config.Position;
        _panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _panel.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0, 0, 0, 0)));
    }

    private void ConfigureLabel()
    {
        _label.Name = "STS2IncomingDamageOverlayModLabel";
        _label.Size = new Vector2(520, 60);
        _label.Text = "";
        _label.Visible = false;
        _label.MouseFilter = Control.MouseFilterEnum.Ignore;
        _label.HorizontalAlignment = HorizontalAlignment.Left;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.AddThemeFontSizeOverride("font_size", 28);
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        _label.AddThemeConstantOverride("shadow_offset_x", 2);
        _label.AddThemeConstantOverride("shadow_offset_y", 2);
    }

    private void UpdateIncomingDamage()
    {
        IncomingDamageSnapshot snapshot = _overlayService.GetSnapshot(GetTree().Root);
        if (!snapshot.ShouldShow)
        {
            HideOverlay();
            return;
        }

        _panel.Visible = true;
        _label.Visible = true;
        _label.Text = OverlayTextFormatter.Format(snapshot);
        _label.AddThemeColorOverride("font_color", snapshot.CharacterColor);
    }

    private void HideOverlay()
    {
        _panel.Visible = false;
        _label.Visible = false;
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.Pressed && IsMouseInsidePanel(mouseButton.Position))
        {
            _isDragging = true;
            _dragOffset = mouseButton.Position - _panel.Position;
            GetViewport().SetInputAsHandled();
        }
        else if (!mouseButton.Pressed && _isDragging)
        {
            _isDragging = false;
            SavePosition();
            GetViewport().SetInputAsHandled();
        }
    }

    private void UpdateEditMode()
    {
        bool editMode = IsEditMode();
        if (editMode == _wasEditMode)
        {
            return;
        }

        _wasEditMode = editMode;
        _panel.MouseFilter = editMode ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
        _label.MouseFilter = editMode ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
        _panel.AddThemeStyleboxOverride("panel", MakePanelStyle(editMode ? new Color(0, 0, 0, 0.45f) : new Color(0, 0, 0, 0)));

        if (!editMode)
        {
            _isDragging = false;
            SavePosition();
        }
    }

    private static bool IsEditMode()
    {
        return Input.IsKeyPressed(Key.Ctrl);
    }

    private bool IsMouseInsidePanel(Vector2 mousePosition)
    {
        Rect2 rect = new(_panel.GlobalPosition, _panel.Size);
        return rect.HasPoint(mousePosition);
    }

    private Vector2 ClampToViewport(Vector2 position)
    {
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        Vector2 max = new(Math.Max(0, viewportSize.X - _panel.Size.X), Math.Max(0, viewportSize.Y - _panel.Size.Y));
        return new Vector2(Math.Clamp(position.X, 0, max.X), Math.Clamp(position.Y, 0, max.Y));
    }

    private void SavePosition()
    {
        _config.SetPosition(_panel.Position);
        _config.Save();
    }

    private static StyleBoxFlat MakePanelStyle(Color background)
    {
        StyleBoxFlat style = new()
        {
            BgColor = background,
            BorderColor = new Color(1, 1, 1, background.A > 0 ? 0.55f : 0),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            ContentMarginLeft = 8,
            ContentMarginTop = 4,
            ContentMarginRight = 8,
            ContentMarginBottom = 4
        };
        return style;
    }
}
