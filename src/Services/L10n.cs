using System;
using System.Collections.Generic;
using DailyPlanBoard.Models;

namespace DailyPlanBoard.Services;

public static class L10n
{
    public static AppLanguage Current { get; set; } = AppLanguage.Zh;

    private static readonly Dictionary<string, (string Zh, string En, string Fr)> Dict = new()
    {
        // App & MainWindow Header
        ["AppTitle"] = ("每日计划看板", "Daily Plan Board", "Plan du jour"),
        ["HeaderTitle"] = ("每日计划", "Daily Plan", "Plan du jour"),
        ["ModeDesktop"] = ("常驻桌面", "Desktop", "Bureau"),
        ["ModeTopMost"] = ("已置顶", "Pinned", "Épinglé"),
        ["ModeNormal"] = ("普通", "Normal", "Normal"),
        ["TipPinMode"] = ("切换窗口层级：窗口置顶 / 常驻桌面 / 普通窗口", "Switch window mode: Pinned / Desktop / Normal", "Changer le mode : Épinglé / Bureau / Normal"),
        ["TipEdgeDock"] = ("吸附贴边并收起为胶囊 (Ctrl+W)", "Dock to edge capsule (Ctrl+W)", "Réduire sur le bord (Ctrl+W)"),
        ["TipTheme"] = ("切换主题：暖纸 / 素白 / 松林 / 晚霞 / 墨夜", "Switch theme: Warm / Light / Forest / Sunset / Dark", "Changer le thème : Papier / Clair / Forêt / Soir / Sombre"),
        ["TipSettings"] = ("打开设置（语言、吸附、外观、快捷键等）", "Settings (Language, Docking, Theme, Hotkey...)", "Paramètres (Langue, Ancrage, Thème, Raccourci...)"),
        ["TipHideTray"] = ("隐藏到系统托盘（后台继续准点提醒）", "Hide to system tray (reminders stay active)", "Masquer dans la barre système (rappels actifs)"),

        // Date & Progress
        ["TipPrevDay"] = ("前一天", "Previous day", "Jour précédent"),
        ["TipNextDay"] = ("后一天", "Next day", "Jour suivant"),
        ["TipJumpToday"] = ("点击返回今天", "Click to return to today", "Cliquer pour revenir à aujourd'hui"),
        ["BtnToday"] = ("回今天", "Today", "Auj."),
        ["SuffixToday"] = (" · 今天", " · Today", " · Aujourd'hui"),
        ["SuffixYesterday"] = (" · 昨天", " · Yesterday", " · Hier"),
        ["SuffixTomorrow"] = (" · 明天", " · Tomorrow", " · Demain"),
        ["RolloverText"] = ("过去有 {0} 项未完成计划", "{0} unfinished item(s) from past", "{0} tâche(s) non terminée(s)"),
        ["BtnMoveToday"] = ("移至今天", "Move to Today", "Reporter"),

        // Quick Add
        ["QuickAddPlaceholder"] = ("写下计划，回车添加（如：15:30 开会）", "Add a plan, press Enter (e.g. 15:30 Meeting)", "Ajouter une tâche, Entrée (ex: 15:30 Réunion)"),
        ["BtnAdd"] = ("+ 添加", "+ Add", "+ Ajouter"),
        ["PriNormal"] = ("● 普通", "● Normal", "● Normal"),
        ["PriImportant"] = ("● 重要", "● Important", "● Important"),
        ["PriUrgent"] = ("● 紧急", "● Urgent", "● Urgent"),
        ["TipQuickPri"] = ("点击切换优先级：普通 / 重要 / 紧急", "Click to cycle priority: Normal / Important / Urgent", "Cliquer pour changer la priorité"),
        ["BtnRemind"] = ("⏰ 提醒", "⏰ Remind", "⏰ Rappel"),
        ["TipQuickRemind"] = ("快捷设置提醒时间", "Quick reminder preset", "Rappel rapide"),
        ["BtnDaily"] = ("🔁 每日", "🔁 Daily", "🔁 Quotidien"),
        ["TipQuickRepeat"] = ("设为每日重复任务", "Repeat every day", "Répéter chaque jour"),

        // Reminder Presets
        ["In15Min"] = ("15 分钟后", "In 15 min", "Dans 15 min"),
        ["In30Min"] = ("30 分钟后", "In 30 min", "Dans 30 min"),
        ["In1Hour"] = ("1 小时后", "In 1 hour", "Dans 1 heure"),
        ["At0900"] = ("上午 09:00", "09:00 AM", "09:00"),
        ["At1030"] = ("上午 10:30", "10:30 AM", "10:30"),
        ["At1400"] = ("下午 14:00", "02:00 PM", "14:00"),
        ["At1600"] = ("下午 16:00", "04:00 PM", "16:00"),
        ["At1800"] = ("傍晚 18:00", "06:00 PM", "18:00"),
        ["At2100"] = ("晚上 21:00", "09:00 PM", "21:00"),
        ["ClearReminder"] = ("清除提醒", "Clear reminder", "Effacer le rappel"),

        // Filter & Empty State
        ["FilterAll"] = ("全部", "All", "Tout"),
        ["FilterActive"] = ("待办", "To-Do", "À faire"),
        ["FilterDone"] = ("已完成", "Done", "Fait"),
        ["ClearCompleted"] = ("清理已完成", "Clear Done", "Effacer faits"),
        ["TipResize"] = ("拖拽调整看板大小", "Drag to resize board", "Glisser pour redimensionner"),
        ["EmptyActive"] = ("太棒了，当前没有待办事项！", "Great! Nothing left to do!", "Bravo, aucune tâche en attente !"),
        ["EmptyDone"] = ("还没有已完成的计划", "No completed plans yet", "Aucune tâche terminée"),
        ["EmptyAll"] = ("暂无计划，在上方添加第一项吧", "No plans yet. Add one above!", "Aucun plan. Ajoutez-en un ci-dessus !"),

        // Task Card & Context Menu
        ["TipCheckDone"] = ("打勾完成", "Mark as done", "Marquer comme fait"),
        ["TipUncheck"] = ("取消完成", "Mark as undone", "Marquer comme à faire"),
        ["TipEdit"] = ("编辑", "Edit", "Modifier"),
        ["TipDelete"] = ("删除", "Delete", "Supprimer"),
        ["CtxEdit"] = ("✎ 编辑计划", "✎ Edit Plan", "✎ Modifier"),
        ["CtxMarkDone"] = ("✓ 标记完成", "✓ Mark Done", "✓ Terminer"),
        ["CtxMarkUndone"] = ("↩ 取消完成", "↩ Mark Undone", "↩ Annuler"),
        ["CtxMoveUp"] = ("↑ 上移", "↑ Move Up", "↑ Monter"),
        ["CtxMoveDown"] = ("↓ 下移", "↓ Move Down", "↓ Descendre"),
        ["CtxDelete"] = ("✕ 删除此项", "✕ Delete", "✕ Supprimer"),
        ["SnoozeTag"] = ("(延后)", "(Snoozed)", "(Reporté)"),
        ["DailyBadge"] = ("🔁 每日", "🔁 Daily", "🔁 Quotidien"),

        // Edit Dialog
        ["EditTitle"] = ("编辑计划", "Edit Plan", "Modifier la tâche"),
        ["LabelPlanContent"] = ("计划内容", "Plan Title", "Intitulé"),
        ["LabelNoteOpt"] = ("备注（可选）", "Note (optional)", "Note (facultatif)"),
        ["LabelPlanDate"] = ("所属日期", "Date", "Date"),
        ["LabelPriority"] = ("优先级", "Priority", "Priorité"),
        ["OptPri0"] = ("普通", "Normal", "Normal"),
        ["OptPri1"] = ("重要", "Important", "Important"),
        ["OptPri2"] = ("紧急", "Urgent", "Urgent"),
        ["LabelReminderFmt"] = ("定时提醒（24小时制，如 14:30，留空不提醒）", "Reminder (24h format, e.g. 14:30, optional)", "Rappel (format 24h, ex: 14:30, facultatif)"),
        ["BtnClear"] = ("清除", "Clear", "Effacer"),
        ["Quick+15"] = ("+15分", "+15m", "+15m"),
        ["Quick+30"] = ("+30分", "+30m", "+30m"),
        ["Quick+60"] = ("+1小时", "+1h", "+1h"),
        ["CheckDailyRepeat"] = ("设为每日重复任务（每天自动出现在当日计划）", "Repeat daily (auto-appears each day)", "Répéter chaque jour automatiquement"),
        ["BtnCancel"] = ("取消", "Cancel", "Annuler"),
        ["BtnSave"] = ("保存", "Save", "Enregistrer"),
        ["ErrEmptyTitle"] = ("请输入计划内容。", "Please enter a plan title.", "Veuillez saisir un intitulé."),
        ["ErrBadTime"] = ("提醒时间格式不正确，请使用 24 小时制（如 09:30）。", "Invalid time format. Use 24h format (e.g. 09:30).", "Format invalide. Utilisez le format 24h (ex: 09:30)."),

        // Reminder Popup
        ["PopupTitle"] = ("⏰  每日计划提醒", "⏰  Daily Plan Reminder", "⏰  Rappel du plan"),
        ["BtnSnooze10"] = ("延后 10 分", "Snooze 10m", "Reporter 10m"),
        ["BtnGotIt"] = ("知道了", "Dismiss", "OK"),
        ["BtnComplete"] = ("✓ 标记完成", "✓ Complete", "✓ Terminer"),
        ["BalloonTitle"] = ("每日计划提醒", "Daily Plan Reminder", "Rappel du plan"),
        ["TestBalloonTitle"] = ("每日计划提醒（测试）", "Daily Plan Reminder (Test)", "Rappel du plan (Test)"),
        ["TestBalloonBody"] = ("准点提醒功能正常！即使看板隐藏到托盘，到期也会准时提醒。", "Reminders work even when minimized to tray!", "Les rappels fonctionnent même réduit dans la barre !"),
        ["TestPopupTitle"] = ("提醒测试：每日计划看板工作正常", "Reminder Test: Everything works!", "Test de rappel : Tout fonctionne !"),
        ["TestPopupNote"] = ("点击右侧按钮可快速标记完成或延后 10 分钟。", "Use buttons below to complete or snooze 10 minutes.", "Utilisez les boutons ci-dessous pour terminer ou reporter."),

        // Edge Capsule
        ["CapsuleTooltip"] = ("每日计划：待办 {0} / 共 {1} 项\n悬停预览 · 单击固定展开 · 上下拖动调节位置", "Daily Plan: {0} pending / {1} total\nHover to peek · Click to pin · Drag to move", "Plan du jour : {0} à faire / {1} total\nSurvoler pour voir · Cliquer pour fixer · Glisser"),
        ["CapPinExpand"] = ("📌 展开并固定看板", "📌 Expand & Pin Board", "📌 Déplier et fixer"),
        ["CapDockLeft"] = ("◧ 吸附到屏幕左侧", "◧ Dock to Left Edge", "◧ Ancrer à gauche"),
        ["CapDockRight"] = ("◨ 吸附到屏幕右侧", "◨ Dock to Right Edge", "◨ Ancrer à droite"),
        ["CapSettings"] = ("⚙ 打开设置...", "⚙ Settings...", "⚙ Paramètres..."),
        ["CapHideTray"] = ("— 隐藏到系统托盘", "— Hide to Tray", "— Masquer dans la barre"),
        ["CapExit"] = ("退出每日计划看板", "Exit Daily Plan Board", "Quitter Plan du jour"),

        // Tray Menu
        ["TrayShowHide"] = ("显示 / 隐藏看板", "Show / Hide Board", "Afficher / Masquer"),
        ["TrayCollapseEdge"] = ("◧ 贴边收起为胶囊", "◧ Dock to Edge Capsule", "◧ Réduire sur le bord"),
        ["TrayModeDesktop"] = ("🖥️ 常驻桌面（贴壁纸层）", "🖥️ Desktop Layer (Win+D safe)", "🖥️ Sur le bureau"),
        ["TrayModeTopMost"] = ("📌 窗口置顶（Always on Top）", "📌 Always on Top", "📌 Toujours au premier plan"),
        ["TrayModeNormal"] = ("📄 普通悬浮窗口", "📄 Normal Window", "📄 Fenêtre normale"),
        ["TrayLanguage"] = ("🌐 语言 / Language / Langue", "🌐 Language / 语言 / Langue", "🌐 Langue / Language / 语言"),
        ["TrayAutoStart"] = ("开机自动启动", "Launch at Startup", "Lancer au démarrage"),
        ["TrayTestRemind"] = ("测试提醒效果", "Test Reminder", "Tester le rappel"),
        ["TrayOpenData"] = ("打开数据文件夹", "Open Data Folder", "Ouvrir le dossier des données"),
        ["TrayExit"] = ("退出程序", "Exit", "Quitter"),

        // Settings Window
        ["SettingsWinTitle"] = ("⚙  看板设置", "⚙  Board Settings", "⚙  Paramètres"),
        ["SecLanguage"] = ("语言 / Language / Langue", "Language / 语言 / Langue", "Langue / Language / 语言"),
        ["SecDockAndLayer"] = ("吸附与窗口层级", "Docking & Window Layer", "Ancrage & Fenêtre"),
        ["LabelWindowLayer"] = ("窗口层级", "Window Layer", "Niveau de fenêtre"),
        ["OptLayer0"] = ("🖥️ 常驻桌面（免疫 Win+D）", "🖥️ Desktop Layer (Win+D safe)", "🖥️ Bureau (résiste à Win+D)"),
        ["OptLayer1"] = ("📌 窗口置顶（Always on Top）", "📌 Always on Top", "📌 Toujours au premier plan"),
        ["OptLayer2"] = ("📄 普通悬浮窗口", "📄 Normal Window", "📄 Fenêtre normale"),
        ["ChkEdgeSnap"] = ("屏幕边缘磁吸（拖动靠边自动贴齐）", "Magnetic screen edge snapping", "Aimantation aux bords de l'écran"),
        ["ChkAutoCollapse"] = ("贴边自动收起为胶囊（不占桌面空间）", "Auto-collapse to edge capsule", "Réduire automatiquement sur le bord"),
        ["ChkHoverExpand"] = ("鼠标悬停胶囊自动滑出展开", "Hover capsule to slide out board", "Survoler la pilule pour déplier"),
        ["ChkAvoidFullscreen"] = ("全屏程序自动避让（看视频/游戏时不遮挡）", "Auto-hide over fullscreen apps", "Masquer devant le plein écran"),
        ["ChkHideCapture"] = ("在截图和录屏中隐藏看板内容（隐私保护）", "Hide board in screenshots & recording", "Masquer dans captures et vidéos"),

        ["SecAppearance"] = ("外观与配色", "Appearance & Theme", "Apparence & Thème"),
        ["LabelThemePalette"] = ("主题配色", "Color Theme", "Thème de couleur"),
        ["Theme0"] = ("暖纸", "Warm", "Papier"),
        ["Theme1"] = ("素白", "Light", "Clair"),
        ["Theme2"] = ("松林", "Forest", "Forêt"),
        ["Theme3"] = ("晚霞", "Sunset", "Soir"),
        ["Theme4"] = ("墨夜", "Dark", "Sombre"),
        ["LabelOpacityFmt"] = ("窗口透明度：{0}%", "Window Opacity: {0}%", "Opacité : {0}%"),
        ["LabelFontScale"] = ("正文字号", "Font Size", "Taille du texte"),
        ["Font0"] = ("紧凑 (92%)", "Compact (92%)", "Compact (92%)"),
        ["Font1"] = ("标准 (100%)", "Standard (100%)", "Standard (100%)"),
        ["Font2"] = ("舒适 (110%)", "Large (110%)", "Grand (110%)"),

        ["SecBehavior"] = ("待办、提醒与常规", "Tasks, Reminders & General", "Tâches, Rappels & Général"),
        ["ChkCompletedBottom"] = ("已完成事项自动沉底", "Move completed items to bottom", "Déplacer les tâches faites en bas"),
        ["ChkAutoDelete"] = ("打勾完成后自动清理（不保留已完成记录）", "Auto-delete item when checked", "Supprimer auto. une fois cochée"),
        ["ChkSound"] = ("到期提醒时播放系统提示音", "Play sound on reminder", "Jouer un son lors du rappel"),
        ["ChkTaskbar"] = ("在任务栏和 Alt+Tab 中显示窗口图标", "Show in Taskbar and Alt+Tab", "Afficher dans la barre des tâches"),
        ["ChkAutoStart"] = ("开机自动启动每日计划看板", "Launch automatically on Windows startup", "Lancer au démarrage de Windows"),
        ["LabelHotkey"] = ("全局呼出/收起快捷键", "Global Toggle Hotkey", "Raccourci global"),
        ["HotkeyHint"] = ("点击输入框直接按下组合键（如 Alt+Q、Ctrl+Alt+D）", "Click box and press key combo (e.g. Alt+Q, Ctrl+Alt+D)", "Cliquez et tapez un raccourci (ex: Alt+Q, Ctrl+Alt+D)"),
        ["HotkeyPressPrompt"] = ("请按下组合键...", "Press key combo...", "Appuyez sur une combinaison..."),
        ["HotkeyNone"] = ("禁用", "Disabled", "Désactivé"),
        ["HotkeyConflict"] = ("快捷键可能已被其他程序占用，请换一个组合。", "Hotkey may be in use by another app.", "Ce raccourci est déjà utilisé."),
        ["SecShortcuts"] = ("看板内快捷键（聚焦窗口时可用）", "Board Shortcuts", "Raccourcis clavier"),
        ["ShortcutList"] = ("Ctrl+N 写计划  |  Ctrl+M 日历导出  |  Ctrl+W 贴边收起  |  Ctrl+L 切语言\nCtrl+T 换配色  |  Ctrl+P 切置顶    |  Alt+←/→ 切日期  |  Ctrl+Home 回今天",
                            "Ctrl+N New task | Ctrl+M Calendar | Ctrl+W Dock edge | Ctrl+L Lang\nCtrl+T Theme    | Ctrl+P Pin mode | Alt+←/→ Day      | Ctrl+Home Today",
                            "Ctrl+N Ajouter  | Ctrl+M Calendrier | Ctrl+W Réduire | Ctrl+L Langue\nCtrl+T Thème    | Ctrl+P Épingler   | Alt+←/→ Jour   | Ctrl+Home Auj."),
        ["BtnTestRemind"] = ("⏰ 测试提醒", "⏰ Test Reminder", "⏰ Tester rappel"),
        ["BtnOpenData"] = ("📂 数据目录", "📂 Data Folder", "📂 Dossier"),
        ["BtnDone"] = ("完成", "Done", "Fermer"),

        // Default Reminder, Auto Rollover & WeCom Bot
        ["NoRemindOnce"] = ("本次不提醒", "No reminder this time", "Pas de rappel cette fois"),
        ["RemindDefaultTag"] = ("⏰ 默认{0}", "⏰ Def {0}", "⏰ Déf {0}"),
        ["SecSmartRemind"] = ("默认提醒、自动顺延与微信推送", "Smart Reminders, Rollover & WeCom Bot", "Rappels auto, Report & Bot WeCom"),
        ["ChkDefaultRemind"] = ("新增计划默认开启定时提醒", "Enable default reminder for new plans", "Activer le rappel par défaut"),
        ["LabelDefaultTime"] = ("默认提醒时间 (24h)", "Default time (24h)", "Heure par défaut"),
        ["ChkAutoRollover"] = ("未完成计划自动顺延到第二天", "Auto-rollover unfinished plans to next day", "Reporter auto. les tâches non terminées"),
        ["ChkRolloverRemind"] = ("顺延到第二天后自动设置提醒", "Auto-set reminder after rollover", "Définir un rappel après report"),
        ["LabelRolloverTime"] = ("顺延提醒时间 (24h)", "Rollover time (24h)", "Heure après report"),
        ["ChkWeComBot"] = ("提醒时同步推送到企业微信机器人 (WeCom Bot)", "Also push reminders to WeCom Bot", "Envoyer aussi au bot WeCom"),
        ["LabelWeComWebhook"] = ("机器人 Webhook 或 Key", "Webhook URL or Key", "URL Webhook ou Clé"),
        ["BtnTestWeCom"] = ("测试微信推送", "Test WeCom", "Tester WeCom"),
        ["ErrInvalidTime"] = ("时间格式请使用 HH:mm（例如 18:00 或 09:00）", "Use HH:mm format (e.g. 18:00 or 09:00)", "Format HH:mm requis (ex: 18:00)"),
        ["WeComNeedKey"] = ("请先填写企业微信机器人 Webhook 地址或 Key。", "Please enter WeCom Webhook URL or Key first.", "Veuillez saisir l'URL Webhook ou la clé."),
        ["WeComSentOk"] = ("推送成功！请在微信群查看。", "Sent! Check your WeCom group.", "Envoyé avec succès !"),
        ["WeComSentFail"] = ("推送失败：{0}", "Failed: {0}", "Échec : {0}"),

        // Calendar & Export
        ["TipCalendar"] = ("打开月历视图与区间导出 (Ctrl+M)", "Open Calendar & Range Export (Ctrl+M)", "Ouvrir le calendrier et l'export (Ctrl+M)"),
        ["CalWinTitle"] = ("📅  日历总览与计划导出", "📅  Calendar & Plan Export", "📅  Calendrier & Export"),
        ["CalMonthFmt"] = ("{0}年 {1}月", "{0}-{1:D2}", "{0}-{1:D2}"),
        ["CalBtnToday"] = ("回本月", "This Month", "Ce mois"),
        ["CalDayPlansFmt"] = ("{0} 计划明细（{1}/{2}）", "Plans on {0} ({1}/{2})", "Tâches du {0} ({1}/{2})"),
        ["CalJumpBoard"] = ("在主看板打开此日", "Open Day in Board", "Ouvrir ce jour"),
        ["CalEmptyDay"] = ("当天暂无计划", "No plans on this day", "Aucun plan ce jour"),
        ["CalExportHeader"] = ("区间统计与一键导出", "Date Range Summary & Export", "Résumé et Export"),
        ["CalFrom"] = ("开始", "From", "Du"),
        ["CalTo"] = ("至", "To", "Au"),
        ["CalPresetWeek"] = ("本周", "This Week", "Semaine"),
        ["CalPresetMonth"] = ("本月", "This Month", "Mois"),
        ["CalPreset7Days"] = ("近7天", "Last 7d", "7 jours"),
        ["CalStatsFmt"] = ("共 {0} 项 · 已完成 {1} · 未完成 {2}（完成率 {3}%）", "Total {0} · Done {1} · Pending {2} ({3}%)", "Total {0} · Fait {1} · Restant {2} ({3}%)"),
        ["CalExportBtn"] = ("📤 一键导出报告 (.md / .csv)", "📤 Export Report (.md / .csv)", "📤 Exporter (.md / .csv)"),
        ["CalWeComSummaryBtn"] = ("💬 发送今日汇总到微信", "💬 Send Today to WeCom", "💬 Envoyer au bot"),
        ["ExportSuccess"] = ("已成功导出至：\n{0}", "Exported successfully to:\n{0}", "Exporté avec succès vers :\n{0}"),

        // Milestone / Target Deadline
        ["BtnMilestone"] = ("🎯 节点", "🎯 Target", "🎯 Échéance"),
        ["TipQuickMilestone"] = ("设置计划节点日期（自动排入节点前每一天，显示倒计时，含提前3天/1天及当天紧急提醒）",
                                 "Set milestone date (auto-adds to every day until target, with T-3/T-1/Today urgent alerts)",
                                 "Définir une échéance (ajouté chaque jour avec rappels J-3/J-1/Jour J urgent)"),
        ["MilestoneTagFmt"] = ("🎯 {0}(剩{1}天)", "🎯 {0}({1}d)", "🎯 {0}(J-{1})"),
        ["MilestoneTodayTagFmt"] = ("🚨 {0}(当天)", "🚨 {0}(Today)", "🚨 {0}(Auj.)"),
        ["MsTomorrow"] = ("🎯 明天 (+1天 · {0})", "🎯 Tomorrow (+1d · {0})", "🎯 Demain (J-1 · {0})"),
        ["MsIn2Days"] = ("🎯 后天 (+2天 · {0})", "🎯 In 2 days (+2d · {0})", "🎯 Dans 2 jours (J-2 · {0})"),
        ["MsIn3Days"] = ("⚠️ 3天后 (+3天 · {0})", "⚠️ In 3 days (+3d · {0})", "⚠️ Dans 3 jours (J-3 · {0})"),
        ["MsIn5Days"] = ("🎯 5天后 (+5天 · {0})", "🎯 In 5 days (+5d · {0})", "🎯 Dans 5 jours (J-5 · {0})"),
        ["MsIn7Days"] = ("🎯 1周后 (+7天 · {0})", "🎯 In 1 week (+7d · {0})", "🎯 Dans 1 sem. (J-7 · {0})"),
        ["MsIn14Days"] = ("🎯 2周后 (+14天 · {0})", "🎯 In 2 weeks (+14d · {0})", "🎯 Dans 2 sem. (J-14 · {0})"),
        ["MsMonthEnd"] = ("🎯 本月底 ({0})", "🎯 Month End ({0})", "🎯 Fin du mois ({0})"),
        ["MsPickDate"] = ("📅 选择指定节点日期...", "📅 Pick custom target date...", "📅 Choisir une date..."),
        ["MsClear"] = ("✕ 清除节点日期", "✕ Clear milestone date", "✕ Effacer l'échéance"),

        ["MilestoneDueTodayBadge"] = ("🚨 今日节点 · 紧急 ({0})", "🚨 Due Today · Urgent ({0})", "🚨 Échéance · URGENT ({0})"),
        ["MilestoneLeft1Badge"] = ("🔥 距节点剩 1 天 · 提前1天 ({0})", "🔥 1d left · T-1 Alert ({0})", "🔥 J-1 · Alerte J-1 ({0})"),
        ["MilestoneLeft3Badge"] = ("⚠️ 距节点剩 3 天 · 提前3天 ({0})", "⚠️ 3d left · T-3 Alert ({0})", "⚠️ J-3 · Alerte J-3 ({0})"),
        ["MilestoneLeftDaysBadge"] = ("🎯 距节点剩 {0} 天 ({1})", "🎯 {0}d left ({1})", "🎯 J-{0} ({1})"),
        ["MilestoneOverdueBadge"] = ("❗ 节点超期 {0} 天 ({1})", "❗ Overdue {0}d ({1})", "❗ En retard J+{0} ({1})"),

        ["CtxCompleteMilestoneAll"] = ("🎯 整个节点系列全部标为完成", "🎯 Complete Entire Milestone Series", "🎯 Terminer toute la série d'échéance"),
        ["CtxDeleteMilestoneAll"] = ("🗑️ 删除整个节点系列计划", "🗑️ Delete Entire Milestone Series", "🗑️ Supprimer toute la série d'échéance"),

        ["LabelMilestoneDate"] = ("🎯 节点截止日期（设置后从当前日期至节点前每天排入计划，含提前3天/1天及当天紧急提醒）",
                                  "🎯 Milestone Target Date (auto-schedules daily countdown + T-3/T-1/Today urgent alerts)",
                                  "🎯 Date d'échéance (planifie chaque jour + rappels J-3/J-1/Jour J urgent)"),
        ["MsPreset+3"] = ("+3天", "+3d", "+3j"),
        ["MsPreset+5"] = ("+5天", "+5d", "+5j"),
        ["MsPreset+7"] = ("+1周", "+7d", "+7j"),
        ["MsPreset+14"] = ("+2周", "+14d", "+14j"),
        ["MsPresetMonthEnd"] = ("本月底", "Month End", "Fin mois"),

        ["PopupTitleUrgentToday"] = ("🚨  今日节点截止 · 紧急提醒！", "🚨  URGENT: Milestone Due Today!", "🚨  URGENT : Échéance Aujourd'hui !"),
        ["PopupTitleOverdue"] = ("🚨  节点已超期 · 紧急提醒！", "🚨  URGENT: Milestone Overdue!", "🚨  URGENT : Échéance Dépassée !"),
        ["PopupTitleT1"] = ("🔥  计划节点 · 提前 1 天提醒", "🔥  Milestone Alert · 1 Day Left", "🔥  Alerte Échéance · J-1"),
        ["PopupTitleT3"] = ("⚠️  计划节点 · 提前 3 天提醒", "⚠️  Milestone Alert · 3 Days Left", "⚠️  Alerte Échéance · J-3"),
        ["PopupTitleMilestone"] = ("🎯  计划节点倒计时提醒（剩 {0} 天）", "🎯  Milestone Countdown ({0}d left)", "🎯  Compte à rebours (J-{0})"),
        ["MilestoneNoteDueToday"] = ("🚨【紧急提醒】今天是节点截止日（{0}），请务必今日完成！", "🚨 [URGENT] Today is the milestone deadline ({0})!", "🚨 [URGENT] Aujourd'hui est la date d'échéance ({0}) !"),
        ["MilestoneNoteOverdue"] = ("❗【超期紧急提醒】节点（{0}）已超期 {1} 天，请立即处理！", "❗ [OVERDUE] Milestone ({0}) is overdue by {1} day(s)!", "❗ [EN RETARD] L'échéance ({0}) est dépassée de {1} jour(s) !"),
        ["MilestoneNoteT1"] = ("🔥【提前1天提醒】距离节点（{0}）仅剩 1 天，请抓紧推进！", "🔥 [T-1 Alert] Only 1 day left until milestone ({0})!", "🔥 [Alerte J-1] Plus qu'un jour avant l'échéance ({0}) !"),
        ["MilestoneNoteT3"] = ("⚠️【提前3天提醒】距离节点（{0}）还剩 3 天，请按计划推进。", "⚠️ [T-3 Alert] 3 days left until milestone ({0}).", "⚠️ [Alerte J-3] 3 jours restants avant l'échéance ({0})."),
        ["MilestoneNoteDays"] = ("🎯 距离节点（{0}）还剩 {1} 天。", "🎯 {1} day(s) left until milestone ({0}).", "🎯 {1} jour(s) restant(s) avant l'échéance ({0}).")
    };

