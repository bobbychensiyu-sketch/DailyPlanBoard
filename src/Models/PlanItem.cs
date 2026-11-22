using System;
using System.Text.Json.Serialization;
using DailyPlanBoard.Services;

namespace DailyPlanBoard.Models;

public enum TaskPriority
{
    Normal = 0,
    Important = 1,
    Urgent = 2
}

public class PlanItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>
    /// 计划所属日期，格式 yyyy-MM-dd
    /// </summary>
    public string Date { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

    public string Title { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    /// <summary>
    /// 提醒时间，格式 HH:mm（为空表示无提醒）
    /// </summary>
    public string? ReminderTime { get; set; }

    /// <summary>
    /// 已触发提醒的标识，格式 yyyy-MM-dd HH:mm，防止重复触发
    /// </summary>
    public string? LastNotifiedKey { get; set; }

    /// <summary>
    /// 延后提醒的目标本地时间
    /// </summary>
    public DateTime? SnoozeUntil { get; set; }

    /// <summary>
    /// 是否为每日重复任务
    /// </summary>
    public bool IsDailyRepeat { get; set; }

    /// <summary>
    /// 若由每日重复模板生成，记录源模板 ID
    /// </summary>
    public string? RepeatSourceId { get; set; }

    /// <summary>
    /// 计划节点（截止）日期，格式 yyyy-MM-dd（为空表示无节点截止日）
    /// </summary>
    public string? MilestoneDate { get; set; }

    /// <summary>
    /// 节点计划跨多天自动排入时共享的系列分组 ID
    /// </summary>
    public string? MilestoneGroupId { get; set; }

    /// <summary>
    /// 节点关键日（提前3天/提前1天/当天）早间自动提醒已触发日期标识 yyyy-MM-dd
    /// </summary>
    public string? LastMilestoneMorningNotifiedDate { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public bool HasMilestone => !string.IsNullOrWhiteSpace(MilestoneDate);

    /// <summary>
    /// 当前计划所属日期 (Date) 距离节点日期 (MilestoneDate) 剩余天数
    /// </summary>
    [JsonIgnore]
    public int? DaysUntilMilestone
    {
        get
        {
            if (string.IsNullOrWhiteSpace(MilestoneDate)) return null;
            if (!DateTime.TryParseExact(MilestoneDate.Trim(), "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var mDate))
                return null;
            if (!DateTime.TryParseExact(Date.Trim(), "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var curDate))
                curDate = DateTime.Today;
            return (mDate.Date - curDate.Date).Days;
        }
    }

    /// <summary>
    /// 是否为节点关键提醒日（提前3天、提前1天、节点当天或已超期）
    /// </summary>
    [JsonIgnore]
    public bool IsMilestoneKeyAlertDay
    {
        get
        {
            var d = DaysUntilMilestone;
            return d.HasValue && (d.Value == 3 || d.Value == 1 || d.Value <= 0);
        }
    }

    /// <summary>
    /// 综合节点倒计时的有效优先级：节点当天/超期自动升为紧急 Urgent，提前1天/3天至少为重要 Important
    /// </summary>
    [JsonIgnore]
    public TaskPriority EffectivePriority
    {
        get
        {
            var d = DaysUntilMilestone;
            if (d.HasValue)
            {
                if (d.Value <= 0) return TaskPriority.Urgent;
                if ((d.Value == 1 || d.Value == 3) && Priority < TaskPriority.Important)
                    return TaskPriority.Important;
            }
            return Priority;
        }
    }

    [JsonIgnore]
    public string DisplayMilestoneBadgeText
    {
        get
        {
            var d = DaysUntilMilestone;
            if (!d.HasValue || string.IsNullOrWhiteSpace(MilestoneDate)) return string.Empty;
            string shortDate = MilestoneDate.Length >= 10 ? MilestoneDate[5..] : MilestoneDate;
            if (d.Value == 0)
                return L10n.TF("MilestoneDueTodayBadge", shortDate);
            if (d.Value == 1)
                return L10n.TF("MilestoneLeft1Badge", shortDate);
            if (d.Value == 3)
                return L10n.TF("MilestoneLeft3Badge", shortDate);
            if (d.Value > 0)
                return L10n.TF("MilestoneLeftDaysBadge", d.Value, shortDate);
            return L10n.TF("MilestoneOverdueBadge", Math.Abs(d.Value), shortDate);
        }
    }

    [JsonIgnore]
    public bool HasReminder => !string.IsNullOrWhiteSpace(ReminderTime) || SnoozeUntil.HasValue;

    [JsonIgnore]
    public string DisplayReminderText
    {
        get
        {
            if (SnoozeUntil.HasValue)
                return $"⏰ {SnoozeUntil.Value:HH:mm} {L10n.T("SnoozeTag")}";
            if (!string.IsNullOrWhiteSpace(ReminderTime))
                return $"⏰ {ReminderTime}";
            return string.Empty;
        }
    }

    [JsonIgnore]
    public bool IsReminderOverdue
    {
        get
        {
            if (IsCompleted) return false;
            var now = DateTime.Now;
            if (SnoozeUntil.HasValue)
                return now > SnoozeUntil.Value;
            if (string.IsNullOrWhiteSpace(ReminderTime))
                return false;
            if (DateTime.TryParseExact($"{Date} {ReminderTime}", "yyyy-MM-dd HH:mm",
                null, System.Globalization.DateTimeStyles.None, out var target))
            {
                return now > target;
            }
            return false;
        }
    }
}

public class RepeatTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Title { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public string? ReminderTime { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class PlanStoreData
{
    public int SchemaVersion { get; set; } = 1;
    public List<PlanItem> Items { get; set; } = new();
    public List<RepeatTemplate> RepeatTemplates { get; set; } = new();
}
