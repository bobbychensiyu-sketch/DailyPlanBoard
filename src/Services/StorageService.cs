using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DailyPlanBoard.Models;

namespace DailyPlanBoard.Services;

public class StorageService
{
    private readonly object _sync = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string DataDirectory { get; }
    public string PlansFilePath { get; }
    public string PlansBackupPath { get; }
    public string SettingsFilePath { get; }

    public PlanStoreData Store { get; private set; } = new();
    public AppSettings Settings { get; private set; } = new();

    public StorageService()
    {
        string baseDir = AppContext.BaseDirectory;
        DataDirectory = Path.Combine(baseDir, "data");
        Directory.CreateDirectory(DataDirectory);

        PlansFilePath = Path.Combine(DataDirectory, "plans.json");
        PlansBackupPath = Path.Combine(DataDirectory, "plans.backup.json");
        SettingsFilePath = Path.Combine(DataDirectory, "settings.json");

        Load();
    }

    public void Load()
    {
        lock (_sync)
        {
            Settings = LoadJson<AppSettings>(SettingsFilePath) ?? new AppSettings();
            Settings.AutoStart = AutoStartService.IsEnabled();

            if (File.Exists(PlansFilePath))
            {
                Store = LoadJson<PlanStoreData>(PlansFilePath)
                    ?? LoadJson<PlanStoreData>(PlansBackupPath)
                    ?? new PlanStoreData();
            }
            else
            {
                Store = CreateStarterData();
                SavePlans();
            }
        }
    }

    private static PlanStoreData CreateStarterData()
    {
        string today = DateTime.Today.ToString("yyyy-MM-dd");
        return new PlanStoreData
        {
            SchemaVersion = 1,
            Items = new List<PlanItem>
            {
                new PlanItem
                {
                    Date = today,
                    Title = "点击左侧方框打勾 ✓ 即可完成计划",
                    Note = "已完成项可自动沉底或一键清理",
                    Priority = TaskPriority.Important,
                    SortOrder = 0
                },
                new PlanItem
                {
                    Date = today,
                    Title = "双击任意条目可编辑详情、设置准点提醒或每日重复",
                    Note = "顶栏可一键切换「常驻桌面 / 窗口置顶 / 普通窗口」与主题",
                    Priority = TaskPriority.Normal,
                    SortOrder = 1
                }
            }
        };
    }

    private T? LoadJson<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void SavePlans()
    {
        lock (_sync)
        {
            AtomicWrite(PlansFilePath, PlansBackupPath, Store);
        }
    }

    public void SaveSettings()
    {
        lock (_sync)
        {
            AtomicWrite(SettingsFilePath, null, Settings);
        }
    }

    private void AtomicWrite<T>(string targetPath, string? backupPath, T data)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            string tempPath = targetPath + ".tmp";
            string json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(tempPath, json);

            if (!string.IsNullOrEmpty(backupPath) && File.Exists(targetPath))
            {
                File.Copy(targetPath, backupPath, true);
            }

