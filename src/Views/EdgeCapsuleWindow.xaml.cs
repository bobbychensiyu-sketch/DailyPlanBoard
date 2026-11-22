using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DailyPlanBoard.Models;
using DailyPlanBoard.Services;

namespace DailyPlanBoard.Views;

public partial class EdgeCapsuleWindow : Window
{
    private readonly App _app;
    private readonly StorageService _storage;
    private readonly DispatcherTimer _hoverTimer;
    private readonly Func<Rect> _getMonitorWorkArea;

    private bool _allowClose;
    private bool _isDragging;
    private bool _dragMoved;
    private System.Windows.Point _dragStartScreenDip;
    private double _dragStartTop;
    private double _dragStartLeft;

    public bool IsContextMenuOpen { get; private set; }

    public event Action? HoverExpandRequested;
    public event Action? ClickExpandRequested;
    public event Action<DockEdgeSide>? SwitchDockSideRequested;
    public event Action? OpenSettingsRequested;
    public event Action? HideToTrayRequested;

    private int _lastPending;
    private int _lastTotal;

    public EdgeCapsuleWindow(App app, StorageService storage, Func<Rect> getMonitorWorkArea)
    {
        InitializeComponent();
        _app = app;
        _storage = storage;
        _getMonitorWorkArea = getMonitorWorkArea;

        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(110) };
        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer.Stop();
            if (!_isDragging && _storage.Settings.HoverExpandEnabled)
                HoverExpandRequested?.Invoke();
        };

        Loaded += (_, _) =>
        {
            NativeWindowHelper.ApplyToolWindowStyle(this, true);
            NativeWindowHelper.SetExcludeFromCapture(this, _storage.Settings.HideFromCapture);
            ApplySideVisuals();
            ApplyLanguage();
            NativeWindowHelper.ApplyPinMode(this, WindowPinMode.TopMost);
        };

        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
                NativeWindowHelper.ApplyPinMode(this, WindowPinMode.TopMost);
            }
        };

        PillBorder.LostMouseCapture += (_, _) =>
        {
            _isDragging = false;
            _dragMoved = false;
        };

        BuildContextMenu();
    }

    public void AllowClose() => _allowClose = true;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            return;
        }
        base.OnClosing(e);
    }

    public void ApplyLanguage()
    {
        bool isZh = L10n.Current == AppLanguage.Zh;
        ZhLabelPanel.Visibility = isZh ? Visibility.Visible : Visibility.Collapsed;
        LatinLabelText.Visibility = isZh ? Visibility.Collapsed : Visibility.Visible;
        BuildContextMenu();
        UpdatePendingCount(_lastPending, _lastTotal);
    }

    private void BuildContextMenu()
    {
        var ctx = new ContextMenu();
        ctx.Opened += (_, _) => IsContextMenuOpen = true;
        ctx.Closed += (_, _) =>
        {
            IsContextMenuOpen = false;
            NativeWindowHelper.ApplyPinMode(this, WindowPinMode.TopMost);
        };
        var mPin = new MenuItem { Header = L10n.T("CapPinExpand") };
        mPin.Click += (_, _) => ClickExpandRequested?.Invoke();
        ctx.Items.Add(mPin);

        var mLeft = new MenuItem { Header = L10n.T("CapDockLeft") };
        mLeft.Click += (_, _) => SwitchDockSideRequested?.Invoke(DockEdgeSide.Left);
        ctx.Items.Add(mLeft);

        var mRight = new MenuItem { Header = L10n.T("CapDockRight") };
        mRight.Click += (_, _) => SwitchDockSideRequested?.Invoke(DockEdgeSide.Right);
        ctx.Items.Add(mRight);

        ctx.Items.Add(new Separator());

        var mSettings = new MenuItem { Header = L10n.T("CapSettings") };
        mSettings.Click += (_, _) => OpenSettingsRequested?.Invoke();
        ctx.Items.Add(mSettings);

        var mTray = new MenuItem { Header = L10n.T("CapHideTray") };
        mTray.Click += (_, _) => HideToTrayRequested?.Invoke();
        ctx.Items.Add(mTray);

        ctx.Items.Add(new Separator());
        var mExit = new MenuItem { Header = L10n.T("CapExit") };
        mExit.Click += (_, _) => _app.ExitApplication();
        ctx.Items.Add(mExit);

        PillBorder.ContextMenu = ctx;
    }

    public void UpdatePendingCount(int pending, int total)
    {
        _lastPending = pending;
        _lastTotal = total;
        if (pending == 0 && total > 0)
        {
            CountText.Text = "✓";
            CountBadge.Background = (System.Windows.Media.Brush)FindResource("AccentSoftBrush");
            CountText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
        }
        else
        {
            CountText.Text = pending.ToString();
            CountBadge.Background = (System.Windows.Media.Brush)FindResource("AccentSoftBrush");
            CountText.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
        }
        ToolTip = L10n.TF("CapsuleTooltip", pending, total);
    }

    public void ApplySideVisuals()
    {
        var side = _storage.Settings.DockSide;
        if (side == DockEdgeSide.Left)
        {
            PillBorder.CornerRadius = new CornerRadius(0, 12, 12, 0);
            PillBorder.BorderThickness = new Thickness(0, 1, 1, 1);
        }
        else
        {
            PillBorder.CornerRadius = new CornerRadius(12, 0, 0, 12);
            PillBorder.BorderThickness = new Thickness(1, 1, 0, 1);
        }
        PillBorder.Opacity = Math.Clamp(_storage.Settings.WindowOpacity, 0.72, 1.0);
    }

    public void PositionOnEdge(Rect workArea)
    {
        ApplySideVisuals();
        double maxTop = Math.Max(workArea.Top + 10, workArea.Bottom - Height - 10);
        double targetTop = Math.Clamp(_storage.Settings.CapsuleTop, workArea.Top + 10, maxTop);
        _storage.Settings.CapsuleTop = targetTop;

        Top = targetTop;
        Left = _storage.Settings.DockSide == DockEdgeSide.Left
            ? workArea.Left
            : workArea.Right - Width;
    }

    private void Pill_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        PillBorder.Background = (System.Windows.Media.Brush)FindResource("SurfaceBgBrush");
        if (!_isDragging && _storage.Settings.HoverExpandEnabled)
        {
            _hoverTimer.Stop();
            _hoverTimer.Start();
        }
    }

    private void Pill_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        PillBorder.Background = (System.Windows.Media.Brush)FindResource("CardBgBrush");
        _hoverTimer.Stop();
    }

    private void Pill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _hoverTimer.Stop();
        _isDragging = true;
        _dragMoved = false;
        _dragStartScreenDip = NativeWindowHelper.GetCursorDip(this);
        _dragStartTop = Top;
        _dragStartLeft = Left;
        PillBorder.CaptureMouse();
    }

    private void Pill_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging) return;

        var cur = NativeWindowHelper.GetCursorDip(this);
        double dx = cur.X - _dragStartScreenDip.X;
        double dy = cur.Y - _dragStartScreenDip.Y;

        if (!_dragMoved && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4))
            _dragMoved = true;

        if (!_dragMoved) return;

        var work = _getMonitorWorkArea();
        double maxTop = Math.Max(work.Top + 8, work.Bottom - Height - 8);
        Top = Math.Clamp(_dragStartTop + dy, work.Top + 8, maxTop);
        Left = Math.Clamp(_dragStartLeft + dx, work.Left, work.Right - Width);
    }

    private void Pill_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        PillBorder.ReleaseMouseCapture();

        if (!_dragMoved)
        {
            ClickExpandRequested?.Invoke();
            return;
        }

        // 拖拽结束：自动吸附到就近左/右边缘并保存高度
        var work = _getMonitorWorkArea();
        double centerX = Left + Width / 2.0;
        double midX = work.Left + work.Width / 2.0;

        _storage.Settings.DockSide = centerX < midX ? DockEdgeSide.Left : DockEdgeSide.Right;
        _storage.Settings.CapsuleTop = Top;
        PositionOnEdge(work);
        _storage.SaveSettings();
    }
}
