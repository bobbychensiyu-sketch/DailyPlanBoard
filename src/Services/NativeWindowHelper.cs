using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DailyPlanBoard.Models;

namespace DailyPlanBoard.Services;

public static class NativeWindowHelper
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int GWL_HWNDPARENT = -8;
    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICKFRAME = 0x00040000L;
    private const long WS_EX_TOPMOST = 0x00000008L;
    private const long WS_EX_TRANSPARENT = 0x00000020L;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WM_WINDOWPOSCHANGING = 0x0046;
    public const int WM_HOTKEY = 0x0312;
    public const int WM_DISPLAYCHANGE = 0x007E;
    private const uint GW_HWNDPREV = 3;
    private const int DWMWA_CLOAKED = 14;

    private const uint WDA_NONE = 0x00000000;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    private static readonly IntPtr HWND_BOTTOM = new(1);
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static bool TryParseHotkey(string? spec, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(spec) || spec.Equals("None", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return false;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToUpperInvariant())
            {
                case "ALT": modifiers |= 0x0001; break;
                case "CTRL":
                case "CONTROL": modifiers |= 0x0002; break;
                case "SHIFT": modifiers |= 0x0004; break;
                case "WIN": modifiers |= 0x0008; break;
                default: return false;
            }
        }

        if (modifiers == 0) return false;

        string keyPart = parts[^1];
        if (Enum.TryParse<System.Windows.Input.Key>(keyPart, true, out var wpfKey))
        {
            vk = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(wpfKey);
            return vk != 0;
        }
        return false;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        => IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, dwNewLong);

    public static void ApplyToolWindowStyle(Window window, bool hideFromAltTabAndTaskbar)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        if (hideFromAltTabAndTaskbar)
        {
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~WS_EX_APPWINDOW;
        }
        else
        {
            exStyle &= ~WS_EX_TOOLWINDOW;
            exStyle |= WS_EX_APPWINDOW;
        }
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    public static void ApplyPinMode(Window window, WindowPinMode mode)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        if (mode == WindowPinMode.Desktop)
        {
            window.Topmost = false;
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        else if (mode == WindowPinMode.TopMost)
        {
            window.Topmost = true;
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        else
        {
            window.Topmost = false;
            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    public static void SetExcludeFromCapture(Window window, bool exclude)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            SetWindowDisplayAffinity(hwnd, exclude ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);
        }
        catch { }
    }

    public static void SendToBottom(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public static IntPtr DesktopModeWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, Func<bool> isDesktopMode)
    {
        if (msg == WM_WINDOWPOSCHANGING && isDesktopMode())
        {
            var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            if ((pos.flags & SWP_NOZORDER) == 0)
            {
                pos.hwndInsertAfter = HWND_BOTTOM;
                Marshal.StructureToPtr(pos, lParam, true);
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 获取指定窗口所在显示器的工作区（已换算为 WPF DIP 坐标）
    /// </summary>
    public static Rect GetMonitorWorkAreaDip(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        IntPtr mon = hwnd != IntPtr.Zero
            ? MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST)
            : MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTONEAREST);

        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(mon, ref mi))
            return SystemParameters.WorkArea;

        var dpi = VisualTreeHelper.GetDpi(window);
        double sx = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double sy = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        return new Rect(
            mi.rcWork.Left / sx,
            mi.rcWork.Top / sy,
            (mi.rcWork.Right - mi.rcWork.Left) / sx,
            (mi.rcWork.Bottom - mi.rcWork.Top) / sy);
    }

    /// <summary>
    /// 获取当前鼠标位置（已换算为 WPF DIP 坐标）
    /// </summary>
    public static System.Windows.Point GetCursorDip(Visual reference)
    {
        GetCursorPos(out var pt);
        var dpi = VisualTreeHelper.GetDpi(reference);
        double sx = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double sy = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
        return new System.Windows.Point(pt.X / sx, pt.Y / sy);
    }

    private static bool IsWindowCloaked(IntPtr hwnd)
    {
        try
        {
            int hr = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int));
            return hr == 0 && cloaked != 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool RectsOverlap(RECT a, RECT b)
        => a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;

    private static bool IsShellOverlayClass(string cn)
        => cn is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
            or "Windows.UI.Core.CoreWindow" or "MultitaskingViewFrame"
            or "ForegroundStaging" or "ApplicationManager_DesktopShellWindow"
            or "Xaml_WindowedPopupClass" or "NotifyIconOverflowWindow" or "#32768"
            or "tooltips_class32" or "SysShadow";

    /// <summary>
    /// 若窗口应处于置顶模式但被新打开的软件抢占 Z 序或丢失 WS_EX_TOPMOST，则静默恢复置顶（不抢占输入焦点）
    /// </summary>
    public static void EnsureTopMostIfNeeded(Window window)
    {
        try
        {
            if (!window.IsVisible) return;
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            bool hasTopMostStyle = (exStyle & WS_EX_TOPMOST) != 0;

            if (!hasTopMostStyle || !window.Topmost)
            {
                ApplyPinMode(window, WindowPinMode.TopMost);
                return;
            }

            if (!GetWindowRect(hwnd, out var selfRect)) return;

            IntPtr prev = GetWindow(hwnd, GW_HWNDPREV);
            int guard = 0;
            bool needLift = false;

            while (prev != IntPtr.Zero && guard++ < 48)
            {
                if (IsWindowVisible(prev) && !IsIconic(prev) && !IsWindowCloaked(prev))
                {
                    if (GetWindowRect(prev, out var otherRect) && RectsOverlap(selfRect, otherRect))
                    {
                        GetWindowThreadProcessId(prev, out uint otherPid);
                        if (otherPid == (uint)Environment.ProcessId)
                        {
                            // 本程序自己的弹窗/右键菜单在上方时不抢压
                            return;
                        }

                        var cls = new StringBuilder(64);
                        GetClassName(prev, cls, 64);
                        string cn = cls.ToString();

                        if (IsShellOverlayClass(cn)
                            || cn.Contains("IME", StringComparison.OrdinalIgnoreCase)
                            || cn.Contains("Popup", StringComparison.OrdinalIgnoreCase))
                        {
                            prev = GetWindow(prev, GW_HWNDPREV);
                            continue;
                        }

                        needLift = true;
                        break;
                    }
                }
                prev = GetWindow(prev, GW_HWNDPREV);
            }

            if (needLift)
            {
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }
        catch { }
    }

    /// <summary>
    /// 检测当前前台是否有真正的全屏应用（无边框游戏、全屏视频、PPT 放映等），避免误判开始菜单或普通最大化窗口
    /// </summary>
    public static bool IsForegroundFullscreen()
    {
        try
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || !IsWindowVisible(fg) || IsIconic(fg)) return false;
            if (IsWindowCloaked(fg)) return false;

            GetWindowThreadProcessId(fg, out uint fgPid);
            if (fgPid == (uint)Environment.ProcessId) return false;

            var cls = new StringBuilder(64);
            GetClassName(fg, cls, 64);
            string className = cls.ToString();
            if (IsShellOverlayClass(className))
                return false;

            long exStyle = GetWindowLongPtr(fg, GWL_EXSTYLE).ToInt64();
            if ((exStyle & WS_EX_TRANSPARENT) != 0)
                return false;

            if (!GetWindowRect(fg, out var r)) return false;
            if (r.Right <= r.Left || r.Bottom <= r.Top) return false;

            IntPtr mon = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(mon, ref mi)) return false;

            var m = mi.rcMonitor;
            bool coversMonitor = r.Left <= m.Left + 2 && r.Top <= m.Top + 2 &&
                                 r.Right >= m.Right - 2 && r.Bottom >= m.Bottom - 2;
            if (!coversMonitor) return false;

            // 普通带标题栏和可缩放边框的最大化软件（如任务栏自动隐藏/副屏最大化）不算全屏避让对象
            long style = GetWindowLongPtr(fg, GWL_STYLE).ToInt64();
            bool hasTitleAndFrame = (style & WS_CAPTION) == WS_CAPTION && (style & WS_THICKFRAME) == WS_THICKFRAME;
            if (hasTitleAndFrame && IsZoomed(fg) && r.Top >= m.Top - 16)
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void TrimMemory()
    {
        try
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
            GC.WaitForPendingFinalizers();
            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, new IntPtr(-1), new IntPtr(-1));
        }
        catch { }
    }
}
