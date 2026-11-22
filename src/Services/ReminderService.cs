using System;
using System.Windows.Threading;
using DailyPlanBoard.Models;

namespace DailyPlanBoard.Services;

public class ReminderService : IDisposable
{
    private readonly StorageService _storage;
    private readonly DispatcherTimer _timer;
    private string _lastDate = DateTime.Today.ToString("yyyy-MM-dd");
    private int _idleTicks;

    public event Action<PlanItem>? ReminderTriggered;
    public event Action? DayChanged;

    public ReminderService(StorageService storage)
    {
        _storage = storage;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        string today = now.ToString("yyyy-MM-dd");

        if (today != _lastDate)
        {
            _lastDate = today;
            _storage.EnsureDailyRepeatsForDate(today);
            DayChanged?.Invoke();
        }

        var dueItems = _storage.GetDueReminders(now);
        foreach (var item in dueItems)
        {
            ReminderTriggered?.Invoke(item);
        }

        // 每 10 分钟温和回收一次工作集，保持低内存常驻
        _idleTicks++;
        if (_idleTicks >= 600)
        {
            _idleTicks = 0;
            NativeWindowHelper.TrimMemory();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
