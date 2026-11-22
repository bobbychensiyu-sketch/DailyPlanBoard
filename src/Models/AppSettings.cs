namespace DailyPlanBoard.Models;

public enum WindowPinMode
{
    Desktop = 0, // 常驻桌面（贴在桌面层，免疫 Win+D）
    TopMost = 1, // 窗口置顶（悬浮于所有窗口之上）
    Normal = 2   // 普通窗口
}

public enum AppThemeMode
{
    WarmPaper = 0,  // 暖纸护眼
    CleanLight = 1, // 素白极简
    Forest = 2,     // 松林清雅
    Sunset = 3,     // 晚霞微醺
    DarkSlate = 4   // 墨夜暗色
}

public enum DockEdgeSide
{
    None = 0,
    Left = 1,
    Right = 2
}

public enum AppLanguage
{
    Zh = 0,
    En = 1,
    Fr = 2
}

public class AppSettings
{
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public double WindowWidth { get; set; } = 350;
    public double WindowHeight { get; set; } = 540;
    public WindowPinMode PinMode { get; set; } = WindowPinMode.TopMost;
    public AppThemeMode Theme { get; set; } = AppThemeMode.WarmPaper;
    public double WindowOpacity { get; set; } = 0.96;
    public double FontScale { get; set; } = 1.0; // 0.92 | 1.0 | 1.1

    // 吸附与胶囊收起（借鉴 PaperTodo）
    public bool EdgeSnapEnabled { get; set; } = true;
    public bool AutoCollapseOnEdge { get; set; } = true;
    public bool HoverExpandEnabled { get; set; } = true;
    public bool IsCollapsedToEdge { get; set; } = false;
    public DockEdgeSide DockSide { get; set; } = DockEdgeSide.Right;
    public double CapsuleTop { get; set; } = 160;

    // 待办与智能提醒
    public bool SoundEnabled { get; set; } = true;
    public bool CompletedAtBottom { get; set; } = true;
    public bool AutoDeleteCompleted { get; set; } = false;

    public bool DefaultReminderEnabled { get; set; } = true;
    public string DefaultReminderTime { get; set; } = "18:00";

    public bool AutoRolloverNextDay { get; set; } = true;
    public bool RolloverReminderEnabled { get; set; } = true;
    public string RolloverReminderTime { get; set; } = "09:00";

    // 企业微信机器人推送（仅本地保存，不外泄）
    public bool WeComBotEnabled { get; set; } = false;
    public string WeComWebhookOrKey { get; set; } = string.Empty;

    public bool ShowInTaskbar { get; set; } = false;
    public bool AutoStart { get; set; } = false;
    public bool AvoidFullscreen { get; set; } = true;
    public bool HideFromCapture { get; set; } = false;
    public string GlobalHotkey { get; set; } = "Alt+Q"; // None | Alt+Q | Ctrl+Shift+D
    public AppLanguage Language { get; set; } = AppLanguage.Zh;
}
