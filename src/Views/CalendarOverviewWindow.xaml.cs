using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DailyPlanBoard.Models;
using DailyPlanBoard.Services;
using Win32SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace DailyPlanBoard.Views;

public partial class CalendarOverviewWindow : Window
{
    private readonly StorageService _storage;
    private readonly Action<DateTime> _onJumpToDate;
    private readonly Action _onTasksChanged;

    private int _viewYear;
    private int _viewMonth;
    private DateTime _selectedDate;
    private bool _initializing = true;

    public CalendarOverviewWindow(StorageService storage, DateTime initialDate, Action<DateTime> onJumpToDate, Action onTasksChanged)
    {
        InitializeComponent();
        _storage = storage;
        _onJumpToDate = onJumpToDate;
        _onTasksChanged = onTasksChanged;

        _selectedDate = initialDate.Date;
        _viewYear = _selectedDate.Year;
        _viewMonth = _selectedDate.Month;

        // Default export range: current month
        StartDatePicker.SelectedDate = new DateTime(_viewYear, _viewMonth, 1);
        EndDatePicker.SelectedDate = new DateTime(_viewYear, _viewMonth, DateTime.DaysInMonth(_viewYear, _viewMonth));

        ApplyLanguage();
        _initializing = false;

        RenderWeekdayHeaders();
        RenderCalendar();
        RenderSelectedDay();
        UpdateRangeStats();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
    }

