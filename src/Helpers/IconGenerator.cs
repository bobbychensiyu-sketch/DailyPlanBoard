using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DailyPlanBoard.Helpers;

public static class IconGenerator
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon CreateTrayIcon(int size = 32)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Warm amber-orange rounded badge
            using var path = CreateRoundedRect(new Rectangle(2, 2, size - 4, size - 4), size / 4);
            using var bgBrush = new LinearGradientBrush(
                new Point(0, 0), new Point(size, size),
                Color.FromArgb(255, 245, 158, 11),
                Color.FromArgb(255, 217, 119, 6));
            g.FillPath(bgBrush, path);

            using var borderPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1.2f);
            g.DrawPath(borderPen, path);

            // Crisp white checkmark
            using var checkPen = new Pen(Color.White, Math.Max(2.4f, size / 10f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            g.DrawLines(checkPen, new[]
            {
                new PointF(size * 0.27f, size * 0.52f),
                new PointF(size * 0.44f, size * 0.68f),
                new PointF(size * 0.74f, size * 0.34f)
            });
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
