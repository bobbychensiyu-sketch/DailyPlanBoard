using System;
using System.Windows;
using DailyPlanBoard.Models;
using DailyPlanBoard.Services;

namespace DailyPlanBoard.Views;

public partial class ReminderPopup : Window
{
    private readonly PlanItem? _item;
    private readonly Action<string>? _onComplete;
    private readonly Action<string, int>? _onSnooze;

    private static int _activePopups;

    public ReminderPopup(PlanItem? item, string title, string? note, Action<string>? onComplete, Action<string, int>? onSnooze)
    {
        InitializeComponent();
        _item = item;
        _onComplete = onComplete;
        _onSnooze = onSnooze;

        PopupHeaderTitle.Text = L10n.T("PopupTitle");
        SnoozeBtn.Content = L10n.T("BtnSnooze10");
        DismissBtn.Content = L10n.T("BtnGotIt");
        CompleteBtn.Content = L10n.T("BtnComplete");

        TimeText.Text = DateTime.Now.ToString("HH:mm");
        TaskTitleText.Text = title;

        string combinedNote = note ?? string.Empty;
        if (_item != null && _item.HasMilestone && _item.DaysUntilMilestone.HasValue)
        {
            int dLeft = _item.DaysUntilMilestone.Value;
            string msDate = _item.MilestoneDate ?? string.Empty;
            string msBanner;

            if (dLeft == 0)
            {
                PopupHeaderTitle.Text = L10n.T("PopupTitleUrgentToday");
                var redBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"));
                PopupHeaderTitle.Foreground = redBrush;
                PopupCardBorder.BorderBrush = redBrush;
                PopupCardBorder.BorderThickness = new Thickness(2.2);
                msBanner = L10n.TF("MilestoneNoteDueToday", msDate);
            }
            else if (dLeft < 0)
            {
                PopupHeaderTitle.Text = L10n.T("PopupTitleOverdue");
                var redBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"));
                PopupHeaderTitle.Foreground = redBrush;
                PopupCardBorder.BorderBrush = redBrush;
                PopupCardBorder.BorderThickness = new Thickness(2.2);
                msBanner = L10n.TF("MilestoneNoteOverdue", msDate, Math.Abs(dLeft));
            }
            else if (dLeft == 1)
            {
                PopupHeaderTitle.Text = L10n.T("PopupTitleT1");
                var orangeBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EA580C"));
                PopupHeaderTitle.Foreground = orangeBrush;
                PopupCardBorder.BorderBrush = orangeBrush;
                PopupCardBorder.BorderThickness = new Thickness(1.8);
                msBanner = L10n.TF("MilestoneNoteT1", msDate);
            }
            else if (dLeft == 3)
            {
                PopupHeaderTitle.Text = L10n.T("PopupTitleT3");
                var amberBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D97706"));
                PopupHeaderTitle.Foreground = amberBrush;
                PopupCardBorder.BorderBrush = amberBrush;
                PopupCardBorder.BorderThickness = new Thickness(1.8);
                msBanner = L10n.TF("MilestoneNoteT3", msDate);
            }
            else
            {
                PopupHeaderTitle.Text = L10n.TF("PopupTitleMilestone", dLeft);
                msBanner = L10n.TF("MilestoneNoteDays", msDate, dLeft);
            }

            combinedNote = string.IsNullOrWhiteSpace(combinedNote)
                ? msBanner
                : $"{msBanner}\n{combinedNote}";
        }

        if (!string.IsNullOrWhiteSpace(combinedNote))
        {
            TaskNoteText.Text = combinedNote;
            TaskNoteText.Visibility = Visibility.Visible;
        }

        if (_item == null)
        {
            CompleteBtn.Visibility = Visibility.Collapsed;
        }

        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            int slot = _activePopups % 4;
            _activePopups++;
            Left = area.Right - Width - 14;
            Top = area.Bottom - ActualHeight - 14 - (slot * 140);
            if (Top < area.Top + 12) Top = area.Top + 12;
        };

        Closed += (_, _) =>
        {
            if (_activePopups > 0) _activePopups--;
        };
    }

    private void Complete_Click(object sender, RoutedEventArgs e)
    {
        if (_item != null)
            _onComplete?.Invoke(_item.Id);
        Close();
    }

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        if (_item != null)
            _onSnooze?.Invoke(_item.Id, 10);
        Close();
    }

    private void Dismiss_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