    public static string T(string key)
    {
        if (Dict.TryGetValue(key, out var entry))
        {
            return Current switch
            {
                AppLanguage.En => entry.En,
                AppLanguage.Fr => entry.Fr,
                _ => entry.Zh
            };
        }
        return key;
    }

    public static string TF(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }

    public static string GetWeekday(DayOfWeek dow)
    {
        return Current switch
        {
            AppLanguage.En => dow switch
            {
                DayOfWeek.Monday => "Mon",
                DayOfWeek.Tuesday => "Tue",
                DayOfWeek.Wednesday => "Wed",
                DayOfWeek.Thursday => "Thu",
                DayOfWeek.Friday => "Fri",
                DayOfWeek.Saturday => "Sat",
                _ => "Sun"
            },
            AppLanguage.Fr => dow switch
            {
                DayOfWeek.Monday => "Lun",
                DayOfWeek.Tuesday => "Mar",
                DayOfWeek.Wednesday => "Mer",
                DayOfWeek.Thursday => "Jeu",
                DayOfWeek.Friday => "Ven",
                DayOfWeek.Saturday => "Sam",
                _ => "Dim"
            },
            _ => dow switch
            {
                DayOfWeek.Monday => "周一",
                DayOfWeek.Tuesday => "周二",
                DayOfWeek.Wednesday => "周三",
                DayOfWeek.Thursday => "周四",
                DayOfWeek.Friday => "周五",
                DayOfWeek.Saturday => "周六",
                _ => "周日"
            }
        };
    }
}
