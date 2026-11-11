// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RDPManager.Services;

/// <summary>應用程式圖示 (內嵌資源 Resources\app.ico,由 tools\make-icon.ps1 產生) 與狀態燈號圖片</summary>
public static class AppIcon
{
    private static Icon? _icon;

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon Get()
    {
        if (_icon != null) return _icon;
        using (var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("RDPManager.app.ico"))
        {
            if (stream != null) return _icon = new Icon(stream);
        }
        return _icon = DrawFallback();
    }

    private static Icon DrawFallback()
    {
        Icon result;
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            var blue = Color.FromArgb(0, 103, 192);
            using var body = new SolidBrush(blue);
            using var screen = new SolidBrush(Color.FromArgb(225, 240, 255));
            g.FillRectangle(body, 1, 3, 30, 21);
            g.FillRectangle(screen, 4, 6, 24, 15);
            g.FillRectangle(body, 13, 24, 6, 3);
            g.FillRectangle(body, 8, 27, 16, 3);
            using var pen = new Pen(blue, 2.6f) { EndCap = LineCap.ArrowAnchor };
            g.DrawLine(pen, 8, 18, 22, 10);
        }
        var h = bmp.GetHicon();
        result = (Icon)Icon.FromHandle(h).Clone();
        DestroyIcon(h);
        return result;
    }

    public static ImageList CreateStatusImages()
    {
        var il = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        il.Images.Add("Unknown", Dot(Color.Silver));
        il.Images.Add("Checking", Dot(Color.Orange));
        il.Images.Add("Online", Dot(Color.FromArgb(40, 167, 69)));
        il.Images.Add("Offline", Dot(Color.FromArgb(220, 53, 69)));
        return il;
    }

    private static Bitmap Dot(Color c)
    {
        var b = new Bitmap(16, 16);
        using var g = Graphics.FromImage(b);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var br = new SolidBrush(c);
        g.FillEllipse(br, 3, 3, 10, 10);
        return b;
    }
}
