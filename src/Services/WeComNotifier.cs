using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DailyPlanBoard.Models;

namespace DailyPlanBoard.Services;

public static class WeComNotifier
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public static string? ResolveWebhookUrl(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        string trimmed = input.Trim();

        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        // Allow pasting just the key UUID
        if (!trimmed.Contains('/') && !trimmed.Contains(' '))
        {
            return $"https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key={Uri.EscapeDataString(trimmed)}";
        }

        return null;
    }

    public static async Task<(bool Ok, string Message)> SendMarkdownAsync(string? webhookOrKey, string markdownContent)
    {
        string? url = ResolveWebhookUrl(webhookOrKey);
        if (string.IsNullOrEmpty(url))
            return (false, L10n.T("WeComNeedKey"));

        try
        {
            var payload = new
            {
                msgtype = "markdown",
                markdown = new
                {
                    content = markdownContent
                }
            };

            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await Http.PostAsync(url, content).ConfigureAwait(false);
            string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                return (false, $"HTTP {(int)resp.StatusCode}");

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errcode", out var errProp) && errProp.GetInt32() == 0)
                return (true, "OK");

            string errMsg = doc.RootElement.TryGetProperty("errmsg", out var msgProp)
                ? msgProp.GetString() ?? body
                : body;
            return (false, errMsg);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static Task<(bool Ok, string Message)> SendTaskReminderAsync(string? webhookOrKey, PlanItem item)
    {
        string priTag = item.EffectivePriority switch
        {
            TaskPriority.Urgent => "<font color=\"warning\">【紧急】</font>",
            TaskPriority.Important => "<font color=\"info\">【重要】</font>",
            _ => ""
        };

        var sb = new StringBuilder();
        if (item.HasMilestone && item.DaysUntilMilestone.HasValue)
        {
            int dLeft = item.DaysUntilMilestone.Value;
            string head = dLeft <= 0
                ? "🚨 计划节点【今日截止 · 紧急提醒】"
                : dLeft == 1
                    ? "🔥 计划节点【提前 1 天提醒】"
                    : dLeft == 3
                        ? "⚠️ 计划节点【提前 3 天提醒】"
                        : $"🎯 计划节点倒计时提醒（剩 {dLeft} 天）";
            sb.AppendLine($"### {head} {priTag}");
            sb.AppendLine($"> **计划**：{EscapeMd(item.Title)}");
            sb.AppendLine($"> **节点截止日**：<font color=\"warning\">{item.MilestoneDate}</font>（{EscapeMd(item.DisplayMilestoneBadgeText)}）");
            sb.AppendLine($"> **当前日期**：{item.Date}  {(string.IsNullOrWhiteSpace(item.ReminderTime) ? "" : item.ReminderTime)}");
        }
        else
        {
            sb.AppendLine($"### ⏰ 每日计划提醒 {priTag}");
            sb.AppendLine($"> **计划**：{EscapeMd(item.Title)}");
            sb.AppendLine($"> **日期**：{item.Date}  {(string.IsNullOrWhiteSpace(item.ReminderTime) ? "" : item.ReminderTime)}");
        }

        if (!string.IsNullOrWhiteSpace(item.Note))
            sb.AppendLine($"> **备注**：{EscapeMd(item.Note)}");

        return SendMarkdownAsync(webhookOrKey, sb.ToString());
    }

    public static Task<(bool Ok, string Message)> SendDailySummaryAsync(string? webhookOrKey, string dateStr, List<PlanItem> items)
    {
        int total = items.Count;
        int done = items.Count(x => x.IsCompleted);
        int pending = total - done;

        var sb = new StringBuilder();
        sb.AppendLine($"### 📋 每日计划汇总 ({dateStr})");
        sb.AppendLine($"共 **{total}** 项 ｜ 已完成 <font color=\"info\">{done}</font> ｜ 未完成 <font color=\"warning\">{pending}</font>");

        if (pending > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**待完成：**");
            foreach (var x in items.Where(i => !i.IsCompleted).Take(20))
            {
                string ms = x.HasMilestone ? $" `{EscapeMd(x.DisplayMilestoneBadgeText)}`" : "";
                string time = string.IsNullOrWhiteSpace(x.ReminderTime) ? "" : $" `⏰{x.ReminderTime}`";
                sb.AppendLine($"- [ ] {EscapeMd(x.Title)}{ms}{time}");
            }
        }

        if (done > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**已完成：**");
            foreach (var x in items.Where(i => i.IsCompleted).Take(15))
            {
                sb.AppendLine($"- ✓ ~~{EscapeMd(x.Title)}~~");
            }
        }

        return SendMarkdownAsync(webhookOrKey, sb.ToString());
    }

    public static Task<(bool Ok, string Message)> SendTestAsync(string? webhookOrKey)
    {
        string msg = $"### ✅ 每日计划看板连接测试\n> 来自本地 **DailyPlanBoard** 的提醒推送测试（{DateTime.Now:yyyy-MM-dd HH:mm:ss}）。\n> 到期提醒与每日汇总可正常推送至本群！";
        return SendMarkdownAsync(webhookOrKey, msg);
    }

    private static string EscapeMd(string text)
        => text.Replace("\r", "").Replace("\n", " ");
}