            File.Move(tempPath, targetPath, true);
        }
        catch
        {
            // Swallow transient IO contention
        }
    }

    /// <summary>
    /// 确保指定日期（今天及未来）实例化所有启用的每日重复任务。
    /// </summary>
    public bool EnsureDailyRepeatsForDate(string dateStr)
    {
        lock (_sync)
        {
            if (string.CompareOrdinal(dateStr, DateTime.Today.ToString("yyyy-MM-dd")) < 0)
                return false;

            bool changed = false;
            var existingSourceIds = new HashSet<string>(
                Store.Items
                    .Where(x => x.Date == dateStr && !string.IsNullOrEmpty(x.RepeatSourceId))
                    .Select(x => x.RepeatSourceId!));

            int maxOrder = Store.Items.Where(x => x.Date == dateStr).Select(x => x.SortOrder).DefaultIfEmpty(-1).Max();

            foreach (var tpl in Store.RepeatTemplates.Where(t => t.Enabled))
            {
                if (existingSourceIds.Contains(tpl.Id))
                    continue;

                Store.Items.Add(new PlanItem
                {
                    Date = dateStr,
                    Title = tpl.Title,
                    Note = tpl.Note,
                    Priority = tpl.Priority,
                    ReminderTime = tpl.ReminderTime,
                    IsDailyRepeat = true,
                    RepeatSourceId = tpl.Id,
                    SortOrder = ++maxOrder,
                    CreatedAt = DateTime.Now
                });
                changed = true;
            }

            if (changed)
                SavePlans();

            return changed;
        }
    }

    public List<PlanItem> GetItemsForDate(string dateStr)
    {
        EnsureDailyRepeatsForDate(dateStr);
        lock (_sync)
        {
            return Store.Items
                .Where(x => x.Date == dateStr)
                .OrderBy(x => Settings.CompletedAtBottom ? (x.IsCompleted ? 1 : 0) : 0)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .ToList();
        }
    }

    public int CountPastUnfinished(string todayStr)
    {
        lock (_sync)
        {
            var futureMilestoneGroups = Store.Items
                .Where(x => !string.IsNullOrEmpty(x.MilestoneGroupId) && string.CompareOrdinal(x.Date, todayStr) >= 0)
                .Select(x => x.MilestoneGroupId!)
                .ToHashSet();

            return Store.Items.Count(x =>
                !x.IsCompleted &&
                !x.IsDailyRepeat &&
                string.CompareOrdinal(x.Date, todayStr) < 0 &&
                (string.IsNullOrEmpty(x.MilestoneGroupId) || !futureMilestoneGroups.Contains(x.MilestoneGroupId)));
        }
    }

    public int RolloverPastUnfinishedToDate(string targetDateStr, bool applyReminder = false, string? reminderTime = null)
    {
        lock (_sync)
        {
            EnsureDailyRepeatsForDate(targetDateStr);

            // 若节点计划在今天或未来已有每日倒计时实例，则仅清理过期未勾的旧日分身，避免在今天重复叠加
            var futureMilestoneGroups = Store.Items
                .Where(x => !string.IsNullOrEmpty(x.MilestoneGroupId) && string.CompareOrdinal(x.Date, targetDateStr) >= 0)
                .Select(x => x.MilestoneGroupId!)
                .ToHashSet();

            int removedOldMilestoneCopies = Store.Items.RemoveAll(x =>
                !x.IsCompleted &&
                string.CompareOrdinal(x.Date, targetDateStr) < 0 &&
                !string.IsNullOrEmpty(x.MilestoneGroupId) &&
                futureMilestoneGroups.Contains(x.MilestoneGroupId));

            // 每日重复任务每天都会自动生成当日实例，顺延仅针对未完成的非重复计划（或已整体超期的节点计划）
            var pastItems = Store.Items
                .Where(x => !x.IsCompleted && !x.IsDailyRepeat && string.CompareOrdinal(x.Date, targetDateStr) < 0)
                .ToList();

            if (pastItems.Count == 0)
            {
                if (removedOldMilestoneCopies > 0) SavePlans();
                return 0;
            }

            // 对已超期的同一 MilestoneGroupId 只保留最新一条顺延到今天，避免超期多天产生多条副本
            var seenOverdueGroups = new HashSet<string>();
            var toRollover = new List<PlanItem>();
            foreach (var item in pastItems.OrderByDescending(x => x.Date))
            {
                if (!string.IsNullOrEmpty(item.MilestoneGroupId))
                {
                    if (!seenOverdueGroups.Add(item.MilestoneGroupId))
                    {
                        Store.Items.Remove(item);
                        continue;
                    }
                    item.Priority = TaskPriority.Urgent;
                }
                toRollover.Add(item);
            }

            int maxOrder = Store.Items.Where(x => x.Date == targetDateStr).Select(x => x.SortOrder).DefaultIfEmpty(-1).Max();
            foreach (var item in toRollover)
            {
                item.Date = targetDateStr;
                item.LastNotifiedKey = null;
                item.LastMilestoneMorningNotifiedDate = null;
                item.SnoozeUntil = null;
                if (applyReminder && !string.IsNullOrWhiteSpace(reminderTime))
                {
                    item.ReminderTime = reminderTime.Trim();
                }
                item.SortOrder = ++maxOrder;
            }

            SavePlans();
            return toRollover.Count;
        }
    }

    public Dictionary<string, (int Total, int Done)> GetMonthSummary(int year, int month)
    {
        EnsureDailyRepeatsForDate(DateTime.Today.ToString("yyyy-MM-dd"));
        string prefix = $"{year:D4}-{month:D2}-";
        lock (_sync)
        {
            return Store.Items
                .Where(x => x.Date.StartsWith(prefix, StringComparison.Ordinal))
                .GroupBy(x => x.Date)
                .ToDictionary(
                    g => g.Key,
                    g => (Total: g.Count(), Done: g.Count(i => i.IsCompleted)));
        }
    }

    public List<PlanItem> GetItemsInDateRange(DateTime start, DateTime end)
    {
        if (end < start) (start, end) = (end, start);
        string sStr = start.ToString("yyyy-MM-dd");
        string eStr = end.ToString("yyyy-MM-dd");

        EnsureDailyRepeatsForDate(DateTime.Today.ToString("yyyy-MM-dd"));

        lock (_sync)
        {
            return Store.Items
                .Where(x => string.CompareOrdinal(x.Date, sStr) >= 0 && string.CompareOrdinal(x.Date, eStr) <= 0)
                .OrderBy(x => x.Date)
                .ThenBy(x => x.IsCompleted ? 1 : 0)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .ToList();
        }
    }

    public void ExportDateRangeReport(DateTime start, DateTime end, string filePath)
    {
        if (end < start) (start, end) = (end, start);
        var items = GetItemsInDateRange(start, end);
        int total = items.Count;
        int done = items.Count(x => x.IsCompleted);
        int pending = total - done;
        int rate = total == 0 ? 0 : (int)Math.Round(done * 100.0 / total);

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".csv")
        {
            var sb = new StringBuilder();
            sb.AppendLine("日期,状态,计划内容,优先级,节点日期,距离节点,提醒时间,完成时间,备注");
            foreach (var it in items)
            {
                string status = it.IsCompleted ? "已完成" : "未完成";
                string pri = it.EffectivePriority switch
                {
                    TaskPriority.Urgent => "紧急",
                    TaskPriority.Important => "重要",
                    _ => "普通"
                };
                string doneAt = it.CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
                string msCountdown = it.HasMilestone ? it.DisplayMilestoneBadgeText : "";
                sb.AppendLine(string.Join(",",
                    CsvCell(it.Date),
                    CsvCell(status),
                    CsvCell(it.Title),
                    CsvCell(pri),
                    CsvCell(it.MilestoneDate ?? ""),
                    CsvCell(msCountdown),
                    CsvCell(it.ReminderTime ?? ""),
                    CsvCell(doneAt),
                    CsvCell(it.Note)));
            }
            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
            return;
        }

        // Default: Markdown report
        var md = new StringBuilder();
        md.AppendLine($"# 每日计划汇总报告（{start:yyyy-MM-dd} 至 {end:yyyy-MM-dd}）");
        md.AppendLine();
        md.AppendLine($"- 导出时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
        md.AppendLine($"- 总计划数：**{total}** 项");
        md.AppendLine($"- 已完成：**{done}** 项");
        md.AppendLine($"- 未完成：**{pending}** 项");
        md.AppendLine($"- 完成率：**{rate}%**");
        md.AppendLine();

        md.AppendLine("## 一、未完成计划");
        md.AppendLine();
        var pendingList = items.Where(x => !x.IsCompleted).ToList();
        if (pendingList.Count == 0)
        {
            md.AppendLine("_该时间段内全部计划已完成！_");
        }
        else
        {
            foreach (var g in pendingList.GroupBy(x => x.Date))
            {
                md.AppendLine($"### {g.Key}");
                foreach (var it in g)
                {
                    string pri = it.EffectivePriority == TaskPriority.Urgent ? "【紧急】" : it.EffectivePriority == TaskPriority.Important ? "【重要】" : "";
                    string ms = it.HasMilestone ? $" `{it.DisplayMilestoneBadgeText}`" : "";
                    string rem = string.IsNullOrWhiteSpace(it.ReminderTime) ? "" : $"（⏰ {it.ReminderTime}）";
                    string note = string.IsNullOrWhiteSpace(it.Note) ? "" : $" — {it.Note.Replace("\r", "").Replace("\n", " ")}";
                    md.AppendLine($"- [ ] {pri}{it.Title}{ms}{rem}{note}");
                }
                md.AppendLine();
            }
        }

        md.AppendLine("## 二、已完成计划");
        md.AppendLine();
        var doneList = items.Where(x => x.IsCompleted).ToList();
        if (doneList.Count == 0)
        {
            md.AppendLine("_该时间段内暂无已完成记录。_");
        }
        else
        {
            foreach (var g in doneList.GroupBy(x => x.Date))
            {
                md.AppendLine($"### {g.Key}");
                foreach (var it in g)
                {
                    string doneTime = it.CompletedAt.HasValue ? $"（✓ {it.CompletedAt:MM-dd HH:mm}）" : "";
                    string note = string.IsNullOrWhiteSpace(it.Note) ? "" : $" — {it.Note.Replace("\r", "").Replace("\n", " ")}";
                    md.AppendLine($"- [x] {it.Title}{doneTime}{note}");
                }
                md.AppendLine();
            }
        }

        File.WriteAllText(filePath, md.ToString(), new UTF8Encoding(true));
    }

    private static string CsvCell(string? value)
    {
        string v = (value ?? string.Empty).Replace("\r", "").Replace("\n", " ");
        return $"\"{v.Replace("\"", "\"\"")}\"";
    }

    private TaskPriority ResolveMilestoneDayPriority(TaskPriority basePriority, int daysLeft)
    {
        if (daysLeft <= 0)
            return TaskPriority.Urgent;
        if ((daysLeft == 1 || daysLeft == 3) && basePriority < TaskPriority.Important)
            return TaskPriority.Important;
        return basePriority;
    }

    private string? ResolveMilestoneDayReminder(string? requestedReminder, int daysLeft)
    {
        if (!string.IsNullOrWhiteSpace(requestedReminder))
            return requestedReminder.Trim();

        // 提前3天、提前1天、以及节点当天（紧急）强制确保有提醒时间
        if (daysLeft <= 0 || daysLeft == 1 || daysLeft == 3)
        {
            if (!string.IsNullOrWhiteSpace(Settings.DefaultReminderTime))
                return Settings.DefaultReminderTime.Trim();
            return "09:00";
        }

        return null;
    }

    public PlanItem AddItem(string dateStr, string title, string note, TaskPriority priority, string? reminderTime, bool isDailyRepeat, string? milestoneDate = null)
    {
        lock (_sync)
        {
            string? trimmedMilestone = string.IsNullOrWhiteSpace(milestoneDate) ? null : milestoneDate.Trim();

            if (!string.IsNullOrEmpty(trimmedMilestone) &&
                DateTime.TryParseExact(trimmedMilestone, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var endDt))
            {
                if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var startDt))
                {
                    startDt = DateTime.Today;
                }

                // 若当前选在节点当天且节点在未来，自动从今天起至节点每一天排入计划
                if (startDt.Date == endDt.Date && endDt.Date > DateTime.Today)
                {
                    startDt = DateTime.Today;
                }
                else if (startDt.Date > endDt.Date)
                {
                    startDt = endDt.Date;
                }

                if ((endDt.Date - startDt.Date).Days > 365)
                {
                    endDt = startDt.Date.AddDays(365);
                    trimmedMilestone = endDt.ToString("yyyy-MM-dd");
                }

                string groupId = Guid.NewGuid().ToString("N")[..12];
                PlanItem? primaryItem = null;

                for (var d = startDt.Date; d <= endDt.Date; d = d.AddDays(1))
                {
                    string dStr = d.ToString("yyyy-MM-dd");
                    int daysLeft = (endDt.Date - d.Date).Days;
                    int dayMaxOrder = Store.Items.Where(x => x.Date == dStr).Select(x => x.SortOrder).DefaultIfEmpty(-1).Max();

                    var dayItem = new PlanItem
                    {
                        Date = dStr,
                        Title = title,
                        Note = note,
                        Priority = ResolveMilestoneDayPriority(priority, daysLeft),
                        ReminderTime = ResolveMilestoneDayReminder(reminderTime, daysLeft),
                        IsDailyRepeat = false,
                        MilestoneDate = trimmedMilestone,
                        MilestoneGroupId = groupId,
                        SortOrder = dayMaxOrder + 1,
                        CreatedAt = DateTime.Now
                    };

                    Store.Items.Add(dayItem);
                    if (dStr == dateStr || primaryItem == null)
                        primaryItem = dayItem;
                }

                SavePlans();
                return primaryItem!;
            }

            int maxOrder = Store.Items.Where(x => x.Date == dateStr).Select(x => x.SortOrder).DefaultIfEmpty(-1).Max();
            string? sourceId = null;

            if (isDailyRepeat)
            {
                var tpl = new RepeatTemplate
                {
                    Title = title,
                    Note = note,
                    Priority = priority,
                    ReminderTime = reminderTime,
                    Enabled = true
                };
                Store.RepeatTemplates.Add(tpl);
                sourceId = tpl.Id;
            }

            var item = new PlanItem
            {
                Date = dateStr,
                Title = title,
                Note = note,
                Priority = priority,
                ReminderTime = string.IsNullOrWhiteSpace(reminderTime) ? null : reminderTime.Trim(),
                IsDailyRepeat = isDailyRepeat,
                RepeatSourceId = sourceId,
                SortOrder = maxOrder + 1,
                CreatedAt = DateTime.Now
            };

            Store.Items.Add(item);
            SavePlans();
            return item;
        }
    }

    public void UpdateItem(PlanItem edited)
    {
        lock (_sync)
        {
            var existing = Store.Items.FirstOrDefault(x => x.Id == edited.Id);
            if (existing == null) return;

            bool reminderChanged = existing.ReminderTime != edited.ReminderTime || existing.Date != edited.Date || existing.MilestoneDate != edited.MilestoneDate;

            existing.Date = edited.Date;
            existing.Title = edited.Title;
            existing.Note = edited.Note;
            existing.Priority = edited.Priority;
            existing.ReminderTime = string.IsNullOrWhiteSpace(edited.ReminderTime) ? null : edited.ReminderTime.Trim();
            if (reminderChanged)
            {
                existing.LastNotifiedKey = null;
                existing.LastMilestoneMorningNotifiedDate = null;
                existing.SnoozeUntil = null;
            }

            string? trimmedMilestone = string.IsNullOrWhiteSpace(edited.MilestoneDate) ? null : edited.MilestoneDate.Trim();
            if (!string.IsNullOrEmpty(trimmedMilestone) &&
                DateTime.TryParseExact(trimmedMilestone, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var endDt))
            {
                if (string.IsNullOrEmpty(existing.MilestoneGroupId))
                    existing.MilestoneGroupId = Guid.NewGuid().ToString("N")[..12];

                string gid = existing.MilestoneGroupId!;
                string mStr = endDt.ToString("yyyy-MM-dd");
                existing.MilestoneDate = mStr;

                // 清理超过新节点日期的未完成后续分身
                Store.Items.RemoveAll(x =>
                    x.MilestoneGroupId == gid &&
                    x.Id != existing.Id &&
                    !x.IsCompleted &&
                    string.CompareOrdinal(x.Date, mStr) > 0);

                // 同步该节点系列所有条目的标题、备注、节点日与提醒
                var groupItems = Store.Items.Where(x => x.MilestoneGroupId == gid).ToList();
                var existingDates = new HashSet<string>(groupItems.Select(x => x.Date));
                foreach (var gi in groupItems)
                {
                    gi.Title = existing.Title;
                    gi.Note = existing.Note;
                    gi.MilestoneDate = mStr;
                    int dLeft = gi.DaysUntilMilestone ?? 0;
                    gi.Priority = ResolveMilestoneDayPriority(edited.Priority, dLeft);
                    gi.ReminderTime = ResolveMilestoneDayReminder(edited.ReminderTime, dLeft);
                    if (reminderChanged)
                    {
                        gi.LastNotifiedKey = null;
                        gi.LastMilestoneMorningNotifiedDate = null;
                    }
                }

                // 补齐从起始日到节点日期之间缺失的每一天计划
                if (!DateTime.TryParseExact(existing.Date, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var startDt))
                {
                    startDt = DateTime.Today;
                }
                if (startDt.Date == endDt.Date && endDt.Date > DateTime.Today)
                    startDt = DateTime.Today;

                if ((endDt.Date - startDt.Date).Days <= 365)
                {
                    for (var d = startDt.Date; d <= endDt.Date; d = d.AddDays(1))
                    {
                        string dStr = d.ToString("yyyy-MM-dd");
                        if (existingDates.Contains(dStr)) continue;

                        int daysLeft = (endDt.Date - d.Date).Days;
                        int dayMaxOrder = Store.Items.Where(x => x.Date == dStr).Select(x => x.SortOrder).DefaultIfEmpty(-1).Max();
                        Store.Items.Add(new PlanItem
                        {
                            Date = dStr,
                            Title = existing.Title,
                            Note = existing.Note,
                            Priority = ResolveMilestoneDayPriority(edited.Priority, daysLeft),
                            ReminderTime = ResolveMilestoneDayReminder(edited.ReminderTime, daysLeft),
                            MilestoneDate = mStr,
                            MilestoneGroupId = gid,
                            SortOrder = dayMaxOrder + 1,
                            CreatedAt = DateTime.Now
                        });
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(existing.MilestoneGroupId))
                {
                    string oldGid = existing.MilestoneGroupId;
                    Store.Items.RemoveAll(x => x.MilestoneGroupId == oldGid && x.Id != existing.Id && !x.IsCompleted);
                    existing.MilestoneGroupId = null;
                }
                existing.MilestoneDate = null;
            }

            // Sync daily repeat template
            if (edited.IsDailyRepeat && string.IsNullOrEmpty(existing.MilestoneDate))
            {
                existing.IsDailyRepeat = true;
                if (string.IsNullOrEmpty(existing.RepeatSourceId))
                {
                    var tpl = new RepeatTemplate
                    {
                        Title = existing.Title,
                        Note = existing.Note,
                        Priority = existing.Priority,
                        ReminderTime = existing.ReminderTime,
                        Enabled = true
                    };
                    Store.RepeatTemplates.Add(tpl);
                    existing.RepeatSourceId = tpl.Id;
                }
                else
                {
                    var tpl = Store.RepeatTemplates.FirstOrDefault(t => t.Id == existing.RepeatSourceId);
                    if (tpl != null)
                    {
                        tpl.Title = existing.Title;
                        tpl.Note = existing.Note;
                        tpl.Priority = existing.Priority;
                        tpl.ReminderTime = existing.ReminderTime;
                        tpl.Enabled = true;
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(existing.RepeatSourceId))
                {
                    Store.RepeatTemplates.RemoveAll(t => t.Id == existing.RepeatSourceId);
                    existing.RepeatSourceId = null;
                }
                existing.IsDailyRepeat = false;
            }

            SavePlans();
        }
    }

    public void ToggleComplete(string id, bool completed)
    {
        lock (_sync)
        {
            var item = Store.Items.FirstOrDefault(x => x.Id == id);
            if (item == null) return;
            if (completed && Settings.AutoDeleteCompleted)
            {
                Store.Items.Remove(item);
                SavePlans();
                return;
            }
            item.IsCompleted = completed;
            item.CompletedAt = completed ? DateTime.Now : null;
            if (completed)
            {
                item.SnoozeUntil = null;
                // 若在节点当天（或超期）打勾完成，同步将该节点之前日期的未勾记录一并标为完成
                if (!string.IsNullOrEmpty(item.MilestoneGroupId) && (item.DaysUntilMilestone ?? 1) <= 0)
                {
                    foreach (var prev in Store.Items.Where(x => x.MilestoneGroupId == item.MilestoneGroupId && !x.IsCompleted))
                    {
                        prev.IsCompleted = true;
                        prev.CompletedAt = DateTime.Now;
                        prev.SnoozeUntil = null;
                    }
                }
            }
            SavePlans();
        }
    }

    public void ToggleMilestoneGroupComplete(string groupId, bool completed)
    {
        if (string.IsNullOrWhiteSpace(groupId)) return;
        lock (_sync)
        {
            var list = Store.Items.Where(x => x.MilestoneGroupId == groupId).ToList();
            if (list.Count == 0) return;

            if (completed && Settings.AutoDeleteCompleted)
            {
                Store.Items.RemoveAll(x => x.MilestoneGroupId == groupId);
                SavePlans();
                return;
            }

            foreach (var it in list)
            {
                it.IsCompleted = completed;
                it.CompletedAt = completed ? DateTime.Now : null;
                if (completed) it.SnoozeUntil = null;
            }
            SavePlans();
        }
    }

    public void DeleteMilestoneGroup(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId)) return;
        lock (_sync)
        {
            int removed = Store.Items.RemoveAll(x => x.MilestoneGroupId == groupId);
            if (removed > 0)
                SavePlans();
        }
    }

    public void DeleteItem(string id, bool removeRepeatTemplate = true)
    {
        lock (_sync)
        {
            var item = Store.Items.FirstOrDefault(x => x.Id == id);
            if (item == null) return;

            if (removeRepeatTemplate && !string.IsNullOrEmpty(item.RepeatSourceId))
            {
                Store.RepeatTemplates.RemoveAll(t => t.Id == item.RepeatSourceId);
            }

            Store.Items.Remove(item);
            SavePlans();
        }
    }

    public int ClearCompletedForDate(string dateStr)
    {
        lock (_sync)
        {
            int count = Store.Items.RemoveAll(x => x.Date == dateStr && x.IsCompleted);
            if (count > 0) SavePlans();
            return count;
        }
    }

    public void MoveItem(string dateStr, string id, int delta)
    {
        lock (_sync)
        {
            var list = Store.Items
                .Where(x => x.Date == dateStr)
                .OrderBy(x => Settings.CompletedAtBottom ? (x.IsCompleted ? 1 : 0) : 0)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .ToList();

            int idx = list.FindIndex(x => x.Id == id);
            int targetIdx = idx + delta;
            if (idx < 0 || targetIdx < 0 || targetIdx >= list.Count)
                return;

            var moved = list[idx];
            list.RemoveAt(idx);
            list.Insert(targetIdx, moved);

            for (int i = 0; i < list.Count; i++)
                list[i].SortOrder = i;

            SavePlans();
        }
    }

    public void SnoozeItem(string id, int minutes)
    {
        lock (_sync)
        {
            var item = Store.Items.FirstOrDefault(x => x.Id == id);
            if (item == null) return;
            item.SnoozeUntil = DateTime.Now.AddMinutes(minutes);
            SavePlans();
        }
    }

    public List<PlanItem> GetDueReminders(DateTime now)
    {
        lock (_sync)
        {
            string todayStr = now.ToString("yyyy-MM-dd");
            var due = new List<PlanItem>();
            bool changed = false;

            foreach (var item in Store.Items)
            {
                if (item.IsCompleted) continue;

                if (item.SnoozeUntil.HasValue && now >= item.SnoozeUntil.Value)
                {
                    item.SnoozeUntil = null;
                    item.LastNotifiedKey = now.ToString("yyyy-MM-dd HH:mm");
                    due.Add(item);
                    changed = true;
                    continue;
                }

                if (item.Date != todayStr)
                    continue;

                // 节点关键提醒日（提前3天、提前1天、以及节点当天紧急提醒）：
                // 1) 节点当天（daysLeft <= 0）：首次进入当天或到达 09:00 即触发紧急提醒；
                // 2) 提前3天 / 提前1天：到达早间提醒时间（默认 09:00）即触发一次节点预警；
                // 3) 若还配置了晚间/自定义提醒时间（如 18:00），到点未完成会再次提醒！
                if (item.IsMilestoneKeyAlertDay)
                {
                    int daysLeft = item.DaysUntilMilestone ?? 0;
                    string morningClock = !string.IsNullOrWhiteSpace(Settings.RolloverReminderTime)
                        ? Settings.RolloverReminderTime.Trim()
                        : "09:00";

                    bool shouldFireMilestoneAlert = false;
                    if (item.LastMilestoneMorningNotifiedDate != todayStr)
                    {
                        if (daysLeft <= 0)
                        {
                            // 节点当天即刻/全天紧急提醒（新建后满 2 秒或已到当天即提醒）
                            if ((now - item.CreatedAt).TotalSeconds >= 2)
                                shouldFireMilestoneAlert = true;
                        }
                        else if (DateTime.TryParseExact($"{todayStr} {morningClock}", "yyyy-MM-dd HH:mm",
                            null, System.Globalization.DateTimeStyles.None, out var morningTarget))
                        {
                            if (now >= morningTarget)
                                shouldFireMilestoneAlert = true;
                        }
                    }

                    if (shouldFireMilestoneAlert)
                    {
                        item.LastMilestoneMorningNotifiedDate = todayStr;
                        // 如果项目的定时提醒时间恰好已经到了或与当前时间相同，同步记录 LastNotifiedKey 避免同一时刻连弹两次
                        if (!string.IsNullOrWhiteSpace(item.ReminderTime) &&
                            DateTime.TryParseExact($"{item.Date} {item.ReminderTime}", "yyyy-MM-dd HH:mm",
                                null, System.Globalization.DateTimeStyles.None, out var remTarget) &&
                            Math.Abs((now - remTarget).TotalMinutes) <= 5)
                        {
                            item.LastNotifiedKey = $"{item.Date} {item.ReminderTime}";
                        }
                        due.Add(item);
                        changed = true;
                        continue;
                    }
                }

                if (string.IsNullOrWhiteSpace(item.ReminderTime))
                    continue;

                if (DateTime.TryParseExact($"{item.Date} {item.ReminderTime}", "yyyy-MM-dd HH:mm",
                    null, System.Globalization.DateTimeStyles.None, out var target))
                {
                    double elapsedMinutes = (now - target).TotalMinutes;
                    string notifyKey = $"{item.Date} {item.ReminderTime}";
                    double maxCatchup = item.IsMilestoneKeyAlertDay ? 720 : 15;
                    if (elapsedMinutes >= 0 && elapsedMinutes <= maxCatchup && item.LastNotifiedKey != notifyKey)
                    {
                        item.LastNotifiedKey = notifyKey;
                        due.Add(item);
                        changed = true;
                    }
                }
            }

            if (changed)
                SavePlans();

            return due;
        }
    }
}
