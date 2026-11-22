using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DailyPlanBoard.Models;
using DailyPlanBoard.Services;

namespace DailyPlanBoard.Views;

public partial class EditTaskDialog : Window
{
    public PlanItem ResultItem { get; private set; }

    public EditTaskDialog(PlanItem source)
    {
        InitializeComponent();
        ApplyLanguage();

        ResultItem = new PlanItem
        {
            Id = source.Id,
            Date = source.Date,
            Title = source.Title,
            Note = source.Note,
            IsCompleted = source.IsCompleted,
            CompletedAt = source.CompletedAt,
            Priority = source.Priority,
            ReminderTime = source.ReminderTime,
            IsDailyRepeat = source.IsDailyRepeat,
            RepeatSourceId = source.RepeatSourceId,
            MilestoneDate = source.MilestoneDate,
            MilestoneGroupId = source.MilestoneGroupId,
            SortOrder = source.SortOrder,
            CreatedAt = source.CreatedAt
        };

        TitleBox.Text = source.Title;
        NoteBox.Text = source.Note;
        ReminderBox.Text = source.ReminderTime ?? string.Empty;
        DailyRepeatCheck.IsChecked = source.IsDailyRepeat;

        if (DateTime.TryParseExact(source.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            PlanDatePicker.SelectedDate = dt;
        else
            PlanDatePicker.SelectedDate = DateTime.Today;

        if (!string.IsNullOrWhiteSpace(source.MilestoneDate) &&
            DateTime.TryParseExact(source.MilestoneDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var mDt))
        {
            MilestoneDatePicker.SelectedDate = mDt;
        }
        else
        {
            MilestoneDatePicker.SelectedDate = null;
        }

        PriorityCombo.SelectedIndex = (int)source.Priority;

        Loaded += (_, _) =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        };
    }

    private void ApplyLanguage()
    {
        Title = L10n.T("EditTitle");
        DialogTitleText.Text = L10n.T("EditTitle");
        LabelContent.Text = L10n.T("LabelPlanContent");
        LabelNote.Text = L10n.T("LabelNoteOpt");
        LabelDate.Text = L10n.T("LabelPlanDate");
        LabelPri.Text = L10n.T("LabelPriority");
        PriItem0.Content = L10n.T("OptPri0");
        PriItem1.Content = L10n.T("OptPri1");
        PriItem2.Content = L10n.T("OptPri2");
        LabelMilestone.Text = L10n.T("LabelMilestoneDate");
        ClearMilestoneBtn.Content = L10n.T("BtnClear");
        MsP3Btn.Content = L10n.T("MsPreset+3");
        MsP5Btn.Content = L10n.T("MsPreset+5");
        MsP7Btn.Content = L10n.T("MsPreset+7");
        MsP14Btn.Content = L10n.T("MsPreset+14");
        MsMonthEndBtn.Content = L10n.T("MsPresetMonthEnd");
        LabelRem.Text = L10n.T("LabelReminderFmt");
        ClearRemBtn.Content = L10n.T("BtnClear");
        Q15Btn.Content = L10n.T("Quick+15");
        Q30Btn.Content = L10n.T("Quick+30");
        Q60Btn.Content = L10n.T("Quick+60");
        DailyRepeatCheck.Content = L10n.T("CheckDailyRepeat");
        CancelBtn.Content = L10n.T("BtnCancel");
        SaveBtn.Content = L10n.T("BtnSave");
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void QuickTime_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string tag)
            return;

        if (tag.StartsWith("+") && int.TryParse(tag[1..], out int mins))
        {
            var t = DateTime.Now.AddMinutes(mins);
            ReminderBox.Text = t.ToString("HH:mm");
            PlanDatePicker.SelectedDate = t.Date;
        }
        else
        {
            ReminderBox.Text = tag;
        }
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void ClearReminder_Click(object sender, RoutedEventArgs e)
    {
        ReminderBox.Text = string.Empty;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void QuickMilestone_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string tag)
            return;

        DateTime baseDate = (PlanDatePicker.SelectedDate ?? DateTime.Today).Date;
        if (tag == "monthend")
        {
            MilestoneDatePicker.SelectedDate = new DateTime(baseDate.Year, baseDate.Month, DateTime.DaysInMonth(baseDate.Year, baseDate.Month));
        }
        else if (int.TryParse(tag, out int days))
        {
            MilestoneDatePicker.SelectedDate = baseDate.AddDays(days);
        }
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void ClearMilestone_Click(object sender, RoutedEventArgs e)
    {
        MilestoneDatePicker.SelectedDate = null;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string title = TitleBox.Text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            ShowError(L10n.T("ErrEmptyTitle"));
            TitleBox.Focus();
            return;
        }

        string reminder = ReminderBox.Text.Trim();
        if (!string.IsNullOrEmpty(reminder))
        {
            reminder = reminder.Replace('：', ':');
            if (!TryNormalizeTime(reminder, out string normalized))
            {
                ShowError(L10n.T("ErrBadTime"));
                ReminderBox.Focus();
                return;
            }
            reminder = normalized;
        }

        ResultItem.Title = title;
        ResultItem.Note = NoteBox.Text.Trim();
        ResultItem.Date = (PlanDatePicker.SelectedDate ?? DateTime.Today).ToString("yyyy-MM-dd");
        ResultItem.Priority = (TaskPriority)Math.Clamp(PriorityCombo.SelectedIndex, 0, 2);
        ResultItem.ReminderTime = string.IsNullOrEmpty(reminder) ? null : reminder;
        ResultItem.MilestoneDate = MilestoneDatePicker.SelectedDate.HasValue
            ? MilestoneDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd")
            : null;
        ResultItem.IsDailyRepeat = DailyRepeatCheck.IsChecked == true && string.IsNullOrEmpty(ResultItem.MilestoneDate);

        DialogResult = true;
        Close();
    }

    private static bool TryNormalizeTime(string input, out string normalized)
    {
        normalized = string.Empty;
        var parts = input.Split(':');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m)) return false;
        if (h < 0 || h > 23 || m < 0 || m > 59) return false;
        normalized = $"{h:D2}:{m:D2}";
        return true;
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
