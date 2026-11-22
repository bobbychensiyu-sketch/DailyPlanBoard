using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DailyPlanBoard.Models;
using DailyPlanBoard.Services;

namespace DailyPlanBoard.Views;

public partial class SettingsWindow : Window
{
    private readonly App _app;
    private readonly MainWindow _main;
    private readonly StorageService _storage;
    private bool _initializing = true;

    public SettingsWindow(App app, MainWindow main, StorageService storage)
    {
        InitializeComponent();
        _app = app;
        _main = main;
        _storage = storage;

        LoadControlsFromSettings();
        ApplyLanguageTexts();
        _initializing = false;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !HotkeyBox.IsFocused) Close();
        };
    }

    private void LoadControlsFromSettings()
    {
        var s = _storage.Settings;

        PinModeCombo.SelectedIndex = Math.Clamp((int)s.PinMode, 0, 2);
        EdgeSnapCheck.IsChecked = s.EdgeSnapEnabled;
        AutoCollapseCheck.IsChecked = s.AutoCollapseOnEdge;
        HoverExpandCheck.IsChecked = s.HoverExpandEnabled;
        AvoidFullscreenCheck.IsChecked = s.AvoidFullscreen;
        HideFromCaptureCheck.IsChecked = s.HideFromCapture;

        OpacitySlider.Value = Math.Clamp(s.WindowOpacity, 0.70, 1.0);
        FontScaleCombo.SelectedIndex = s.FontScale < 0.96 ? 0 : (s.FontScale > 1.05 ? 2 : 1);

        CompletedAtBottomCheck.IsChecked = s.CompletedAtBottom;
        AutoDeleteCompletedCheck.IsChecked = s.AutoDeleteCompleted;
        SoundEnabledCheck.IsChecked = s.SoundEnabled;
        ShowInTaskbarCheck.IsChecked = s.ShowInTaskbar;
        AutoStartCheck.IsChecked = AutoStartService.IsEnabled();

        DefaultRemindCheck.IsChecked = s.DefaultReminderEnabled;
        DefaultTimeBox.Text = string.IsNullOrWhiteSpace(s.DefaultReminderTime) ? "18:00" : s.DefaultReminderTime;
        AutoRolloverCheck.IsChecked = s.AutoRolloverNextDay;
        RolloverRemindCheck.IsChecked = s.RolloverReminderEnabled;
        RolloverTimeBox.Text = string.IsNullOrWhiteSpace(s.RolloverReminderTime) ? "09:00" : s.RolloverReminderTime;

        WeComBotCheck.IsChecked = s.WeComBotEnabled;
        WeComWebhookBox.Password = s.WeComWebhookOrKey ?? string.Empty;

        UpdateHotkeyBoxText();
        UpdateThemeButtons();
        UpdateLangButtons();
    }

    private void ApplyLanguageTexts()
    {
        Title = L10n.T("SettingsWinTitle");
        WinHeaderTitle.Text = L10n.T("SettingsWinTitle");
        SecLangHeader.Text = L10n.T("SecLanguage");
        SecDockHeader.Text = L10n.T("SecDockAndLayer");
        LabelLayer.Text = L10n.T("LabelWindowLayer");
        LayerItem0.Content = L10n.T("OptLayer0");
        LayerItem1.Content = L10n.T("OptLayer1");
        LayerItem2.Content = L10n.T("OptLayer2");

        EdgeSnapCheck.Content = L10n.T("ChkEdgeSnap");
        AutoCollapseCheck.Content = L10n.T("ChkAutoCollapse");
        HoverExpandCheck.Content = L10n.T("ChkHoverExpand");
        AvoidFullscreenCheck.Content = L10n.T("ChkAvoidFullscreen");
        HideFromCaptureCheck.Content = L10n.T("ChkHideCapture");

        SecAppHeader.Text = L10n.T("SecAppearance");
        LabelPalette.Text = L10n.T("LabelThemePalette");
        ThemeBtn0.Content = L10n.T("Theme0");
        ThemeBtn1.Content = L10n.T("Theme1");
        ThemeBtn2.Content = L10n.T("Theme2");
        ThemeBtn3.Content = L10n.T("Theme3");
        ThemeBtn4.Content = L10n.T("Theme4");

        OpacityLabel.Text = L10n.TF("LabelOpacityFmt", (int)Math.Round(OpacitySlider.Value * 100));
        LabelFont.Text = L10n.T("LabelFontScale");
        FontItem0.Content = L10n.T("Font0");
        FontItem1.Content = L10n.T("Font1");
        FontItem2.Content = L10n.T("Font2");

        SecBehaviorHeader.Text = L10n.T("SecBehavior");
        CompletedAtBottomCheck.Content = L10n.T("ChkCompletedBottom");
        AutoDeleteCompletedCheck.Content = L10n.T("ChkAutoDelete");
        SoundEnabledCheck.Content = L10n.T("ChkSound");
        ShowInTaskbarCheck.Content = L10n.T("ChkTaskbar");
        AutoStartCheck.Content = L10n.T("ChkAutoStart");

        LabelHotkey.Text = L10n.T("LabelHotkey");
        ClearHotkeyBtn.Content = L10n.T("HotkeyNone");
        HotkeyHintText.Text = L10n.T("HotkeyHint");
        UpdateHotkeyBoxText();

        SecShortcutsHeader.Text = L10n.T("SecShortcuts");
        ShortcutListText.Text = L10n.T("ShortcutList");

        SecSmartRemindHeader.Text = L10n.T("SecSmartRemind");
        DefaultRemindCheck.Content = L10n.T("ChkDefaultRemind");
        LabelDefaultTime.Text = L10n.T("LabelDefaultTime");
        AutoRolloverCheck.Content = L10n.T("ChkAutoRollover");
        RolloverRemindCheck.Content = L10n.T("ChkRolloverRemind");
        LabelRolloverTime.Text = L10n.T("LabelRolloverTime");
        WeComBotCheck.Content = L10n.T("ChkWeComBot");
        LabelWeComWebhook.Text = L10n.T("LabelWeComWebhook");
        TestWeComBtn.Content = L10n.T("BtnTestWeCom");

        TestRemindBtn.Content = L10n.T("BtnTestRemind");
        OpenDataBtn.Content = L10n.T("BtnOpenData");
        DoneBtn.Content = L10n.T("BtnDone");
    }

    private void UpdateHotkeyBoxText()
    {
        string hk = _storage.Settings.GlobalHotkey;
        HotkeyBox.Text = string.IsNullOrWhiteSpace(hk) || hk.Equals("None", StringComparison.OrdinalIgnoreCase)
            ? L10n.T("HotkeyNone")
            : hk;
    }

    private void UpdateLangButtons()
    {
        var btns = new[] { LangBtnZh, LangBtnEn, LangBtnFr };
        for (int i = 0; i < btns.Length; i++)
        {
            bool active = (int)_storage.Settings.Language == i;
            btns[i].Background = (System.Windows.Media.Brush)FindResource(active ? "AccentSoftBrush" : "ItemBgBrush");
            btns[i].Foreground = (System.Windows.Media.Brush)FindResource(active ? "AccentBrush" : "TextPrimaryBrush");
        }
    }

    private void UpdateThemeButtons()
    {
        var btns = new[] { ThemeBtn0, ThemeBtn1, ThemeBtn2, ThemeBtn3, ThemeBtn4 };
        for (int i = 0; i < btns.Length; i++)
        {
            bool active = (int)_storage.Settings.Theme == i;
            btns[i].Background = (System.Windows.Media.Brush)FindResource(active ? "AccentSoftBrush" : "ItemBgBrush");
            btns[i].Foreground = (System.Windows.Media.Brush)FindResource(active ? "AccentBrush" : "TextPrimaryBrush");
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void LangBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string tag) return;
        if (!int.TryParse(tag, out int idx)) return;

        var lang = (AppLanguage)Math.Clamp(idx, 0, 2);
        _app.SetLanguage(lang);
        UpdateLangButtons();
        ApplyLanguageTexts();
    }

    private void PinModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || PinModeCombo.SelectedIndex < 0) return;
        _main.SetPinMode((WindowPinMode)PinModeCombo.SelectedIndex);
    }

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        var s = _storage.Settings;

        s.EdgeSnapEnabled = EdgeSnapCheck.IsChecked == true;
        s.AutoCollapseOnEdge = AutoCollapseCheck.IsChecked == true;
        s.HoverExpandEnabled = HoverExpandCheck.IsChecked == true;
        s.AvoidFullscreen = AvoidFullscreenCheck.IsChecked == true;
        s.HideFromCapture = HideFromCaptureCheck.IsChecked == true;
        s.CompletedAtBottom = CompletedAtBottomCheck.IsChecked == true;
        s.AutoDeleteCompleted = AutoDeleteCompletedCheck.IsChecked == true;
        s.SoundEnabled = SoundEnabledCheck.IsChecked == true;
        s.ShowInTaskbar = ShowInTaskbarCheck.IsChecked == true;

        s.DefaultReminderEnabled = DefaultRemindCheck.IsChecked == true;
        s.AutoRolloverNextDay = AutoRolloverCheck.IsChecked == true;
        s.RolloverReminderEnabled = RolloverRemindCheck.IsChecked == true;
        s.WeComBotEnabled = WeComBotCheck.IsChecked == true;

        _storage.SaveSettings();
        _main.ApplySettingsLive();
    }

    private void TimeBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        bool ok1 = TryNormalizeTime(DefaultTimeBox.Text, out string t1);
        bool ok2 = TryNormalizeTime(RolloverTimeBox.Text, out string t2);
        if (!ok1 || !ok2)
        {
            TimeErrorText.Text = L10n.T("ErrInvalidTime");
            TimeErrorText.Visibility = Visibility.Visible;
            return;
        }
        TimeErrorText.Visibility = Visibility.Collapsed;
        DefaultTimeBox.Text = t1;
        RolloverTimeBox.Text = t2;
        _storage.Settings.DefaultReminderTime = t1;
        _storage.Settings.RolloverReminderTime = t2;
        _storage.SaveSettings();
        _main.ApplySettingsLive();
    }

    private static bool TryNormalizeTime(string input, out string normalized)
    {
        normalized = string.Empty;
        var parts = input.Trim().Replace('：', ':').Split(':');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m)) return false;
        if (h < 0 || h > 23 || m < 0 || m > 59) return false;
        normalized = $"{h:D2}:{m:D2}";
        return true;
    }

    private void WeComWebhookBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _storage.Settings.WeComWebhookOrKey = WeComWebhookBox.Password.Trim();
        _storage.SaveSettings();
    }

    private async void TestWeCom_Click(object sender, RoutedEventArgs e)
    {
        TestWeComBtn.IsEnabled = false;
        WeComStatusText.Visibility = Visibility.Collapsed;
        try
        {
            var (ok, msg) = await WeComNotifier.SendTestAsync(_storage.Settings.WeComWebhookOrKey);
            WeComStatusText.Text = ok ? L10n.T("WeComSentOk") : L10n.TF("WeComSentFail", msg);
            WeComStatusText.Foreground = ok
                ? (System.Windows.Media.Brush)FindResource("SuccessBrush")
                : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"));
            WeComStatusText.Visibility = Visibility.Visible;
        }
        finally
        {
            TestWeComBtn.IsEnabled = true;
        }
    }

    private void AutoStartCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        bool enable = AutoStartCheck.IsChecked == true;
        if (AutoStartService.SetEnabled(enable))
        {
            _storage.Settings.AutoStart = enable;
            _storage.SaveSettings();
            _app.RebuildTrayMenu();
        }
        else
        {
            _initializing = true;
            AutoStartCheck.IsChecked = AutoStartService.IsEnabled();
            _initializing = false;
        }
    }

    private void ThemeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string tag) return;
        if (!int.TryParse(tag, out int idx)) return;

        var mode = (AppThemeMode)Math.Clamp(idx, 0, 4);
        _storage.Settings.Theme = mode;
        _storage.SaveSettings();
        _app.ApplyTheme(mode);
        UpdateThemeButtons();
        UpdateLangButtons();
        _main.OnThemeChanged();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityLabel == null) return;
        OpacityLabel.Text = L10n.TF("LabelOpacityFmt", (int)Math.Round(e.NewValue * 100));
        if (_initializing) return;

        _storage.Settings.WindowOpacity = Math.Round(e.NewValue, 2);
        _storage.SaveSettings();
        _main.ApplySettingsLive();
    }

    private void FontScaleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (FontScaleCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag &&
            double.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out double scale))
        {
            _storage.Settings.FontScale = scale;
            _storage.SaveSettings();
            _main.RefreshList();
        }
    }

    // === Global Hotkey Recorder ===
    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        HotkeyBox.Text = L10n.T("HotkeyPressPrompt");
        HotkeyErrorText.Visibility = Visibility.Collapsed;
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        UpdateHotkeyBoxText();
    }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            UpdateHotkeyBoxText();
            DoneBtn.Focus();
            return;
        }

        // Ignore lone modifier keys
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        var mods = Keyboard.Modifiers;
        if (mods == ModifierKeys.None)
            return;

        var parts = new List<string>();
        if ((mods & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((mods & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((mods & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((mods & ModifierKeys.Windows) != 0) parts.Add("Win");
        parts.Add(key.ToString());

        string spec = string.Join("+", parts);
        _storage.Settings.GlobalHotkey = spec;
        _storage.SaveSettings();

        bool ok = _main.RegisterConfiguredHotkey();
        UpdateHotkeyBoxText();
        DoneBtn.Focus();

        if (!ok)
        {
            HotkeyErrorText.Text = L10n.T("HotkeyConflict");
            HotkeyErrorText.Visibility = Visibility.Visible;
        }
        else
        {
            HotkeyErrorText.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearHotkey_Click(object sender, RoutedEventArgs e)
    {
        _storage.Settings.GlobalHotkey = "None";
        _storage.SaveSettings();
        _main.RegisterConfiguredHotkey();
        UpdateHotkeyBoxText();
        HotkeyErrorText.Visibility = Visibility.Collapsed;
    }

    private void TestReminder_Click(object sender, RoutedEventArgs e) => _app.TriggerTestReminder();
    private void OpenData_Click(object sender, RoutedEventArgs e) => _app.OpenDataFolder();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
