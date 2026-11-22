using System;
using System.Diagnostics;
using System.Media;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DailyPlanBoard.Helpers;
using DailyPlanBoard.Models;
using DailyPlanBoard.Services;
using DailyPlanBoard.Views;
using Forms = System.Windows.Forms;

namespace DailyPlanBoard;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Local\\DailyPlanBoard_SingleInstance_v1";
    private const string WakeEventName = "Local\\DailyPlanBoard_WakeEvent_v1";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _wakeEvent;
    private Thread? _wakeListenerThread;
    private volatile bool _isExiting;

    public StorageService Storage { get; private set; } = null!;
    public ReminderService Reminder { get; private set; } = null!;
    private MainWindow? _mainWindow;
    private Forms.NotifyIcon? _trayIcon;
    private System.Drawing.Icon? _trayDrawingIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 关闭硬件 D3D 管线，避免加载显卡驱动栈与大量 GPU 线程，大幅降低常驻内存占用
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        base.OnStartup(e);

        _instanceMutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // Signal existing instance to show its window
            try
            {
                using var evt = EventWaitHandle.OpenExisting(WakeEventName);
                evt.Set();
            }
            catch { }
            Shutdown();
            return;
        }

        StartWakeListener();

        DispatcherUnhandledException += (_, args) =>
        {
            LogError("DispatcherUnhandledException", args.Exception);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                LogError("UnhandledException", ex);
        };

        try
        {
            Storage = new StorageService();
            L10n.Current = Storage.Settings.Language;
            ApplyTheme(Storage.Settings.Theme);

            _mainWindow = new MainWindow(this, Storage);
            MainWindow = _mainWindow;
            _mainWindow.Show();

            SetupTrayIcon();

            Reminder = new ReminderService(Storage);
            Reminder.ReminderTriggered += OnReminderTriggered;
            Reminder.DayChanged += () => _mainWindow?.RefreshForNewDay();
            Reminder.Start();

            // 启动渲染完成后温和回收初始化临时内存，降低常驻工作集
            var startupTrim = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            startupTrim.Tick += (_, _) =>
            {
                startupTrim.Stop();
                NativeWindowHelper.TrimMemory();
            };
            startupTrim.Start();
        }
        catch (Exception ex)
        {
            LogError("OnStartup", ex);
            System.Windows.MessageBox.Show($"每日计划看板启动失败：\n{ex.Message}", "每日计划看板", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void LogError(string stage, Exception ex)
    {
        try
        {
            string dir = System.IO.Path.Combine(AppContext.BaseDirectory, "data");
            System.IO.Directory.CreateDirectory(dir);
            string logPath = System.IO.Path.Combine(dir, "error.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{stage}] {ex}\r\n\r\n");
        }
        catch { }
    }

    private void StartWakeListener()
    {
        try
        {
            _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
            _wakeListenerThread = new Thread(() =>
            {
                while (!_isExiting)
                {
                    if (_wakeEvent.WaitOne(500) && !_isExiting)
                    {
                        Dispatcher.BeginInvoke(() => ShowAndBringToFront());
                    }
                }
            })
            {
                IsBackground = true
            };
            _wakeListenerThread.Start();
        }
        catch { }
    }

    public void ApplyTheme(AppThemeMode mode)
    {
        void SetBrush(string key, string hex)
        {
            Resources[key] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        }

        switch (mode)
        {
            case AppThemeMode.WarmPaper:
                SetBrush("CardBgBrush", "#FCF9F2");
                SetBrush("SurfaceBgBrush", "#F4EDE0");
                SetBrush("ItemBgBrush", "#FFFFFF");
                SetBrush("ItemHoverBgBrush", "#FFFDF7");
                SetBrush("InputBgBrush", "#FFFFFF");
                SetBrush("BorderBrushKey", "#E6DEC8");
                SetBrush("TextPrimaryBrush", "#2C2824");
                SetBrush("TextSecondaryBrush", "#78716C");
                SetBrush("TextCompletedBrush", "#A8A29E");
                SetBrush("AccentBrush", "#D97706");
                SetBrush("AccentSoftBrush", "#FEF3C7");
                SetBrush("DangerBrush", "#DC2626");
                SetBrush("SuccessBrush", "#16A34A");
                break;

            case AppThemeMode.CleanLight:
                SetBrush("CardBgBrush", "#F8FAFC");
                SetBrush("SurfaceBgBrush", "#EEF2F6");
                SetBrush("ItemBgBrush", "#FFFFFF");
                SetBrush("ItemHoverBgBrush", "#F1F5F9");
                SetBrush("InputBgBrush", "#FFFFFF");
                SetBrush("BorderBrushKey", "#DCE3EC");
                SetBrush("TextPrimaryBrush", "#1E293B");
                SetBrush("TextSecondaryBrush", "#64748B");
                SetBrush("TextCompletedBrush", "#94A3B8");
                SetBrush("AccentBrush", "#2563EB");
                SetBrush("AccentSoftBrush", "#DBEAFE");
                SetBrush("DangerBrush", "#DC2626");
                SetBrush("SuccessBrush", "#16A34A");
                break;

            case AppThemeMode.Forest:
                SetBrush("CardBgBrush", "#F3F8F4");
                SetBrush("SurfaceBgBrush", "#E4EFE7");
                SetBrush("ItemBgBrush", "#FFFFFF");
                SetBrush("ItemHoverBgBrush", "#ECF6EE");
                SetBrush("InputBgBrush", "#FFFFFF");
                SetBrush("BorderBrushKey", "#CDE0D2");
                SetBrush("TextPrimaryBrush", "#1B3124");
                SetBrush("TextSecondaryBrush", "#567060");
                SetBrush("TextCompletedBrush", "#8FA596");
                SetBrush("AccentBrush", "#15803D");
                SetBrush("AccentSoftBrush", "#DCFCE7");
                SetBrush("DangerBrush", "#DC2626");
                SetBrush("SuccessBrush", "#16A34A");
                break;

            case AppThemeMode.Sunset:
                SetBrush("CardBgBrush", "#FDF5F3");
                SetBrush("SurfaceBgBrush", "#F9E7E3");
                SetBrush("ItemBgBrush", "#FFFFFF");
                SetBrush("ItemHoverBgBrush", "#FFF6F4");
                SetBrush("InputBgBrush", "#FFFFFF");
                SetBrush("BorderBrushKey", "#EED3CD");
                SetBrush("TextPrimaryBrush", "#332220");
                SetBrush("TextSecondaryBrush", "#7D615D");
                SetBrush("TextCompletedBrush", "#AD9692");
                SetBrush("AccentBrush", "#E11D48");
                SetBrush("AccentSoftBrush", "#FFE4E6");
                SetBrush("DangerBrush", "#DC2626");
                SetBrush("SuccessBrush", "#16A34A");
                break;

            case AppThemeMode.DarkSlate:
                SetBrush("CardBgBrush", "#1C2128");
                SetBrush("SurfaceBgBrush", "#262C36");
                SetBrush("ItemBgBrush", "#22272E");
                SetBrush("ItemHoverBgBrush", "#2B323C");
                SetBrush("InputBgBrush", "#22272E");
                SetBrush("BorderBrushKey", "#373E47");
                SetBrush("TextPrimaryBrush", "#E6EDF3");
                SetBrush("TextSecondaryBrush", "#8B949E");
                SetBrush("TextCompletedBrush", "#6E7681");
                SetBrush("AccentBrush", "#60A5FA");
                SetBrush("AccentSoftBrush", "#1E3A5F");
                SetBrush("DangerBrush", "#F87171");
                SetBrush("SuccessBrush", "#4ADE80");
                break;
        }
    }

    public void SetLanguage(AppLanguage lang)
    {
        Storage.Settings.Language = lang;
        Storage.SaveSettings();
        L10n.Current = lang;
        if (_trayIcon != null)
            _trayIcon.Text = L10n.T("AppTitle");
        RebuildTrayMenu();
        _mainWindow?.ApplyLanguage();
    }

    public static ImageSource? AppWindowIcon { get; private set; }

    private void SetupTrayIcon()
    {
        try
        {
            var iconUri = new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
            var streamInfo = GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                using (streamInfo.Stream)
                {
                    _trayDrawingIcon = new System.Drawing.Icon(streamInfo.Stream, 32, 32);
                }
            }
            AppWindowIcon = BitmapFrame.Create(iconUri);
        }
        catch { }

        _trayDrawingIcon ??= IconGenerator.CreateTrayIcon(32);

        if (_mainWindow != null && AppWindowIcon != null)
            _mainWindow.Icon = AppWindowIcon;

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _trayDrawingIcon,
            Text = L10n.T("AppTitle"),
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => Dispatcher.BeginInvoke(ToggleMainWindowVisibility);
        _trayIcon.BalloonTipClicked += (_, _) => Dispatcher.BeginInvoke(ShowAndBringToFront);

        RebuildTrayMenu();
    }

    public void RebuildTrayMenu()
    {
        if (_trayIcon == null) return;

        var menu = new Forms.ContextMenuStrip();

        var toggleShow = new Forms.ToolStripMenuItem(L10n.T("TrayShowHide"), null, (_, _) => Dispatcher.BeginInvoke(ToggleMainWindowVisibility));
        menu.Items.Add(toggleShow);
        var collapseEdge = new Forms.ToolStripMenuItem(L10n.T("TrayCollapseEdge"), null, (_, _) => Dispatcher.BeginInvoke(() => _mainWindow?.CollapseToEdge()));
        menu.Items.Add(collapseEdge);
        menu.Items.Add(new Forms.ToolStripSeparator());

        var desktopItem = new Forms.ToolStripMenuItem(L10n.T("TrayModeDesktop"), null, (_, _) => Dispatcher.BeginInvoke(() => _mainWindow?.SetPinMode(WindowPinMode.Desktop)))
        {
            Checked = Storage.Settings.PinMode == WindowPinMode.Desktop
        };
        var topItem = new Forms.ToolStripMenuItem(L10n.T("TrayModeTopMost"), null, (_, _) => Dispatcher.BeginInvoke(() => _mainWindow?.SetPinMode(WindowPinMode.TopMost)))
        {
            Checked = Storage.Settings.PinMode == WindowPinMode.TopMost
        };
        var normalItem = new Forms.ToolStripMenuItem(L10n.T("TrayModeNormal"), null, (_, _) => Dispatcher.BeginInvoke(() => _mainWindow?.SetPinMode(WindowPinMode.Normal)))
        {
            Checked = Storage.Settings.PinMode == WindowPinMode.Normal
        };
        menu.Items.Add(desktopItem);
        menu.Items.Add(topItem);
        menu.Items.Add(normalItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        var langMenu = new Forms.ToolStripMenuItem(L10n.T("TrayLanguage"));
        langMenu.DropDownItems.Add(new Forms.ToolStripMenuItem("简体中文", null, (_, _) => Dispatcher.BeginInvoke(() => SetLanguage(AppLanguage.Zh))) { Checked = Storage.Settings.Language == AppLanguage.Zh });
        langMenu.DropDownItems.Add(new Forms.ToolStripMenuItem("English", null, (_, _) => Dispatcher.BeginInvoke(() => SetLanguage(AppLanguage.En))) { Checked = Storage.Settings.Language == AppLanguage.En });
        langMenu.DropDownItems.Add(new Forms.ToolStripMenuItem("Français", null, (_, _) => Dispatcher.BeginInvoke(() => SetLanguage(AppLanguage.Fr))) { Checked = Storage.Settings.Language == AppLanguage.Fr });
        menu.Items.Add(langMenu);

        var autoStartItem = new Forms.ToolStripMenuItem(L10n.T("TrayAutoStart"), null, (_, _) => Dispatcher.BeginInvoke(ToggleAutoStart))
        {
            Checked = AutoStartService.IsEnabled()
        };
        menu.Items.Add(autoStartItem);

        var testRemindItem = new Forms.ToolStripMenuItem(L10n.T("TrayTestRemind"), null, (_, _) => Dispatcher.BeginInvoke(TriggerTestReminder));
        menu.Items.Add(testRemindItem);

        var openDataItem = new Forms.ToolStripMenuItem(L10n.T("TrayOpenData"), null, (_, _) => OpenDataFolder());
        menu.Items.Add(openDataItem);

        menu.Items.Add(new Forms.ToolStripSeparator());
        var exitItem = new Forms.ToolStripMenuItem(L10n.T("TrayExit"), null, (_, _) => Dispatcher.BeginInvoke(ExitApplication));
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
    }

    public void ToggleAutoStart()
    {
        bool next = !AutoStartService.IsEnabled();
        if (AutoStartService.SetEnabled(next))
        {
            Storage.Settings.AutoStart = next;
            Storage.SaveSettings();
            RebuildTrayMenu();
        }
    }

    public void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Storage.DataDirectory,
                UseShellExecute = true
            });
        }
        catch { }
    }

    public void ToggleMainWindowVisibility()
    {
        if (_mainWindow == null) return;
        _mainWindow.ToggleShowOrCollapse();
    }

    public void ShowAndBringToFront()
    {
        if (_mainWindow == null) return;
        _mainWindow.ExpandFromAnywhere(activate: true);
    }

    private void OnReminderTriggered(PlanItem item)
    {
        bool isUrgentMilestone = item.HasMilestone && (item.DaysUntilMilestone ?? 1) <= 0;

        if (Storage.Settings.SoundEnabled)
        {
            try
            {
                if (isUrgentMilestone)
                    SystemSounds.Hand.Play();
                else
                    SystemSounds.Exclamation.Play();
            }
            catch { }
        }

        string balloonTitle = L10n.T("BalloonTitle");
        string balloonBody = item.Title;
        var balloonIcon = Forms.ToolTipIcon.Info;

        if (item.HasMilestone && item.DaysUntilMilestone.HasValue)
        {
            int dLeft = item.DaysUntilMilestone.Value;
            if (dLeft == 0)
            {
                balloonTitle = L10n.T("PopupTitleUrgentToday");
                balloonBody = $"{item.Title}\n{L10n.TF("MilestoneNoteDueToday", item.MilestoneDate ?? "")}";
                balloonIcon = Forms.ToolTipIcon.Warning;
            }
            else if (dLeft < 0)
            {
                balloonTitle = L10n.T("PopupTitleOverdue");
                balloonBody = $"{item.Title}\n{L10n.TF("MilestoneNoteOverdue", item.MilestoneDate ?? "", Math.Abs(dLeft))}";
                balloonIcon = Forms.ToolTipIcon.Warning;
            }
            else if (dLeft == 1)
            {
                balloonTitle = L10n.T("PopupTitleT1");
                balloonBody = $"{item.Title}\n{L10n.TF("MilestoneNoteT1", item.MilestoneDate ?? "")}";
                balloonIcon = Forms.ToolTipIcon.Warning;
            }
            else if (dLeft == 3)
            {
                balloonTitle = L10n.T("PopupTitleT3");
                balloonBody = $"{item.Title}\n{L10n.TF("MilestoneNoteT3", item.MilestoneDate ?? "")}";
            }
            else
            {
                balloonTitle = L10n.TF("PopupTitleMilestone", dLeft);
                balloonBody = $"{item.Title} ({item.DisplayMilestoneBadgeText})";
            }
        }

        ShowBalloon(balloonTitle, balloonBody, balloonIcon);

        if (Storage.Settings.WeComBotEnabled && !string.IsNullOrWhiteSpace(Storage.Settings.WeComWebhookOrKey))
        {
            _ = WeComNotifier.SendTaskReminderAsync(Storage.Settings.WeComWebhookOrKey, item);
        }

        var popup = new ReminderPopup(
            item,
            item.Title,
            item.Note,
            id =>
            {
                Storage.ToggleComplete(id, true);
                _mainWindow?.RefreshList();
            },
            (id, mins) =>
            {
                Storage.SnoozeItem(id, mins);
                _mainWindow?.RefreshList();
            });
        popup.Show();
    }

    public void TriggerTestReminder()
    {
        if (Storage.Settings.SoundEnabled)
        {
            try { SystemSounds.Exclamation.Play(); } catch { }
        }

        ShowBalloon(L10n.T("TestBalloonTitle"), L10n.T("TestBalloonBody"));

        var popup = new ReminderPopup(
            null,
            L10n.T("TestPopupTitle"),
            L10n.T("TestPopupNote"),
            null,
            null);
        popup.Show();
    }

    public void ShowBalloon(string title, string text, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Info)
    {
        try
        {
            _trayIcon?.ShowBalloonTip(6000, title, text, icon);
        }
        catch { }
    }

    public void ExitApplication()
    {
        _isExiting = true;
        Reminder?.Dispose();
        if (_mainWindow != null)
        {
            _mainWindow.SaveWindowBounds();
            _mainWindow.ForceClose();
        }
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _trayDrawingIcon?.Dispose();
        _wakeEvent?.Set();
        _wakeEvent?.Dispose();
        try { _instanceMutex?.ReleaseMutex(); } catch { }
        _instanceMutex?.Dispose();
        Shutdown();
    }
}