    private void ApplyLanguage()
    {
        Title = L10n.T("CalWinTitle");
        WinTitleText.Text = L10n.T("CalWinTitle");
        ThisMonthBtn.Content = L10n.T("CalBtnToday");
        JumpBoardBtn.Content = L10n.T("CalJumpBoard");
        EmptyDayText.Text = L10n.T("CalEmptyDay");
        ExportHeaderLabel.Text = L10n.T("CalExportHeader");
        PresetWeekBtn.Content = L10n.T("CalPresetWeek");
        PresetMonthBtn.Content = L10n.T("CalPresetMonth");
        Preset7DaysBtn.Content = L10n.T("CalPreset7Days");
        FromLabel.Text = L10n.T("CalFrom");
        ToLabel.Text = L10n.T("CalTo");
        ExportBtn.Content = L10n.T("CalExportBtn");
        SendWeComBtn.Content = L10n.T("CalWeComSummaryBtn");
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void RenderWeekdayHeaders()
    {
        WeekdayHeaderGrid.Children.Clear();
        var dows = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };
        foreach (var d in dows)
        {
            var tb = new TextBlock
            {
                Text = L10n.GetWeekday(d),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            WeekdayHeaderGrid.Children.Add(tb);
        }
    }

    private void RenderCalendar()
    {
        MonthLabel.Text = L10n.TF("CalMonthFmt", _viewYear, _viewMonth);
        CalendarGrid.Children.Clear();

        var summary = _storage.GetMonthSummary(_viewYear, _viewMonth);
        var firstOfMonth = new DateTime(_viewYear, _viewMonth, 1);
        int mondayOffset = ((int)firstOfMonth.DayOfWeek + 6) % 7; // Mon=0..Sun=6
        DateTime gridStart = firstOfMonth.AddDays(-mondayOffset);
        DateTime today = DateTime.Today;

        for (int i = 0; i < 42; i++)
        {
            DateTime d = gridStart.AddDays(i);
            bool inMonth = d.Month == _viewMonth && d.Year == _viewYear;
            bool isToday = d == today;
            bool isSelected = d == _selectedDate;
            string dateKey = d.ToString("yyyy-MM-dd");

            summary.TryGetValue(dateKey, out var stat);

            var cell = new Border
            {
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(isToday || isSelected ? 1.5 : 1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Opacity = inMonth ? 1.0 : 0.42
            };

            if (isSelected)
                cell.SetResourceReference(Border.BackgroundProperty, "AccentSoftBrush");
            else
                cell.SetResourceReference(Border.BackgroundProperty, "ItemBgBrush");

            if (isSelected || isToday)
                cell.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
            else
                cell.SetResourceReference(Border.BorderBrushProperty, "BorderBrushKey");

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            var dayNum = new TextBlock
            {
                Text = d.Day.ToString(),
                FontSize = 12.5,
                FontWeight = isToday || isSelected ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };
            dayNum.SetResourceReference(TextBlock.ForegroundProperty,
                isSelected ? "AccentBrush" : "TextPrimaryBrush");
            stack.Children.Add(dayNum);

            if (stat.Total > 0)
            {
                bool allDone = stat.Done == stat.Total;
                var badge = new TextBlock
                {
                    Text = allDone ? $"✓{stat.Total}" : $"{stat.Done}/{stat.Total}",
                    FontSize = 9.8,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                badge.SetResourceReference(TextBlock.ForegroundProperty,
                    allDone ? "SuccessBrush" : "AccentBrush");
                stack.Children.Add(badge);
            }

            cell.Child = stack;
            DateTime captured = d;
            cell.MouseLeftButtonDown += (_, e) =>
            {
                _selectedDate = captured;
                if (captured.Month != _viewMonth || captured.Year != _viewYear)
                {
                    _viewYear = captured.Year;
                    _viewMonth = captured.Month;
                }
                RenderCalendar();
                RenderSelectedDay();
                if (e.ClickCount == 2)
                {
                    _onJumpToDate(_selectedDate);
                    Close();
                }
            };

            CalendarGrid.Children.Add(cell);
        }
    }

    private void RenderSelectedDay()
    {
        string dateStr = _selectedDate.ToString("yyyy-MM-dd");
        var items = _storage.GetItemsForDate(dateStr);
        int done = items.Count(x => x.IsCompleted);
        SelectedDayHeader.Text = L10n.TF("CalDayPlansFmt", dateStr, done, items.Count);

        DayPlansPanel.Children.Clear();
        if (items.Count == 0)
        {
            EmptyDayText.Visibility = Visibility.Visible;
            return;
        }
        EmptyDayText.Visibility = Visibility.Collapsed;

        foreach (var it in items)
        {
            var row = new Border
            {
                Margin = new Thickness(0, 0, 0, 5),
                Padding = new Thickness(8, 6, 8, 6),
                CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(1)
            };
            row.SetResourceReference(Border.BackgroundProperty, "ItemBgBrush");
            row.SetResourceReference(Border.BorderBrushProperty, "BorderBrushKey");

            var dp = new DockPanel();
            var cb = new System.Windows.Controls.CheckBox
            {
                IsChecked = it.IsCompleted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Style = (Style)FindResource("TaskCheckBoxStyle")
            };
            cb.Click += (_, _) =>
            {
                _storage.ToggleComplete(it.Id, cb.IsChecked == true);
                RenderCalendar();
                RenderSelectedDay();
                UpdateRangeStats();
                _onTasksChanged();
            };
            dp.Children.Add(cb);

            if (!string.IsNullOrWhiteSpace(it.ReminderTime))
            {
                var rem = new TextBlock
                {
                    Text = $"⏰ {it.ReminderTime}",
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0)
                };
                rem.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                DockPanel.SetDock(rem, Dock.Right);
                dp.Children.Add(rem);
            }

            if (it.HasMilestone && it.DaysUntilMilestone.HasValue)
            {
                int dLeft = it.DaysUntilMilestone.Value;
                var ms = new TextBlock
                {
                    Text = it.DisplayMilestoneBadgeText,
                    FontSize = 10.2,
                    FontWeight = dLeft <= 1 ? FontWeights.Bold : FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0)
                };
                if (dLeft <= 0)
                    ms.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"));
                else if (dLeft == 1)
                    ms.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EA580C"));
                else
                    ms.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");

                DockPanel.SetDock(ms, Dock.Right);
                dp.Children.Add(ms);
            }

            var txt = new TextBlock
            {
                Text = it.Title,
                FontSize = 12.2,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                TextDecorations = it.IsCompleted ? TextDecorations.Strikethrough : null
            };
            txt.SetResourceReference(TextBlock.ForegroundProperty,
                it.IsCompleted ? "TextCompletedBrush" : "TextPrimaryBrush");
            dp.Children.Add(txt);

            row.Child = dp;
            DayPlansPanel.Children.Add(row);
        }
    }

    private void UpdateRangeStats()
    {
        if (_initializing) return;
        DateTime s = (StartDatePicker.SelectedDate ?? DateTime.Today).Date;
        DateTime e = (EndDatePicker.SelectedDate ?? DateTime.Today).Date;
        var items = _storage.GetItemsInDateRange(s, e);
        int total = items.Count;
        int done = items.Count(x => x.IsCompleted);
        int pending = total - done;
        int rate = total == 0 ? 0 : (int)Math.Round(done * 100.0 / total);

        RangeStatsText.Text = L10n.TF("CalStatsFmt", total, done, pending, rate);
    }

    private void PrevMonth_Click(object sender, RoutedEventArgs e)
    {
        var dt = new DateTime(_viewYear, _viewMonth, 1).AddMonths(-1);
        _viewYear = dt.Year;
        _viewMonth = dt.Month;
        RenderCalendar();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        var dt = new DateTime(_viewYear, _viewMonth, 1).AddMonths(1);
        _viewYear = dt.Year;
        _viewMonth = dt.Month;
        RenderCalendar();
    }

    private void ThisMonth_Click(object sender, RoutedEventArgs e)
    {
        _selectedDate = DateTime.Today;
        _viewYear = _selectedDate.Year;
        _viewMonth = _selectedDate.Month;
        RenderCalendar();
        RenderSelectedDay();
    }

    private void JumpToBoard_Click(object sender, RoutedEventArgs e)
    {
        _onJumpToDate(_selectedDate);
        Close();
    }

    private void RangePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string tag) return;
        DateTime today = DateTime.Today;
        _initializing = true;
        switch (tag)
        {
            case "week":
                int offset = ((int)today.DayOfWeek + 6) % 7;
                StartDatePicker.SelectedDate = today.AddDays(-offset);
                EndDatePicker.SelectedDate = today.AddDays(6 - offset);
                break;
            case "month":
                StartDatePicker.SelectedDate = new DateTime(_viewYear, _viewMonth, 1);
                EndDatePicker.SelectedDate = new DateTime(_viewYear, _viewMonth, DateTime.DaysInMonth(_viewYear, _viewMonth));
                break;
            case "7d":
                StartDatePicker.SelectedDate = today.AddDays(-6);
                EndDatePicker.SelectedDate = today;
                break;
        }
        _initializing = false;
        UpdateRangeStats();
    }

    private void RangePicker_Changed(object? sender, SelectionChangedEventArgs e) => UpdateRangeStats();

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        DateTime s = (StartDatePicker.SelectedDate ?? DateTime.Today).Date;
        DateTime end = (EndDatePicker.SelectedDate ?? DateTime.Today).Date;
        if (end < s) (s, end) = (end, s);

        var dlg = new Win32SaveFileDialog
        {
            Title = L10n.T("CalExportBtn"),
            FileName = $"每日计划汇总_{s:yyyyMMdd}-{end:yyyyMMdd}",
            Filter = "Markdown 报告 (*.md)|*.md|CSV 表格 (Excel可用) (*.csv)|*.csv",
            DefaultExt = ".md"
        };

        if (dlg.ShowDialog(this) == true)
        {
            _storage.ExportDateRangeReport(s, end, dlg.FileName);
            System.Windows.MessageBox.Show(this, L10n.TF("ExportSuccess", dlg.FileName), L10n.T("AppTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void SendWeComSummary_Click(object sender, RoutedEventArgs e)
    {
        string key = _storage.Settings.WeComWebhookOrKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            System.Windows.MessageBox.Show(this, L10n.T("WeComNeedKey"), L10n.T("AppTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SendWeComBtn.IsEnabled = false;
        try
        {
            string dateStr = _selectedDate.ToString("yyyy-MM-dd");
            var items = _storage.GetItemsForDate(dateStr);
            var (ok, msg) = await WeComNotifier.SendDailySummaryAsync(key, dateStr, items);
            System.Windows.MessageBox.Show(this,
                ok ? L10n.T("WeComSentOk") : L10n.TF("WeComSentFail", msg),
                L10n.T("AppTitle"),
                MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        finally
        {
            SendWeComBtn.IsEnabled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
