using System;
using System.Linq;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;

namespace Arale.Scripts.Quests;

/// <summary>Run-owned local notebook. Presentation never mutates gameplay or network state.</summary>
public partial class AraleQuestPage : Control
{
    private const string PositionFile = "user://arale_quest_page.cfg";
    private const float PageWidth = 314;
    private const float PageHeight = 448;
    private static readonly Color[] Ink =
    {
        new("#922e44"), new("#397b9b"), new("#b24e7b"), new("#8962a6")
    };
    private readonly MegaRichTextLabel[] _rows = new MegaRichTextLabel[4];
    private readonly AraleQuestPresentation?[] _snapshots = new AraleQuestPresentation?[4];
    public Control Host { get; set; } = null!;
    public Func<AraleQuestBookRelic?> FindBook { get; set; } = () => null;
    public Func<(object? Screen, bool Combat, Vector2? Anchor)> ReadScreen { get; set; } = () => (null, false, null);
    private AraleQuestBookRelic? _book;
    private string? _language;
    private PanelContainer _tooltip = null!;
    private Label _tooltipText = null!;
    private bool _dragging;
    private Vector2 _dragOffset;
    private Vector2? _savedFraction;
    private Vector2 _lastBounds;
    private bool _positioned;
    private double _refreshIn;
    private bool _collapsed;
    private Label _title = null!;
    private Label _hint = null!;
    private Button _toggle = null!;
    private Button _zoomOut = null!, _zoomIn = null!;
    private float _zoom = .8f;
    private bool _combatCollapsed;
    private bool _inCombat;
    private bool _screenKnown;
    private object? _screen;
    private double _saveIn = -1;
    private Vector2? _anchor;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        MouseDefaultCursorShape = CursorShape.Drag;
        Size = new Vector2(PageWidth, PageHeight);
        ZIndex = 100;
        Visible = false;
        var title = _title = new Label
        {
            Text = AraleQuestText.Get("TITLE"), Position = new Vector2(40, 18),
            Size = new Vector2(218, 30), MouseFilter = MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Color("#555366"));
        AddChild(title);
        for (int i = 0; i < _rows.Length; i++)
        {
            var row = new MegaRichTextLabel
            {
                Position = new Vector2(31, 65 + i * 90), Size = new Vector2(267, 84),
                BbcodeEnabled = true, ScrollActive = false, FitContent = false,
                MinFontSize = 12, MaxFontSize = 18, AutoSizeEnabled = true,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore
            };
            row.AddThemeFontOverride("normal_font", GetThemeFont("font", "Label"));
            row.AddThemeFontSizeOverride("normal_font_size", 18);
            row.AddThemeConstantOverride("line_separation", 3);
            row.AddThemeColorOverride("default_color", Ink[i]);
            row.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
            row.AddThemeColorOverride("font_outline_color", Colors.Transparent);
            _rows[i] = row;
            AddChild(row);
        }
        var hint = _hint = new Label
        {
            Text = AraleQuestText.Get("HINT", 80), Position = new Vector2(31, 427),
            MouseFilter = MouseFilterEnum.Ignore
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", new Color("#777785"));
        AddChild(hint);
        _toggle = new Button { Name = "TogglePage", MouseFilter = MouseFilterEnum.Stop, FocusMode = FocusModeEnum.None };
        _toggle.AddThemeFontSizeOverride("font_size", 16);
        _toggle.AddThemeColorOverride("font_color", new Color("#66526e"));
        foreach (string state in new[] { "normal", "hover", "pressed" })
            _toggle.AddThemeStyleboxOverride(state, new StyleBoxFlat {
                BgColor = new Color(state == "normal" ? "#fff2cc" : "#ffe2ed"),
                BorderColor = new Color("#e5bc65"), BorderWidthBottom = 1,
                CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7
            });
        _toggle.Pressed += ToggleCollapsed;
        _toggle.MouseForcePassScrollEvents = false;
        _toggle.GuiInput += HandleWheel;
        AddChild(_toggle);
        _zoomOut = CreateZoomButton("ZoomOut", "−", 216, -.05f);
        _zoomIn = CreateZoomButton("ZoomIn", "+", 250, .05f);

        _tooltip = new PanelContainer { Name = "AraleQuestProgress", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 101, Visible = false };
        var style = new StyleBoxFlat
        {
            BgColor = new Color("#fffcf4"), BorderColor = new Color("#c5bcae"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 12, ContentMarginBottom = 12,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
        };
        _tooltip.AddThemeStyleboxOverride("panel", style);
        _tooltipText = new Label { MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(380, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _tooltipText.AddThemeFontSizeOverride("font_size", 17);
        _tooltipText.AddThemeColorOverride("font_color", new Color("#454052"));
        _tooltip.AddChild(_tooltipText);
        Host.AddChild(_tooltip);
        var config = new ConfigFile();
        if (config.Load(PositionFile) == Error.Ok)
        {
            _combatCollapsed = config.GetValue("page", "collapsed", false).AsBool();
            float savedZoom = (float)config.GetValue("page", "zoom", .8f).AsDouble();
            _zoom = float.IsFinite(savedZoom) ? Math.Clamp(savedZoom, .5f, 1.25f) : .8f;
            var fraction = config.GetValue("page", "fraction", Variant.From(Vector2.Zero));
            if (fraction.VariantType == Variant.Type.Vector2)
            {
                Vector2 value = fraction.AsVector2();
                if (config.HasSectionKey("page", "fraction") && float.IsFinite(value.X) && float.IsFinite(value.Y)) _savedFraction = value.Clamp(Vector2.Zero, Vector2.One);
            }
        }
        ApplyPageMode();
    }

    private Button CreateZoomButton(string name, string text, float x, float step)
    {
        var button = new Button { Name = name, Text = text, Position = new Vector2(x, 416), Size = new Vector2(30, 28),
            FocusMode = FocusModeEnum.None, MouseFilter = MouseFilterEnum.Stop, MouseForcePassScrollEvents = false,
            TooltipText = AraleQuestText.Get(step < 0 ? "ZOOM_OUT" : "ZOOM_IN") };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", new Color("#66526e"));
        button.AddThemeStyleboxOverride("normal", _toggle.GetThemeStylebox("normal"));
        button.AddThemeStyleboxOverride("hover", _toggle.GetThemeStylebox("hover"));
        button.Pressed += () => ChangeZoom(step);
        button.GuiInput += HandleWheel;
        AddChild(button);
        return button;
    }

    private void HandleWheel(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true } wheel) return;
        if (wheel.ButtonIndex != MouseButton.WheelUp && wheel.ButtonIndex != MouseButton.WheelDown) return;
        ChangeZoom(wheel.ButtonIndex == MouseButton.WheelUp ? .05f : -.05f);
        GetViewport().SetInputAsHandled();
    }

    private void ChangeZoom(float delta)
    {
        EndDrag();
        _zoom = Math.Clamp(_zoom + delta, .5f, 1.25f);
        ApplyPageMode();
        Position = Position.Clamp(Vector2.Zero, Travel(Bounds()));
        SavePageState();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(new Vector2(4, 5), Size), new Color(0, 0, 0, .18f));
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("#fffefa"));
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("#e2bc68"), false, 2);
        DrawRect(new Rect2(2, 2, Size.X - 4, _collapsed ? Size.Y - 4 : 50), new Color("#fff3e9"));
        // The ornament lives in the margins, leaving the ruled writing area clear.
        DrawLine(new Vector2(3, 3), new Vector2(Size.X * .5f, 3), new Color("#f292bc"), 3);
        DrawLine(new Vector2(Size.X * .5f, 3), new Vector2(Size.X - 3, 3), new Color("#7bd4e5"), 3);
        DrawStar(new Vector2(12, _collapsed ? 24 : 35), 7, new Color("#edbf48"));
        if (_collapsed) return;
        DrawColoredPolygon(new[] { new Vector2(12, 28), new Vector2(10, 14), new Vector2(23, 24) }, new Color("#3b3444"));
        DrawColoredPolygon(new[] { new Vector2(24, 24), new Vector2(35, 14), new Vector2(34, 29) }, new Color("#3b3444"));
        DrawLine(new Vector2(10, 14), new Vector2(23, 24), new Color("#efc65e"), 2, true);
        DrawLine(new Vector2(35, 14), new Vector2(24, 24), new Color("#efc65e"), 2, true);
        for (int i = 0; i < 12; i++)
        {
            float y = 72 + i * 29;
            Color tint = new(i % 2 == 0 ? "#f4bfda" : "#b6e6ed");
            DrawArc(new Vector2(5, y), 5, -Mathf.Pi / 2, Mathf.Pi / 2, 12, tint, 1.5f, true);
            DrawArc(new Vector2(PageWidth - 5, y), 5, Mathf.Pi / 2, Mathf.Pi * 1.5f, 12, tint, 1.5f, true);
        }
        DrawStar(new Vector2(14, 434), 7, new Color("#efc65e"));
        DrawStar(new Vector2(302, 434), 7, new Color("#efc65e"));
        for (float y = 61; y < 424; y += 30)
            DrawLine(new Vector2(12, y), new Vector2(PageWidth - 12, y), new Color("#d8e4ee"), 1);
        DrawLine(new Vector2(24, 54), new Vector2(24, 420), new Color("#e9b9be"), 1);
        for (int i = 0; i < 4; i++)
            DrawStar(new Vector2(15, 94 + i * 90), 4, Ink[i].Lerp(Colors.White, .4f));
    }

    private void DrawStar(Vector2 center, float radius, Color color)
    {
        var points = new Vector2[10];
        for (int i = 0; i < points.Length; i++)
            points[i] = center + Vector2.FromAngle(i * Mathf.Pi / 5 - Mathf.Pi / 2) * radius * (i % 2 == 0 ? 1 : .45f);
        DrawColoredPolygon(points, color);
    }

    private void ToggleCollapsed()
    {
        EndDrag();
        _collapsed = !_collapsed;
        if (_inCombat) _combatCollapsed = _collapsed;
        ApplyPageMode();
        Position = Position.Clamp(Vector2.Zero, Travel(Bounds()));
        SavePageState();
    }

    private void ApplyPageMode()
    {
        Size = _collapsed ? new Vector2(146, 48) : new Vector2(PageWidth, PageHeight);
        Scale = Vector2.One * (_collapsed ? 1 : _zoom);
        _title.Visible = _hint.Visible = !_collapsed;
        _zoomOut.Visible = _zoomIn.Visible = !_collapsed;
        _hint.Text = AraleQuestText.Get("HINT", Mathf.RoundToInt(_zoom * 100));
        foreach (var row in _rows) row.Visible = !_collapsed;
        _toggle.Position = _collapsed ? new Vector2(26, 8) : new Vector2(272, 18);
        UpdateToggleText();
        _toggle.Size = _collapsed ? new Vector2(112, 32) : new Vector2(28, 28);
        _toggle.TooltipText = AraleQuestText.Get(_collapsed ? "EXPAND" : "COLLAPSE");
        _tooltip.Visible = false;
        QueueRedraw();
    }

    private void UpdateToggleText()
    {
        string text = _collapsed ? AraleQuestText.Get("BOOKMARK", _snapshots.Count(s => s?.Completed == true)) : "−";
        if (_toggle.Text != text) _toggle.Text = text;
    }

    public override void _Process(double delta)
    {
        if (_saveIn >= 0 && (_saveIn -= delta) <= 0) FlushPageState();
        _refreshIn -= delta;
        if (_refreshIn <= 0)
        {
            _refreshIn = .2;
            _book = FindBook();
            Visible = _book?.QuestRolled != 0 && _book != null;
            var context = ReadScreen();
            _anchor = context.Anchor;
            if (!_screenKnown || !ReferenceEquals(_screen, context.Screen) || _inCombat != context.Combat)
            {
                EndDrag();
                _screenKnown = true;
                _screen = context.Screen;
                _inCombat = context.Combat;
                _collapsed = _inCombat ? _combatCollapsed : true;
                ApplyPageMode();
            }
            if (Visible) RefreshRows();
        }
        if (!Visible || !IsVisibleInTree()) { EndDrag(); _tooltip.Visible = false; return; }
        Vector2 bounds = Bounds();
        if (!_positioned)
        {
            Position = _savedFraction.HasValue ? _savedFraction.Value * Travel(bounds)
                : _anchor.HasValue ? _anchor.Value - new Vector2(PageWidth * _zoom + 20, 22)
                : new Vector2(24, 180);
            _positioned = true;
        }
        else if (bounds != _lastBounds && _lastBounds != Vector2.Zero && !_dragging)
        {
            Position = Fraction(Position, _lastBounds) * Travel(bounds);
        }
        _lastBounds = bounds;
        Position = Position.Clamp(Vector2.Zero, Travel(bounds));
        UpdateTooltip(bounds);
    }

    private void RefreshRows()
    {
        if (_language != LocManager.Instance.Language)
        {
            _language = LocManager.Instance.Language;
            _title.Text = AraleQuestText.Get("TITLE");
            _zoomOut.TooltipText = AraleQuestText.Get("ZOOM_OUT");
            _zoomIn.TooltipText = AraleQuestText.Get("ZOOM_IN");
            ApplyPageMode();
        }
        for (int i = 0; i < 4; i++)
        {
            var snapshot = _book!.GetQuestPresentation(i);
            if (snapshot == _snapshots[i]) continue;
            _snapshots[i] = snapshot;
            if (snapshot == null) continue;
            Color color = snapshot.Completed ? Ink[i].Lerp(new Color("#fffefa"), .48f) : Ink[i];
            _rows[i].AddThemeColorOverride("default_color", color);
            // RichTextLabel shapes strike-through per wrapped line, including CJK wrapping.
            _rows[i].Text = snapshot.Completed ? $"[s]{snapshot.Text}[/s]" : snapshot.Text;
        }
        UpdateToggleText();
    }

    private void UpdateTooltip(Vector2 bounds)
    {
        if (_collapsed) { _tooltip.Visible = false; return; }
        int slot = -1;
        Vector2 mouse = GetLocalMousePosition();
        if (!_dragging && mouse.X >= 24 && mouse.X <= PageWidth && mouse.Y >= 65 && mouse.Y < 425)
            slot = Math.Clamp((int)((mouse.Y - 65) / 90), 0, 3);
        _tooltip.Visible = slot >= 0 && _snapshots[slot] != null;
        if (!_tooltip.Visible) return;
        if (_tooltipText.Text != _snapshots[slot]!.Tooltip)
        {
            _tooltipText.Text = _snapshots[slot]!.Tooltip;
            _tooltip.ResetSize();
        }
        Vector2 size = _tooltip.GetCombinedMinimumSize();
        Vector2 pos = Position + new Vector2(PageWidth * Scale.X + 10, (65 + slot * 90) * Scale.Y);
        if (pos.X + size.X > bounds.X) pos.X = Position.X - size.X - 10;
        _tooltip.Position = pos.Clamp(Vector2.Zero, (bounds - size).Max(Vector2.Zero));
    }

    public override void _GuiInput(InputEvent @event)
    {
        HandleWheel(@event);
        if (@event is not InputEventMouseButton button || button.ButtonIndex != MouseButton.Left) return;
        if (button.Pressed)
        {
            _dragging = true;
            _dragOffset = Host.GetLocalMousePosition() - Position;
            _tooltip.Visible = false;
        }
        else EndDrag();
        AcceptEvent();
    }

    public override void _Input(InputEvent input)
    {
        if (!_dragging) return;
        if (input is InputEventMouseMotion)
        {
            Position = (Host.GetLocalMousePosition() - _dragOffset).Clamp(Vector2.Zero, Travel(Bounds()));
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            EndDrag();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut && IsInsideTree()) EndDrag();
    }

    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        Position = Position.Clamp(Vector2.Zero, Travel(Bounds()));
        _savedFraction = Fraction(Position, Bounds());
        SavePageState();
    }

    private void SavePageState()
    {
        _savedFraction = Fraction(Position, Bounds());
        _saveIn = .35;
    }

    private void FlushPageState()
    {
        _saveIn = -1;
        var config = new ConfigFile();
        config.SetValue("page", "fraction", _savedFraction ?? Fraction(Position, Bounds()));
        config.SetValue("page", "collapsed", _combatCollapsed);
        config.SetValue("page", "zoom", _zoom);
        config.Save(PositionFile);
    }

    private Vector2 Bounds() => Host.Size;
    private Vector2 Travel(Vector2 bounds) => (bounds - Size * Scale).Max(Vector2.Zero);
    private Vector2 Fraction(Vector2 position, Vector2 bounds)
    {
        var travel = Travel(bounds);
        return new Vector2(travel.X > 0 ? position.X / travel.X : 0, travel.Y > 0 ? position.Y / travel.Y : 0).Clamp(Vector2.Zero, Vector2.One);
    }

    public override void _ExitTree()
    {
        if (_saveIn >= 0) FlushPageState();
        if (GodotObject.IsInstanceValid(_tooltip)) _tooltip.QueueFree();
    }
}


