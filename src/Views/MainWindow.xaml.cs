using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DailyPlanBoard.Models;
using DailyPlanBoard.Services;

namespace DailyPlanBoard.Views;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x51A1;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;

    private readonly App _app;
    private readonly StorageService _storage;
    private readonly EdgeCapsuleWindow _capsule;
    private readonly DispatcherTimer _edgeWatchTimer;
    private readonly DispatcherTimer _fullscreenTimer;

    private DateTime _currentDate = DateTime.Today;
    private string _filterMode = "all"; // all | active | done
    private bool _allowClose;
    private bool _isHoverPeeking;
    private bool _isAutoCollapseArmed;
    private bool _hasOpenDialog;
    private bool _hiddenForFullscreen;
    private bool _explicitlyHiddenToTray;
    private bool _isResizing;
    private DateTime _mouseOutsideSince = DateTime.MinValue;

    // Quick add transient state
    private TaskPriority _quickPriority = TaskPriority.Normal;
    private string? _quickReminderTime;
    private bool _quickDailyRepeat;
    private string? _quickMilestoneDate;

    private static readonly Regex InlineTimeRegex = new(
        @"(^|\s)([01]?\d|2[0-3])[:：]([0-5]\d)(?=$|\s)",
        RegexOptions.Compiled);

    private static readonly Regex InlineMilestoneRegex = new(
        @"(^|\s)(?:@|节点[:：]?|截止[:：]?)(?:(20\d{2})[-/.年])?(0?[1-9]|1[0-2])[-/.月](0?[1-9]|[12]\d|3[01])日?(?=$|\s)",
        RegexOptions.Compiled);

    public MainWindow(App app, StorageService storage)
    {
        InitializeComponent();
        _app = app;
        _storage = storage;

        _capsule = new EdgeCapsuleWindow(_app, _storage, () =>
            NativeWindowHelper.GetMonitorWorkAreaDip(_capsule != null && _capsule.IsVisible ? _capsule : this));
        _capsule.HoverExpandRequested += () => ExpandFromCapsule(hoverPeek: true);
        _capsule.ClickExpandRequested += () => ExpandFromCapsule(hoverPeek: false);
        _capsule.SwitchDockSideRequested += side =>
        {
            _storage.Settings.DockSide = side;
            _storage.SaveSettings();
            var work = NativeWindowHelper.GetMonitorWorkAreaDip(this);
            _capsule.PositionOnEdge(work);
        };
        _capsule.OpenSettingsRequested += OpenSettingsWindow;
        _capsule.HideToTrayRequested += () =>
        {
            _explicitlyHiddenToTray = true;
            _capsule.Hide();
            Hide();
            NativeWindowHelper.TrimMemory();
        };

        _edgeWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _edgeWatchTimer.Tick += EdgeWatchTimer_Tick;

        _fullscreenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        _fullscreenTimer.Tick += FullscreenTimer_Tick;

        RestoreWindowBounds();
        Topmost = _storage.Settings.PinMode == WindowPinMode.TopMost;
        ApplyOpacity();
        ApplyLanguage();

        SourceInitialized += OnSourceInitialized;
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
                ReapplyEffectivePinMode();
            }
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Loaded += (_, _) =>
        {
            ApplyPinModeVisuals();
            NativeWindowHelper.ApplyToolWindowStyle(this, !_storage.Settings.ShowInTaskbar);
            NativeWindowHelper.SetExcludeFromCapture(this, _storage.Settings.HideFromCapture);
            NativeWindowHelper.ApplyPinMode(this, _storage.Settings.PinMode);
            RegisterConfiguredHotkey();
            RunAutoRolloverIfEnabled();
            RefreshList();

            if (_storage.Settings.IsCollapsedToEdge)
            {
                CollapseToEdge(save: false);
            }
            else if (_storage.Settings.AutoCollapseOnEdge)
            {
                _mouseOutsideSince = DateTime.MinValue;
                _edgeWatchTimer.Start();
            }
            _fullscreenTimer.Start();
        };
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        SizeChanged += (_, _) => UpdateProgressWidth();
        LocationChanged += (_, _) => SaveBoundsDeferred();
    }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;
        if (mods == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.W:
                    CollapseToEdge();
                    e.Handled = true;
                    return;
                case Key.N:
                case Key.K:
                    QuickAddBox.Focus();
                    e.Handled = true;
                    return;
                case Key.L:
                    var nextLang = (AppLanguage)(((int)_storage.Settings.Language + 1) % 3);
                    _app.SetLanguage(nextLang);
                    e.Handled = true;
                    return;
                case Key.T:
                    CycleTheme_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    return;
                case Key.P:
                    CyclePinMode_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    return;
                case Key.Home:
                    _currentDate = DateTime.Today;
                    RefreshList();
                    e.Handled = true;
                    return;
                case Key.M:
                    OpenCalendarWindow();
                    e.Handled = true;
                    return;
            }
        }
        else if (mods == ModifierKeys.Alt)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Left)
            {
                _currentDate = _currentDate.AddDays(-1);
                RefreshList();
                e.Handled = true;
            }
            else if (key == Key.Right)
            {
                _currentDate = _currentDate.AddDays(1);
                RefreshList();
                e.Handled = true;
            }
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeWindowHelper.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            ToggleByHotkey();
            return IntPtr.Zero;
        }

        if (msg == NativeWindowHelper.WM_DISPLAYCHANGE)
        {
            Dispatcher.BeginInvoke(EnsureVisibleInWorkArea);
        }

        return NativeWindowHelper.DesktopModeWndProc(
            hwnd, msg, wParam, lParam,
            () => _storage.Settings.PinMode == WindowPinMode.Desktop && !_isHoverPeeking);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        try { Dispatcher.BeginInvoke(EnsureVisibleInWorkArea); } catch { }
    }

    private void EnsureVisibleInWorkArea()
    {
        if (_capsule.IsVisible)
        {
            var capWork = NativeWindowHelper.GetMonitorWorkAreaDip(_capsule);
            double expectedLeft = _storage.Settings.DockSide == DockEdgeSide.Left
                ? capWork.Left
                : capWork.Right - _capsule.Width;
            if (Math.Abs(_capsule.Left - expectedLeft) > 1.5 ||
                _capsule.Top < capWork.Top ||
                _capsule.Top > capWork.Bottom - _capsule.Height)
            {
                _capsule.PositionOnEdge(capWork);
            }
            if (!_hiddenForFullscreen && !_capsule.IsContextMenuOpen)
                NativeWindowHelper.ApplyPinMode(_capsule, WindowPinMode.TopMost);
        }

        if (IsVisible && WindowState == WindowState.Normal)
        {
            var work = NativeWindowHelper.GetMonitorWorkAreaDip(this);
            Width = Math.Clamp(Width, MinWidth, Math.Max(MinWidth, work.Width - 10));
            Height = Math.Clamp(Height, MinHeight, Math.Max(MinHeight, work.Height - 10));

            if (_storage.Settings.DockSide == DockEdgeSide.Right)
                Left = work.Right - Width;
            else if (_storage.Settings.DockSide == DockEdgeSide.Left)
                Left = work.Left;
            else
                Left = Math.Clamp(Left, work.Left, Math.Max(work.Left, work.Right - Width));

            Top = Math.Clamp(Top, work.Top, Math.Max(work.Top, work.Bottom - Height));
            ReapplyEffectivePinMode();
        }
    }

    public bool RegisterConfiguredHotkey()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return false;

        NativeWindowHelper.UnregisterHotKey(hwnd, HotkeyId);

        if (!NativeWindowHelper.TryParseHotkey(_storage.Settings.GlobalHotkey, out uint mods, out uint vk))
            return true; // Intentionally disabled

        return NativeWindowHelper.RegisterHotKey(hwnd, HotkeyId, mods, vk);
    }

    private void ToggleByHotkey()
    {
        if (_capsule.IsVisible && !IsVisible)
        {
            ExpandFromCapsule(hoverPeek: false);
        }
        else if (IsVisible)
        {
            if (_storage.Settings.AutoCollapseOnEdge || _storage.Settings.DockSide != DockEdgeSide.None)
                CollapseToEdge();
            else
            {
                _explicitlyHiddenToTray = true;
                SaveWindowBounds();
                Hide();
                NativeWindowHelper.TrimMemory();
            }
        }
        else
        {
            ExpandFromAnywhere(activate: true);
        }
    }

    private void RestoreWindowBounds()
    {
        var s = _storage.Settings;
        Width = Math.Clamp(s.WindowWidth, MinWidth, 1200);
        Height = Math.Clamp(s.WindowHeight, MinHeight, 1600);

        var work = SystemParameters.WorkArea;
        bool unset = s.WindowLeft == -1 && s.WindowTop == -1;
        double vLeft = SystemParameters.VirtualScreenLeft;
        double vTop = SystemParameters.VirtualScreenTop;
        double vRight = vLeft + SystemParameters.VirtualScreenWidth;
        double vBottom = vTop + SystemParameters.VirtualScreenHeight;

        if (unset ||
            s.WindowLeft < vLeft - Width + 60 || s.WindowLeft > vRight - 60 ||
            s.WindowTop < vTop || s.WindowTop > vBottom - 60)
        {
            Left = work.Right - Width - 22;
            Top = work.Top + 26;
        }
        else
        {
            Left = s.WindowLeft;
            Top = s.WindowTop;
        }

        ShowInTaskbar = s.ShowInTaskbar;
    }

    private void SaveBoundsDeferred()
    {
        if (!IsLoaded || !IsVisible || WindowState != WindowState.Normal) return;
        if (Left < -10000 || Top < -10000 || Width < MinWidth || Height < MinHeight) return;
        _storage.Settings.WindowLeft = Left;
        _storage.Settings.WindowTop = Top;
        _storage.Settings.WindowWidth = Width;
        _storage.Settings.WindowHeight = Height;
    }

    public void SaveWindowBounds()
    {
        SaveBoundsDeferred();
        _storage.SaveSettings();
    }

    public void ForceClose()
    {
        _allowClose = true;
        _edgeWatchTimer.Stop();
        _fullscreenTimer.Stop();
        try { Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged; } catch { }
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            NativeWindowHelper.UnregisterHotKey(hwnd, HotkeyId);
        _capsule.AllowClose();
        _capsule.Close();
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            if (_storage.Settings.DockSide != DockEdgeSide.None && _storage.Settings.AutoCollapseOnEdge)
            {
                CollapseToEdge();
            }
            else
            {
                _explicitlyHiddenToTray = true;
                SaveWindowBounds();
                Hide();
                NativeWindowHelper.TrimMemory();
            }
            return;
        }
        base.OnClosing(e);
    }

    // === 吸附与边缘胶囊逻辑 ===

    private void SnapAfterDrag()
    {
        var work = NativeWindowHelper.GetMonitorWorkAreaDip(this);
        const double snapThreshold = 24.0;

        bool snappedEdge = false;
        if (_storage.Settings.EdgeSnapEnabled)
        {
            if (Math.Abs(Left - work.Left) <= snapThreshold)
            {
                Left = work.Left;
                _storage.Settings.DockSide = DockEdgeSide.Left;
                snappedEdge = true;
            }
            else if (Math.Abs((Left + Width) - work.Right) <= snapThreshold)
            {
                Left = work.Right - Width;
                _storage.Settings.DockSide = DockEdgeSide.Right;
                snappedEdge = true;
            }
            else
            {
                _storage.Settings.DockSide = DockEdgeSide.None;
            }

            if (Math.Abs(Top - work.Top) <= 18.0)
                Top = work.Top;
            else if (Math.Abs((Top + Height) - work.Bottom) <= 18.0)
                Top = work.Bottom - Height;
        }

        // 限制上下不越过工作区顶部
        if (Top < work.Top) Top = work.Top;
        if (Top > work.Bottom - 80) Top = work.Bottom - 80;

        if (snappedEdge && _storage.Settings.AutoCollapseOnEdge)
        {
            _storage.Settings.CapsuleTop = Top + Math.Min(40, Math.Max(0, (Height - _capsule.Height) / 3));
            _isAutoCollapseArmed = true;
            _mouseOutsideSince = DateTime.MinValue;
            _edgeWatchTimer.Start();
        }
        else
        {
            _isAutoCollapseArmed = false;
            if (!_isHoverPeeking)
                _edgeWatchTimer.Stop();
        }

        SaveWindowBounds();
    }

    public void CollapseToEdge(bool save = true)
    {
        _edgeWatchTimer.Stop();
        _isHoverPeeking = false;
        _isAutoCollapseArmed = false;
        _explicitlyHiddenToTray = false;
        try { System.Windows.Input.Keyboard.ClearFocus(); } catch { }

        var work = NativeWindowHelper.GetMonitorWorkAreaDip(this);
        if (_storage.Settings.DockSide == DockEdgeSide.None)
        {
            double winCenter = Left + Width / 2.0;
            double workCenter = work.Left + work.Width / 2.0;
            _storage.Settings.DockSide = winCenter < workCenter ? DockEdgeSide.Left : DockEdgeSide.Right;
            Left = _storage.Settings.DockSide == DockEdgeSide.Left ? work.Left : work.Right - Width;
        }

        _storage.Settings.IsCollapsedToEdge = true;
        if (save) _storage.SaveSettings();

        _capsule.PositionOnEdge(work);
        UpdateCapsuleBadge();
        Hide();

        _hiddenForFullscreen = _storage.Settings.AvoidFullscreen && NativeWindowHelper.IsForegroundFullscreen();
        if (!_hiddenForFullscreen)
        {
            _capsule.Show();
            NativeWindowHelper.ApplyPinMode(_capsule, WindowPinMode.TopMost);
        }
        NativeWindowHelper.TrimMemory();
    }

    private void ExpandFromCapsule(bool hoverPeek)
    {
        if (_hasOpenDialog) return;

        _explicitlyHiddenToTray = false;
        if (!hoverPeek) _hiddenForFullscreen = false;

        var work = NativeWindowHelper.GetMonitorWorkAreaDip(_capsule.IsVisible ? _capsule : this);
        var side = _storage.Settings.DockSide == DockEdgeSide.None ? DockEdgeSide.Right : _storage.Settings.DockSide;

        Width = Math.Clamp(Width, MinWidth, Math.Max(MinWidth, work.Width - 10));
        Height = Math.Clamp(Height, MinHeight, Math.Max(MinHeight, work.Height - 10));

        Left = side == DockEdgeSide.Left ? work.Left : work.Right - Width;
        double targetTop = Math.Clamp(_storage.Settings.CapsuleTop - 20, work.Top + 6, Math.Max(work.Top + 6, work.Bottom - Height - 6));
        Top = targetTop;

        _isHoverPeeking = hoverPeek;
        _isAutoCollapseArmed = !hoverPeek && _storage.Settings.AutoCollapseOnEdge;
        _mouseOutsideSince = DateTime.MinValue;

        if (!hoverPeek)
        {
            _storage.Settings.IsCollapsedToEdge = false;
            _storage.SaveSettings();
        }

        _capsule.Hide();
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;

        // 悬停预览时临时置顶以便看清；固定展开则按用户选择的层级模式
        if (hoverPeek)
        {
            NativeWindowHelper.ApplyPinMode(this, WindowPinMode.TopMost);
        }
        else
        {
            NativeWindowHelper.ApplyPinMode(this, _storage.Settings.PinMode);
            if (_storage.Settings.PinMode != WindowPinMode.Desktop)
                Activate();
        }

        if (_isHoverPeeking || _isAutoCollapseArmed)
            _edgeWatchTimer.Start();
    }

    public void ExpandFromAnywhere(bool activate)
    {
        _explicitlyHiddenToTray = false;
        _hiddenForFullscreen = false;

        if (_capsule.IsVisible || _storage.Settings.IsCollapsedToEdge)
        {
            ExpandFromCapsule(hoverPeek: false);
            return;
        }

        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        EnsureVisibleInWorkArea();
        ReapplyEffectivePinMode();
        if (activate && _storage.Settings.PinMode != WindowPinMode.Desktop)
            Activate();

        _mouseOutsideSince = DateTime.MinValue;
        if (_storage.Settings.AutoCollapseOnEdge)
            _edgeWatchTimer.Start();
    }

    public void ToggleShowOrCollapse()
    {
        if (_capsule.IsVisible)
        {
            ExpandFromCapsule(hoverPeek: false);
        }
        else if (IsVisible)
        {
            if (_storage.Settings.DockSide != DockEdgeSide.None && _storage.Settings.AutoCollapseOnEdge)
                CollapseToEdge();
            else
            {
                _explicitlyHiddenToTray = true;
                SaveWindowBounds();
                Hide();
                NativeWindowHelper.TrimMemory();
            }
        }
        else
        {
            ExpandFromAnywhere(activate: true);
        }
    }

    private bool IsNearScreenEdge(Rect work)
    {
        const double threshold = 32.0;
        bool onLeft = Math.Abs(Left - work.Left) <= threshold;
        bool onRight = Math.Abs((Left + Width) - work.Right) <= threshold;
        return onLeft || onRight;
    }

    private void EdgeWatchTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsVisible || _hasOpenDialog || _isResizing) return;

        var work = NativeWindowHelper.GetMonitorWorkAreaDip(this);
        bool isDocked = (_storage.Settings.DockSide != DockEdgeSide.None) || IsNearScreenEdge(work);

        // 如果既不在悬停预览中，也没有靠边吸附（或者未开启贴边自动收起），则无需收缩
        if (!_isHoverPeeking && (!isDocked || !_storage.Settings.AutoCollapseOnEdge))
        {
            _mouseOutsideSince = DateTime.MinValue;
            return;
        }

        var pt = NativeWindowHelper.GetCursorDip(this);
        // 精准判断鼠标是否离开窗口范围（保留 2px 微小容差，防次像素抖动）
        var winRect = new Rect(Left - 2, Top - 2, Width + 4, Height + 4);

        if (winRect.Contains(pt))
        {
            _mouseOutsideSince = DateTime.MinValue;
            return;
        }

        if (_mouseOutsideSince == DateTime.MinValue)
        {
            _mouseOutsideSince = DateTime.Now;
            return;
        }

        // 悬停预览 120ms，展开常驻 180ms，实现“鼠标一离开立刻自动缩小吸附”
        double delayMs = _isHoverPeeking ? 120 : 180;
        if ((DateTime.Now - _mouseOutsideSince).TotalMilliseconds >= delayMs)
        {
            CollapseToEdge();
        }
    }

    private void FullscreenTimer_Tick(object? sender, EventArgs e)
    {
        // 1. 全屏避让判定与恢复
        if (!_storage.Settings.AvoidFullscreen)
        {
            if (_hiddenForFullscreen)
            {
                _hiddenForFullscreen = false;
                RestoreAfterFullscreenExit();
            }
        }
        else if (!_hasOpenDialog && !_capsule.IsContextMenuOpen)
        {
            bool isFs = NativeWindowHelper.IsForegroundFullscreen();
            if (isFs && !_hiddenForFullscreen)
            {
                _hiddenForFullscreen = true;
                if (_capsule.IsVisible) _capsule.Hide();
                if (IsVisible && (_storage.Settings.PinMode == WindowPinMode.TopMost || _isHoverPeeking))
                    NativeWindowHelper.ApplyPinMode(this, WindowPinMode.Normal);
            }
            else if (!isFs && _hiddenForFullscreen)
            {
                _hiddenForFullscreen = false;
                RestoreAfterFullscreenExit();
            }
        }

        // 2. 若未主动隐藏到托盘且未处于全屏避让，防止主面板与边缘胶囊双双意外丢失
        if (!_explicitlyHiddenToTray && !_hiddenForFullscreen && !IsVisible && !_capsule.IsVisible)
        {
            if (_storage.Settings.IsCollapsedToEdge)
            {
                _capsule.PositionOnEdge(NativeWindowHelper.GetMonitorWorkAreaDip(_capsule));
                _capsule.Show();
                NativeWindowHelper.ApplyPinMode(_capsule, WindowPinMode.TopMost);
            }
            else
            {
                Show();
                EnsureVisibleInWorkArea();
                ReapplyEffectivePinMode();
            }
            return;
        }

        // 3. 置顶 Z 序自愈与屏幕边界保底：防止新开软件/弹框后脱离置顶或因缩放变动跑出屏幕
        if (!_hiddenForFullscreen && !_hasOpenDialog && !_capsule.IsContextMenuOpen)
        {
            if (_capsule.IsVisible)
            {
                var capWork = NativeWindowHelper.GetMonitorWorkAreaDip(_capsule);
                double expectedLeft = _storage.Settings.DockSide == DockEdgeSide.Left
                    ? capWork.Left
                    : capWork.Right - _capsule.Width;
                if (Math.Abs(_capsule.Left - expectedLeft) > 2.0 ||
                    _capsule.Top < capWork.Top ||
                    _capsule.Top > capWork.Bottom - _capsule.Height)
                {
                    _capsule.PositionOnEdge(capWork);
                }
                NativeWindowHelper.EnsureTopMostIfNeeded(_capsule);
            }
            else if (IsVisible && (_storage.Settings.PinMode == WindowPinMode.TopMost || _isHoverPeeking))
            {
                NativeWindowHelper.EnsureTopMostIfNeeded(this);
            }
        }
    }

    private void RestoreAfterFullscreenExit()
    {
        if (IsVisible)
        {
            ReapplyEffectivePinMode();
        }
        else if (_storage.Settings.IsCollapsedToEdge && !_explicitlyHiddenToTray)
        {
            _capsule.PositionOnEdge(NativeWindowHelper.GetMonitorWorkAreaDip(_capsule));
            _capsule.Show();
            NativeWindowHelper.ApplyPinMode(_capsule, WindowPinMode.TopMost);
        }
    }

    public void SetPinMode(WindowPinMode mode)
    {
        _storage.Settings.PinMode = mode;
        _storage.SaveSettings();
        ApplyPinModeVisuals();
        if (!_isHoverPeeking)
            NativeWindowHelper.ApplyPinMode(this, mode);
        _app.RebuildTrayMenu();
    }

    public void ReapplyPinMode()
    {
        NativeWindowHelper.ApplyPinMode(this, _storage.Settings.PinMode);
    }

    private void ReapplyEffectivePinMode()
    {
        if (_isHoverPeeking)
            NativeWindowHelper.ApplyPinMode(this, WindowPinMode.TopMost);
        else
            ReapplyPinMode();
    }

    public void ApplySettingsLive()
    {
        ApplyOpacity();
        ShowInTaskbar = _storage.Settings.ShowInTaskbar;
        NativeWindowHelper.ApplyToolWindowStyle(this, !_storage.Settings.ShowInTaskbar);
        NativeWindowHelper.SetExcludeFromCapture(this, _storage.Settings.HideFromCapture);
        NativeWindowHelper.SetExcludeFromCapture(_capsule, _storage.Settings.HideFromCapture);
        _capsule.ApplySideVisuals();
        UpdateQuickAddBadges();
        RefreshList();
    }

    public void OnThemeChanged()
    {
        UpdateQuickAddBadges();
        _capsule.ApplySideVisuals();
        UpdateCapsuleBadge();
        RefreshList();
    }

    public void ApplyLanguage()
    {
        Title = L10n.T("AppTitle");
        HeaderTitleText.Text = L10n.T("HeaderTitle");
        PinModeBtn.ToolTip = L10n.T("TipPinMode");
        EdgeDockBtn.ToolTip = L10n.T("TipEdgeDock");
        ThemeBtn.ToolTip = L10n.T("TipTheme");
        SettingsBtn.ToolTip = L10n.T("TipSettings");
        HideToTrayBtn.ToolTip = L10n.T("TipHideTray");

        PrevDayBtn.ToolTip = L10n.T("TipPrevDay");
        NextDayBtn.ToolTip = L10n.T("TipNextDay");
        CalendarBtn.ToolTip = L10n.T("TipCalendar");
        DateLabel.ToolTip = L10n.T("TipJumpToday");
        TodayPillBtn.Content = L10n.T("BtnToday");
        RolloverMoveBtn.Content = L10n.T("BtnMoveToday");

        QuickAddPlaceholder.Text = L10n.T("QuickAddPlaceholder");
        QuickAddBtn.Content = L10n.T("BtnAdd");
        QuickPriorityBtn.ToolTip = L10n.T("TipQuickPri");
        QuickReminderBtn.ToolTip = L10n.T("TipQuickRemind");
        QuickMilestoneBtn.ToolTip = L10n.T("TipQuickMilestone");
        QuickRepeatBtn.ToolTip = L10n.T("TipQuickRepeat");

        FilterAllBtn.Content = L10n.T("FilterAll");
        FilterActiveBtn.Content = L10n.T("FilterActive");
        FilterDoneBtn.Content = L10n.T("FilterDone");
        ClearCompletedBtn.Content = L10n.T("ClearCompleted");
        ResizeThumb.ToolTip = L10n.T("TipResize");

        ApplyPinModeVisuals();
        UpdateQuickAddBadges();
        _capsule.ApplyLanguage();
        RefreshList();
    }

    private void ApplyPinModeVisuals()
    {
        switch (_storage.Settings.PinMode)
        {
            case WindowPinMode.Desktop:
                PinModeBtn.Content = "🖥️";
                ModeBadgeText.Text = L10n.T("ModeDesktop");
                break;
            case WindowPinMode.TopMost:
                PinModeBtn.Content = "📌";
                ModeBadgeText.Text = L10n.T("ModeTopMost");
                break;
            default:
                PinModeBtn.Content = "📄";
                ModeBadgeText.Text = L10n.T("ModeNormal");
                break;
        }
    }

    private void ApplyOpacity()
    {
        RootCard.Opacity = Math.Clamp(_storage.Settings.WindowOpacity, 0.70, 1.0);
    }

    private void RunAutoRolloverIfEnabled()
    {
        if (!_storage.Settings.AutoRolloverNextDay) return;
        string todayStr = DateTime.Today.ToString("yyyy-MM-dd");
        _storage.RolloverPastUnfinishedToDate(
            todayStr,
            _storage.Settings.RolloverReminderEnabled,
            _storage.Settings.RolloverReminderTime);
    }

    public void RefreshForNewDay()
    {
        _currentDate = DateTime.Today;
        RunAutoRolloverIfEnabled();
        RefreshList();
    }

    private void UpdateCapsuleBadge()
    {
        string todayStr = DateTime.Today.ToString("yyyy-MM-dd");
        var todayItems = _storage.GetItemsForDate(todayStr);
        int total = todayItems.Count;
        int pending = todayItems.Count(x => !x.IsCompleted);
        _capsule.UpdatePendingCount(pending, total);
    }

    public void RefreshList()
    {
        string dateStr = _currentDate.ToString("yyyy-MM-dd");
        string todayStr = DateTime.Today.ToString("yyyy-MM-dd");
        bool isToday = dateStr == todayStr;

        string weekDay = L10n.GetWeekday(_currentDate.DayOfWeek);
        string suffix = isToday ? L10n.T("SuffixToday")
            : _currentDate == DateTime.Today.AddDays(-1) ? L10n.T("SuffixYesterday")
            : _currentDate == DateTime.Today.AddDays(1) ? L10n.T("SuffixTomorrow")
            : string.Empty;
        DateLabel.Text = $"{_currentDate:yyyy-MM-dd} {weekDay}{suffix}";
        TodayPillBtn.Visibility = isToday ? Visibility.Collapsed : Visibility.Visible;

        if (isToday)
        {
            int pastUnfinished = _storage.CountPastUnfinished(todayStr);
            if (pastUnfinished > 0)
            {
                RolloverText.Text = L10n.TF("RolloverText", pastUnfinished);
                RolloverBanner.Visibility = Visibility.Visible;
            }
            else
            {
                RolloverBanner.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            RolloverBanner.Visibility = Visibility.Collapsed;
        }

        var allItems = _storage.GetItemsForDate(dateStr);
        int total = allItems.Count;
        int done = allItems.Count(x => x.IsCompleted);

        ProgressText.Text = total == 0 ? "0 / 0" : $"{done} / {total} ({(int)Math.Round(done * 100.0 / total)}%)";
        ProgressFill.Background = (total > 0 && done == total)
            ? (System.Windows.Media.Brush)FindResource("SuccessBrush")
            : (System.Windows.Media.Brush)FindResource("AccentBrush");
        ProgressFill.Tag = total == 0 ? 0.0 : (double)done / total;
        UpdateProgressWidth();

        ClearCompletedBtn.Visibility = done > 0 ? Visibility.Visible : Visibility.Collapsed;

        IEnumerable<PlanItem> filtered = _filterMode switch
        {
            "active" => allItems.Where(x => !x.IsCompleted),
            "done" => allItems.Where(x => x.IsCompleted),
            _ => allItems
        };
        var visibleList = filtered.ToList();

        UpdateFilterButtons();
        RenderTaskCards(visibleList);

        if (isToday)
            _capsule.UpdatePendingCount(total - done, total);
        else
            UpdateCapsuleBadge();
    }

    private void UpdateProgressWidth()
    {
        if (ProgressFill.Parent is Border track && track.ActualWidth > 0)
        {
            double ratio = ProgressFill.Tag is double d ? d : 0.0;
            ProgressFill.Width = Math.Max(0, track.ActualWidth * Math.Clamp(ratio, 0, 1));
        }
    }

    private void UpdateFilterButtons()
    {
        void StyleBtn(System.Windows.Controls.Button btn, bool active)
        {
            btn.Background = (System.Windows.Media.Brush)FindResource(active ? "AccentSoftBrush" : "SurfaceBgBrush");
            btn.Foreground = (System.Windows.Media.Brush)FindResource(active ? "AccentBrush" : "TextSecondaryBrush");
        }
        StyleBtn(FilterAllBtn, _filterMode == "all");
        StyleBtn(FilterActiveBtn, _filterMode == "active");
        StyleBtn(FilterDoneBtn, _filterMode == "done");
    }

    private void RenderTaskCards(List<PlanItem> items)
    {
        TaskListPanel.Children.Clear();

        if (items.Count == 0)
        {
            EmptyStatePanel.Visibility = Visibility.Visible;
            EmptyStateText.Text = _filterMode switch
            {
                "active" => L10n.T("EmptyActive"),
                "done" => L10n.T("EmptyDone"),
                _ => L10n.T("EmptyAll")
            };
            return;
        }

        EmptyStatePanel.Visibility = Visibility.Collapsed;

        for (int i = 0; i < items.Count; i++)
        {
            TaskListPanel.Children.Add(BuildTaskCard(items[i], i == 0, i == items.Count - 1));
        }
    }

    private UIElement BuildTaskCard(PlanItem item, bool isFirst, bool isLast)
    {
        double fontScale = Math.Clamp(_storage.Settings.FontScale, 0.85, 1.25);

        bool isUrgentMilestoneToday = item.HasMilestone && !item.IsCompleted && (item.DaysUntilMilestone ?? 1) <= 0;
        var card = new Border
        {
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(8, 8, 6, 8),
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(isUrgentMilestoneToday ? 1.5 : 1),
            SnapsToDevicePixels = true
        };
        card.SetResourceReference(Border.BackgroundProperty, "ItemBgBrush");
        if (isUrgentMilestoneToday)
        {
            card.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
        }
        else
        {
            card.SetResourceReference(Border.BorderBrushProperty, "BorderBrushKey");
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var pBar = new Border
        {
            Width = 3,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 2, 2, 2),
            Background = item.EffectivePriority switch
            {
                TaskPriority.Urgent => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444")),
                TaskPriority.Important => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F59E0B")),
                _ => System.Windows.Media.Brushes.Transparent
            }
        };
        Grid.SetColumn(pBar, 0);
        grid.Children.Add(pBar);

        var cb = new System.Windows.Controls.CheckBox
        {
            IsChecked = item.IsCompleted,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(4, 1, 8, 0),
            Style = (Style)FindResource("TaskCheckBoxStyle"),
            ToolTip = item.IsCompleted ? L10n.T("TipUncheck") : L10n.T("TipCheckDone")
        };
        cb.Click += (_, _) =>
        {
            _storage.ToggleComplete(item.Id, cb.IsChecked == true);
            RefreshList();
        };
        Grid.SetColumn(cb, 1);
        grid.Children.Add(cb);

        var contentStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Background = System.Windows.Media.Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        contentStack.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
                OpenEditDialog(item);
        };

        var titleText = new TextBlock
        {
            Text = item.Title,
            FontSize = 13.2 * fontScale,
            FontWeight = isUrgentMilestoneToday ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            TextDecorations = item.IsCompleted ? TextDecorations.Strikethrough : null
        };
        titleText.SetResourceReference(TextBlock.ForegroundProperty,
            item.IsCompleted ? "TextCompletedBrush" : "TextPrimaryBrush");
        contentStack.Children.Add(titleText);

        if (item.HasMilestone || item.HasReminder || item.IsDailyRepeat || !string.IsNullOrWhiteSpace(item.Note))
        {
            var metaPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };

            if (item.HasMilestone && item.DaysUntilMilestone.HasValue)
            {
                int dLeft = item.DaysUntilMilestone.Value;
                var msPill = new Border
                {
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(5, 1.5, 5, 1.5),
                    Margin = new Thickness(0, 0, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var msText = new TextBlock
                {
                    Text = item.DisplayMilestoneBadgeText,
                    FontSize = 10.6 * fontScale,
                    FontWeight = dLeft <= 1 ? FontWeights.Bold : FontWeights.SemiBold
                };

                if (dLeft <= 0)
                {
                    msPill.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEE2E2"));
                    msText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"));
                }
                else if (dLeft == 1)
                {
                    msPill.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFEDD5"));
                    msText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EA580C"));
                }
                else if (dLeft == 3)
                {
                    msPill.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEF3C7"));
                    msText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D97706"));
                }
                else
                {
                    msPill.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
                    msText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                }

                msPill.Child = msText;
                metaPanel.Children.Add(msPill);
            }

            if (item.HasReminder)
            {
                var remText = new TextBlock
                {
                    Text = item.DisplayReminderText,
                    FontSize = 11 * fontScale,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 7, 2)
                };
                remText.SetResourceReference(TextBlock.ForegroundProperty,
                    item.IsReminderOverdue ? "DangerBrush" : "AccentBrush");
                metaPanel.Children.Add(remText);
            }

            if (item.IsDailyRepeat)
            {
                var repText = new TextBlock
                {
                    Text = L10n.T("DailyBadge"),
                    FontSize = 11 * fontScale,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 7, 2)
                };
                repText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                metaPanel.Children.Add(repText);
            }

            if (!string.IsNullOrWhiteSpace(item.Note))
            {
                var noteText = new TextBlock
                {
                    Text = item.Note.Replace("\r", "").Replace("\n", " "),
                    FontSize = 11 * fontScale,
                    MaxWidth = 235,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = item.Note
                };
                noteText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                metaPanel.Children.Add(noteText);
            }

            contentStack.Children.Add(metaPanel);
        }

        Grid.SetColumn(contentStack, 2);
        grid.Children.Add(contentStack);

        var actions = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.0,
            Margin = new Thickness(4, 0, 0, 0)
        };

        var editBtn = new System.Windows.Controls.Button
        {
            Content = "✎",
            Width = 24,
            Height = 24,
            FontSize = 12,
            ToolTip = L10n.T("TipEdit"),
            Style = (Style)FindResource("IconBtnStyle")
        };
        editBtn.Click += (_, _) => OpenEditDialog(item);
        actions.Children.Add(editBtn);

        var delBtn = new System.Windows.Controls.Button
        {
            Content = "✕",
            Width = 24,
            Height = 24,
            FontSize = 11.5,
            ToolTip = L10n.T("TipDelete"),
            Style = (Style)FindResource("IconBtnStyle")
        };
        delBtn.Click += (_, _) =>
        {
            _storage.DeleteItem(item.Id);
            RefreshList();
        };
        actions.Children.Add(delBtn);

        Grid.SetColumn(actions, 3);
        grid.Children.Add(actions);

        card.Child = grid;

        card.MouseEnter += (_, _) =>
        {
            card.SetResourceReference(Border.BackgroundProperty, "ItemHoverBgBrush");
            actions.Opacity = 1.0;
        };
        card.MouseLeave += (_, _) =>
        {
            card.SetResourceReference(Border.BackgroundProperty, "ItemBgBrush");
            actions.Opacity = 0.0;
        };

        var ctx = new ContextMenu();
        ctx.Opened += (_, _) => _hasOpenDialog = true;
        ctx.Closed += (_, _) => { _hasOpenDialog = false; _mouseOutsideSince = DateTime.MinValue; };
        var mEdit = new MenuItem { Header = L10n.T("CtxEdit") };
        mEdit.Click += (_, _) => OpenEditDialog(item);
        ctx.Items.Add(mEdit);

        var mToggle = new MenuItem { Header = item.IsCompleted ? L10n.T("CtxMarkUndone") : L10n.T("CtxMarkDone") };
        mToggle.Click += (_, _) => { _storage.ToggleComplete(item.Id, !item.IsCompleted); RefreshList(); };
        ctx.Items.Add(mToggle);

        if (!string.IsNullOrEmpty(item.MilestoneGroupId))
        {
            var mCompleteAll = new MenuItem { Header = L10n.T("CtxCompleteMilestoneAll") };
            mCompleteAll.Click += (_, _) =>
            {
                _storage.ToggleMilestoneGroupComplete(item.MilestoneGroupId, !item.IsCompleted);
                RefreshList();
            };
            ctx.Items.Add(mCompleteAll);
        }

        if (!isFirst)
        {
            var mUp = new MenuItem { Header = L10n.T("CtxMoveUp") };
            mUp.Click += (_, _) => { _storage.MoveItem(item.Date, item.Id, -1); RefreshList(); };
            ctx.Items.Add(mUp);
        }
        if (!isLast)
        {
            var mDown = new MenuItem { Header = L10n.T("CtxMoveDown") };
            mDown.Click += (_, _) => { _storage.MoveItem(item.Date, item.Id, 1); RefreshList(); };
            ctx.Items.Add(mDown);
        }

        ctx.Items.Add(new Separator());
        var mDel = new MenuItem { Header = L10n.T("CtxDelete") };
        mDel.Click += (_, _) => { _storage.DeleteItem(item.Id); RefreshList(); };
        ctx.Items.Add(mDel);

        if (!string.IsNullOrEmpty(item.MilestoneGroupId))
        {
            var mDelAll = new MenuItem { Header = L10n.T("CtxDeleteMilestoneAll") };
            mDelAll.Click += (_, _) =>
            {
                _storage.DeleteMilestoneGroup(item.MilestoneGroupId);
                RefreshList();
            };
            ctx.Items.Add(mDelAll);
        }

        card.ContextMenu = ctx;
        return card;
    }

    private void OpenEditDialog(PlanItem item)
    {
        _hasOpenDialog = true;
        try
        {
            var dlg = new EditTaskDialog(item)
            {
                Owner = this,
                Icon = App.AppWindowIcon,
                Topmost = _storage.Settings.PinMode == WindowPinMode.TopMost || _isHoverPeeking
            };
            NativeWindowHelper.SetExcludeFromCapture(dlg, _storage.Settings.HideFromCapture);
            if (dlg.ShowDialog() == true)
            {
                _storage.UpdateItem(dlg.ResultItem);
                RefreshList();
            }
        }
        finally
        {
            _hasOpenDialog = false;
            _mouseOutsideSince = DateTime.MinValue;
            if (IsVisible) ReapplyEffectivePinMode();
        }
    }

    private void OpenCalendar_Click(object sender, RoutedEventArgs e) => OpenCalendarWindow();

    private void OpenCalendarWindow()
    {
        _hasOpenDialog = true;
        try
        {
            if (!IsVisible) ExpandFromCapsule(hoverPeek: false);
            var cal = new CalendarOverviewWindow(
                _storage,
                _currentDate,
                pickedDate =>
                {
                    _currentDate = pickedDate.Date;
                    RefreshList();
                },
                RefreshList)
            {
                Owner = this,
                Icon = App.AppWindowIcon,
                Topmost = _storage.Settings.PinMode == WindowPinMode.TopMost || _isHoverPeeking
            };
            NativeWindowHelper.SetExcludeFromCapture(cal, _storage.Settings.HideFromCapture);
            cal.ShowDialog();
        }
        finally
        {
            _hasOpenDialog = false;
            _mouseOutsideSince = DateTime.MinValue;
            if (IsVisible) ReapplyEffectivePinMode();
        }
    }

    private void OpenSettingsWindow()
    {
        _hasOpenDialog = true;
        try
        {
            if (!IsVisible) ExpandFromCapsule(hoverPeek: false);
            var sw = new SettingsWindow(_app, this, _storage)
            {
                Owner = this,
                Icon = App.AppWindowIcon,
                Topmost = _storage.Settings.PinMode == WindowPinMode.TopMost || _isHoverPeeking
            };
            NativeWindowHelper.SetExcludeFromCapture(sw, _storage.Settings.HideFromCapture);
            sw.ShowDialog();
        }
        finally
        {
            _hasOpenDialog = false;
            _mouseOutsideSince = DateTime.MinValue;
            if (IsVisible) ReapplyEffectivePinMode();
        }
    }

    // === Quick Add ===
    private void QuickAddBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        QuickAddPlaceholder.Visibility = string.IsNullOrEmpty(QuickAddBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void QuickAddBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitQuickAdd();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            QuickAddBox.Clear();
            ResetQuickAddMeta();
            e.Handled = true;
        }
    }

    private void QuickAddBtn_Click(object sender, RoutedEventArgs e) => CommitQuickAdd();

    private void CommitQuickAdd()
    {
        string raw = QuickAddBox.Text.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            QuickAddBox.Focus();
            return;
        }

        string title = raw;
        string? reminder = _quickReminderTime;
        string? milestoneDate = _quickMilestoneDate;

        // 识别标题内联节点日期（如 @11-28、节点11-28、截止2026-11-28）
        var msMatch = InlineMilestoneRegex.Match(title);
        if (msMatch.Success)
        {
            int year = string.IsNullOrEmpty(msMatch.Groups[2].Value)
                ? _currentDate.Year
                : int.Parse(msMatch.Groups[2].Value);
            int month = int.Parse(msMatch.Groups[3].Value);
            int day = int.Parse(msMatch.Groups[4].Value);
            int maxDay = DateTime.DaysInMonth(year, month);
            if (day >= 1 && day <= maxDay)
            {
                var parsedMs = new DateTime(year, month, day);
                if (string.IsNullOrEmpty(msMatch.Groups[2].Value) && parsedMs.Date < _currentDate.Date)
                {
                    parsedMs = parsedMs.AddYears(1);
                }
                milestoneDate = parsedMs.ToString("yyyy-MM-dd");
                string strippedMs = InlineMilestoneRegex.Replace(title, " ", 1).Trim();
                if (!string.IsNullOrEmpty(strippedMs))
                    title = strippedMs;
            }
        }

        // 优先识别标题内的 HH:mm 时间；否则若未手动关闭，则采用默认提醒时间
        var m = InlineTimeRegex.Match(title);
        if (m.Success)
        {
            int h = int.Parse(m.Groups[2].Value);
            int min = int.Parse(m.Groups[3].Value);
            reminder = $"{h:D2}:{min:D2}";
            string stripped = InlineTimeRegex.Replace(title, " ", 1).Trim();
            if (!string.IsNullOrEmpty(stripped))
                title = stripped;
        }
        else if (reminder == null && _storage.Settings.DefaultReminderEnabled)
        {
            reminder = _storage.Settings.DefaultReminderTime;
        }
        else if (reminder == "")
        {
            reminder = null;
        }

        _storage.AddItem(
            _currentDate.ToString("yyyy-MM-dd"),
            title,
            string.Empty,
            _quickPriority,
            reminder,
            _quickDailyRepeat,
            milestoneDate);

        QuickAddBox.Clear();
        ResetQuickAddMeta();
        if (_filterMode == "done") _filterMode = "all";
        RefreshList();
        QuickAddBox.Focus();
    }

    private void ResetQuickAddMeta()
    {
        _quickPriority = TaskPriority.Normal;
        _quickReminderTime = null;
        _quickDailyRepeat = false;
        _quickMilestoneDate = null;
        UpdateQuickAddBadges();
    }

    private void UpdateQuickAddBadges()
    {
        QuickPriorityBtn.Content = _quickPriority switch
        {
            TaskPriority.Important => L10n.T("PriImportant"),
            TaskPriority.Urgent => L10n.T("PriUrgent"),
            _ => L10n.T("PriNormal")
        };
        QuickPriorityBtn.Foreground = _quickPriority switch
        {
            TaskPriority.Urgent => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626")),
            TaskPriority.Important => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D97706")),
            _ => (System.Windows.Media.Brush)FindResource("TextSecondaryBrush")
        };

        if (_quickReminderTime == "")
        {
            QuickReminderBtn.Content = L10n.T("BtnRemind");
            QuickReminderBtn.Background = (System.Windows.Media.Brush)FindResource("SurfaceBgBrush");
            QuickReminderBtn.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
        }
        else if (!string.IsNullOrEmpty(_quickReminderTime))
        {
            QuickReminderBtn.Content = $"⏰ {_quickReminderTime}";
            QuickReminderBtn.Background = (System.Windows.Media.Brush)FindResource("AccentSoftBrush");
            QuickReminderBtn.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
        }
        else if (_storage.Settings.DefaultReminderEnabled && !string.IsNullOrWhiteSpace(_storage.Settings.DefaultReminderTime))
        {
            QuickReminderBtn.Content = L10n.TF("RemindDefaultTag", _storage.Settings.DefaultReminderTime);
            QuickReminderBtn.Background = (System.Windows.Media.Brush)FindResource("SurfaceBgBrush");
            QuickReminderBtn.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
        }
        else
        {
            QuickReminderBtn.Content = L10n.T("BtnRemind");
            QuickReminderBtn.Background = (System.Windows.Media.Brush)FindResource("SurfaceBgBrush");
            QuickReminderBtn.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
        }

        if (!string.IsNullOrEmpty(_quickMilestoneDate) &&
            DateTime.TryParseExact(_quickMilestoneDate, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var msDt))
        {
            int dLeft = (msDt.Date - _currentDate.Date).Days;
            string shortMd = msDt.ToString("MM-dd");
            QuickMilestoneBtn.Content = dLeft <= 0
                ? L10n.TF("MilestoneTodayTagFmt", shortMd)
                : L10n.TF("MilestoneTagFmt", shortMd, dLeft);
            QuickMilestoneBtn.Background = (System.Windows.Media.Brush)FindResource("AccentSoftBrush");
            QuickMilestoneBtn.Foreground = dLeft <= 0
                ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"))
                : (System.Windows.Media.Brush)FindResource("AccentBrush");
        }
        else
        {
            QuickMilestoneBtn.Content = L10n.T("BtnMilestone");
            QuickMilestoneBtn.Background = (System.Windows.Media.Brush)FindResource("SurfaceBgBrush");
            QuickMilestoneBtn.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
        }

        QuickRepeatBtn.Content = L10n.T("BtnDaily");
        QuickRepeatBtn.Background = (System.Windows.Media.Brush)FindResource(
            _quickDailyRepeat ? "AccentSoftBrush" : "SurfaceBgBrush");
        QuickRepeatBtn.Foreground = (System.Windows.Media.Brush)FindResource(
            _quickDailyRepeat ? "AccentBrush" : "TextSecondaryBrush");
    }

    private void QuickPriority_Click(object sender, RoutedEventArgs e)
    {
        _quickPriority = (TaskPriority)(((int)_quickPriority + 1) % 3);
        UpdateQuickAddBadges();
    }

    private void QuickRepeat_Click(object sender, RoutedEventArgs e)
    {
        _quickDailyRepeat = !_quickDailyRepeat;
        if (_quickDailyRepeat) _quickMilestoneDate = null;
        UpdateQuickAddBadges();
    }

    private void QuickMilestone_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) => _hasOpenDialog = true;
        menu.Closed += (_, _) => { _hasOpenDialog = false; _mouseOutsideSince = DateTime.MinValue; };

        DateTime baseDate = _currentDate.Date;

        void AddDayOffset(string fmtKey, int days)
        {
            DateTime target = baseDate.AddDays(days);
            var mi = new MenuItem { Header = L10n.TF(fmtKey, target.ToString("MM-dd")) };
            mi.Click += (_, _) =>
            {
                _quickMilestoneDate = target.ToString("yyyy-MM-dd");
                _quickDailyRepeat = false;
                UpdateQuickAddBadges();
                QuickAddBox.Focus();
            };
            menu.Items.Add(mi);
        }

        AddDayOffset("MsTomorrow", 1);
        AddDayOffset("MsIn2Days", 2);
        AddDayOffset("MsIn3Days", 3);
        AddDayOffset("MsIn5Days", 5);
        AddDayOffset("MsIn7Days", 7);
        AddDayOffset("MsIn14Days", 14);

        DateTime monthEnd = new DateTime(baseDate.Year, baseDate.Month, DateTime.DaysInMonth(baseDate.Year, baseDate.Month));
        if (monthEnd > baseDate)
        {
            var miEnd = new MenuItem { Header = L10n.TF("MsMonthEnd", monthEnd.ToString("MM-dd")) };
            miEnd.Click += (_, _) =>
            {
                _quickMilestoneDate = monthEnd.ToString("yyyy-MM-dd");
                _quickDailyRepeat = false;
                UpdateQuickAddBadges();
                QuickAddBox.Focus();
            };
            menu.Items.Add(miEnd);
        }

        menu.Items.Add(new Separator());
        var miPick = new MenuItem { Header = L10n.T("MsPickDate") };
        miPick.Click += (_, _) => ShowMilestoneCalendarPicker();
        menu.Items.Add(miPick);

        if (!string.IsNullOrEmpty(_quickMilestoneDate))
        {
            menu.Items.Add(new Separator());
            var miClear = new MenuItem { Header = L10n.T("MsClear") };
            miClear.Click += (_, _) =>
            {
                _quickMilestoneDate = null;
                UpdateQuickAddBadges();
                QuickAddBox.Focus();
            };
            menu.Items.Add(miClear);
        }

        menu.PlacementTarget = QuickMilestoneBtn;
        menu.IsOpen = true;
    }

    private void ShowMilestoneCalendarPicker()
    {
        _hasOpenDialog = true;
        try
        {
            var pickerWin = new Window
            {
                Title = L10n.T("BtnMilestone"),
                Width = 270,
                SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ShowInTaskbar = false,
                Topmost = true
            };

            var outer = new Border
            {
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22000000")),
                Padding = new Thickness(1.5)
            };
            var inner = new Border
            {
                CornerRadius = new CornerRadius(10.5),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10)
            };
            inner.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
            inner.SetResourceReference(Border.BorderBrushProperty, "BorderBrushKey");

            var stack = new StackPanel();
            var cal = new System.Windows.Controls.Calendar
            {
                DisplayDateStart = _currentDate.Date,
                SelectedDate = !string.IsNullOrEmpty(_quickMilestoneDate) &&
                    DateTime.TryParseExact(_quickMilestoneDate, "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var curMs)
                    ? curMs
                    : _currentDate.Date.AddDays(3),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };
            cal.SelectedDatesChanged += (_, _) =>
            {
                if (cal.SelectedDate.HasValue)
                {
                    _quickMilestoneDate = cal.SelectedDate.Value.ToString("yyyy-MM-dd");
                    _quickDailyRepeat = false;
                    UpdateQuickAddBadges();
                    pickerWin.DialogResult = true;
                    pickerWin.Close();
                }
            };
            stack.Children.Add(cal);

            var cancelBtn = new System.Windows.Controls.Button
            {
                Content = L10n.T("BtnCancel"),
                Margin = new Thickness(0, 6, 0, 0),
                Padding = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Style = (Style)FindResource("SubtleBtnStyle")
            };
            cancelBtn.Click += (_, _) => pickerWin.Close();
            stack.Children.Add(cancelBtn);

            inner.Child = stack;
            outer.Child = inner;
            pickerWin.Content = outer;
            pickerWin.KeyDown += (_, ke) =>
            {
                if (ke.Key == Key.Escape) pickerWin.Close();
            };

            pickerWin.ShowDialog();
            QuickAddBox.Focus();
        }
        finally
        {
            _hasOpenDialog = false;
            _mouseOutsideSince = DateTime.MinValue;
            if (IsVisible) ReapplyEffectivePinMode();
        }
    }

    private void QuickReminder_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) => _hasOpenDialog = true;
        menu.Closed += (_, _) => { _hasOpenDialog = false; _mouseOutsideSince = DateTime.MinValue; };

        void AddPreset(string label, Func<string?> getter)
        {
            var mi = new MenuItem { Header = label };
            mi.Click += (_, _) => { _quickReminderTime = getter(); UpdateQuickAddBadges(); };
            menu.Items.Add(mi);
        }

        AddPreset(L10n.T("In15Min"), () => DateTime.Now.AddMinutes(15).ToString("HH:mm"));
        AddPreset(L10n.T("In30Min"), () => DateTime.Now.AddMinutes(30).ToString("HH:mm"));
        AddPreset(L10n.T("In1Hour"), () => DateTime.Now.AddHours(1).ToString("HH:mm"));
        menu.Items.Add(new Separator());
        AddPreset(L10n.T("At0900"), () => "09:00");
        AddPreset(L10n.T("At1030"), () => "10:30");
        AddPreset(L10n.T("At1400"), () => "14:00");
        AddPreset(L10n.T("At1600"), () => "16:00");
        AddPreset(L10n.T("At1800"), () => "18:00");
        AddPreset(L10n.T("At2100"), () => "21:00");
        menu.Items.Add(new Separator());
        AddPreset(L10n.T("NoRemindOnce"), () => "");
        if (_quickReminderTime != null)
            AddPreset(L10n.T("ClearReminder"), () => null);

        menu.PlacementTarget = QuickReminderBtn;
        menu.IsOpen = true;
    }

    // === Header & Controls ===
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (_isHoverPeeking)
            {
                _isHoverPeeking = false;
                _storage.Settings.IsCollapsedToEdge = false;
                ReapplyEffectivePinMode();
            }
            DragMove();
            SnapAfterDrag();
            if (_storage.Settings.PinMode == WindowPinMode.Desktop && !_isHoverPeeking)
                NativeWindowHelper.SendToBottom(this);
        }
    }

    private (double minW, double maxW, double minH, double maxH, Rect work) GetResizeConstraints()
    {
        var work = NativeWindowHelper.GetMonitorWorkAreaDip(this);
        double minW = MinWidth > 0 ? MinWidth : 295;
        double minH = MinHeight > 0 ? MinHeight : 360;
        double maxW = Math.Max(minW, Math.Min(1200, work.Width - 10));
        double maxH = Math.Max(minH, Math.Min(1600, work.Height - 10));
        return (minW, maxW, minH, maxH, work);
    }

    private void OnResizeDragStarted(object sender, DragStartedEventArgs e)
    {
        _isResizing = true;
        _edgeWatchTimer.Stop();
        if (_isHoverPeeking)
        {
            _isHoverPeeking = false;
            _storage.Settings.IsCollapsedToEdge = false;
            _storage.SaveSettings();
            NativeWindowHelper.ApplyPinMode(this, _storage.Settings.PinMode);
        }
    }

    private void OnResizeDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isResizing = false;
        var work = NativeWindowHelper.GetMonitorWorkAreaDip(this);

        if (_storage.Settings.DockSide == DockEdgeSide.Right)
        {
            if (Math.Abs((Left + Width) - work.Right) <= 24.0)
            {
                Left = work.Right - Width;
            }
        }
        else if (_storage.Settings.DockSide == DockEdgeSide.Left)
        {
            if (Math.Abs(Left - work.Left) <= 24.0)
            {
                Left = work.Left;
            }
        }

        SaveWindowBounds();

        if (_storage.Settings.PinMode == WindowPinMode.Desktop && !_isHoverPeeking)
            NativeWindowHelper.SendToBottom(this);

        if (_storage.Settings.AutoCollapseOnEdge)
        {
            _mouseOutsideSince = DateTime.MinValue;
            _edgeWatchTimer.Start();
        }
    }

    private void ResizeThumb_L_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var (minW, maxW, _, _, work) = GetResizeConstraints();
        double targetW = Width - e.HorizontalChange;
        double newW = Math.Clamp(targetW, minW, maxW);
        double deltaW = newW - Width;
        double newLeft = Left - deltaW;

        if (newLeft < work.Left)
        {
            newLeft = work.Left;
            newW = Math.Clamp(Width + (Left - newLeft), minW, maxW);
        }

        Left = newLeft;
        Width = newW;
        SaveBoundsDeferred();
    }

    private void ResizeThumb_R_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var (minW, maxW, _, _, work) = GetResizeConstraints();
        double targetW = Width + e.HorizontalChange;
        double newW = Math.Clamp(targetW, minW, maxW);
        if (Left + newW > work.Right)
        {
            newW = Math.Max(minW, work.Right - Left);
        }
        Width = newW;
        SaveBoundsDeferred();
    }

    private void ResizeThumb_T_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var (_, _, minH, maxH, work) = GetResizeConstraints();
        double targetH = Height - e.VerticalChange;
        double newH = Math.Clamp(targetH, minH, maxH);
        double deltaH = newH - Height;
        double newTop = Top - deltaH;

        if (newTop < work.Top)
        {
            newTop = work.Top;
            newH = Math.Clamp(Height + (Top - newTop), minH, maxH);
        }

        Top = newTop;
        Height = newH;
        SaveBoundsDeferred();
    }

    private void ResizeThumb_B_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var (_, _, minH, maxH, work) = GetResizeConstraints();
        double targetH = Height + e.VerticalChange;
        double newH = Math.Clamp(targetH, minH, maxH);
        if (Top + newH > work.Bottom)
        {
            newH = Math.Max(minH, work.Bottom - Top);
        }
        Height = newH;
        SaveBoundsDeferred();
    }

    private void ResizeThumb_TL_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ResizeThumb_L_DragDelta(sender, e);
        ResizeThumb_T_DragDelta(sender, e);
    }

    private void ResizeThumb_TR_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ResizeThumb_R_DragDelta(sender, e);
        ResizeThumb_T_DragDelta(sender, e);
    }

    private void ResizeThumb_BL_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ResizeThumb_L_DragDelta(sender, e);
        ResizeThumb_B_DragDelta(sender, e);
    }

    private void ResizeThumb_BR_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ResizeThumb_R_DragDelta(sender, e);
        ResizeThumb_B_DragDelta(sender, e);
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e) => ResizeThumb_BR_DragDelta(sender, e);
    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e) => OnResizeDragCompleted(sender, e);

    private void CyclePinMode_Click(object sender, RoutedEventArgs e)
    {
        var next = _storage.Settings.PinMode switch
        {
            WindowPinMode.Desktop => WindowPinMode.TopMost,
            WindowPinMode.TopMost => WindowPinMode.Normal,
            _ => WindowPinMode.Desktop
        };
        SetPinMode(next);
    }

    private void CollapseToEdge_Click(object sender, RoutedEventArgs e) => CollapseToEdge();

    private void CycleTheme_Click(object sender, RoutedEventArgs e)
    {
        var next = (AppThemeMode)(((int)_storage.Settings.Theme + 1) % 5);
        _storage.Settings.Theme = next;
        _storage.SaveSettings();
        _app.ApplyTheme(next);
        OnThemeChanged();
    }

    private void HideToTray_Click(object sender, RoutedEventArgs e)
    {
        _explicitlyHiddenToTray = true;
        SaveWindowBounds();
        _capsule.Hide();
        Hide();
        NativeWindowHelper.TrimMemory();
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e) => OpenSettingsWindow();

    // === Date & Filter ===
    private void PrevDay_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = _currentDate.AddDays(-1);
        RefreshList();
    }

    private void NextDay_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = _currentDate.AddDays(1);
        RefreshList();
    }

    private void JumpToday_Click(object sender, MouseButtonEventArgs e)
    {
        _currentDate = DateTime.Today;
        RefreshList();
    }

    private void JumpTodayBtn_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = DateTime.Today;
        RefreshList();
    }

    private void Rollover_Click(object sender, RoutedEventArgs e)
    {
        _storage.RolloverPastUnfinishedToDate(
            DateTime.Today.ToString("yyyy-MM-dd"),
            _storage.Settings.RolloverReminderEnabled,
            _storage.Settings.RolloverReminderTime);
        RefreshList();
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string tag)
        {
            _filterMode = tag;
            RefreshList();
        }
    }

    private void ClearCompleted_Click(object sender, RoutedEventArgs e)
    {
        _storage.ClearCompletedForDate(_currentDate.ToString("yyyy-MM-dd"));
        RefreshList();
    }
}
