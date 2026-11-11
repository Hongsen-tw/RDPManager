// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.Diagnostics;
using System.Text;
using RDPManager.Models;

namespace RDPManager.Services;

/// <summary>產生 .rdp 檔並以 Windows 內建 mstsc.exe 開啟</summary>
public static class ExternalLauncher
{
    public static void Launch(RdpConnection c, string password, bool fullScreen = false)
    {
        if (fullScreen && c.ScreenMode != ScreenMode.FullScreen)
        {
            c = c.Clone();
            c.ScreenMode = ScreenMode.FullScreen;
        }
        var host = c.Host.Trim();
        if (!string.IsNullOrEmpty(c.FullUserName) && !string.IsNullOrEmpty(password))
            CredentialManager.WriteRdpCredential(host, c.FullUserName, password);

        var dir = Path.Combine(Path.GetTempPath(), "RDPManager");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{SafeFileName(c.Name)}-{Guid.NewGuid():N}.rdp");
        File.WriteAllText(file, BuildRdpFile(c), Encoding.Unicode);

        var args = $"\"{file}\"";
        if (c.AdminSession) args += " /admin";
        Process.Start(new ProcessStartInfo("mstsc.exe", args) { UseShellExecute = false });

        // mstsc 讀取完畢後刪除暫存 .rdp
        _ = Task.Delay(TimeSpan.FromSeconds(20)).ContinueWith(_ =>
        {
            try { File.Delete(file); } catch { /* ignore */ }
        });
    }

    public static void SaveRdpFile(RdpConnection c, string path) =>
        File.WriteAllText(path, BuildRdpFile(c), Encoding.Unicode);

    public static string BuildRdpFile(RdpConnection c)
    {
        var sb = new StringBuilder();
        void S(string key, string v) => sb.AppendLine($"{key}:s:{v}");
        void I(string key, int v) => sb.AppendLine($"{key}:i:{v}");

        S("full address", AddressParser.Format(c.Host.Trim(), c.Port));
        if (!string.IsNullOrEmpty(c.FullUserName)) S("username", c.FullUserName);

        switch (c.ScreenMode)
        {
            case ScreenMode.FullScreen:
                I("screen mode id", 2);
                break;
            case ScreenMode.Custom:
                I("screen mode id", 1);
                I("desktopwidth", c.DesktopWidth);
                I("desktopheight", c.DesktopHeight);
                break;
            default:
                var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1600, 900);
                I("screen mode id", 1);
                I("desktopwidth", (int)(area.Width * 0.85));
                I("desktopheight", (int)(area.Height * 0.85));
                I("dynamic resolution", 1);
                break;
        }

        I("session bpp", c.ColorDepth);
        I("smart sizing", c.SmartSizing ? 1 : 0);
        I("use multimon", c.MultiMonitor ? 1 : 0);
        I("redirectclipboard", c.RedirectClipboard ? 1 : 0);
        I("redirectprinters", c.RedirectPrinters ? 1 : 0);
        if (c.RedirectDrives) S("drivestoredirect", "*");
        I("audiomode", (int)c.AudioMode);
        I("administrative session", c.AdminSession ? 1 : 0);
        I("authentication level", 2);
        I("prompt for credentials", 0);
        I("autoreconnection enabled", 1);
        I("enablecredsspsupport", 1);
        return sb.ToString();
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var s = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return s.Length == 0 ? "connection" : s;
    }
}
