// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.Diagnostics;
using RDPManager.Models;

namespace RDPManager.Services;

/// <summary>以 UltraVNC Viewer (vncviewer.exe) 開啟 VNC 連線</summary>
public static class VncLauncher
{
    public const string DownloadUrl = "https://uvnc.com/downloads/ultravnc.html";

    private static readonly string[] CandidatePaths =
    {
        @"%ProgramFiles%\uvnc bvba\UltraVNC\vncviewer.exe",
        @"%ProgramFiles(x86)%\uvnc bvba\UltraVNC\vncviewer.exe",
        @"%ProgramFiles%\UltraVNC\vncviewer.exe",
        @"%ProgramFiles(x86)%\UltraVNC\vncviewer.exe",
    };

    /// <summary>在常見安裝位置尋找 UltraVNC Viewer,找不到回傳 null</summary>
    public static string? Detect()
    {
        foreach (var p in CandidatePaths)
        {
            var full = Environment.ExpandEnvironmentVariables(p);
            if (File.Exists(full)) return full;
        }
        // 可攜版:放在本程式旁的 UltraVNC 資料夾或同一資料夾
        foreach (var p in new[] { Path.Combine(AppContext.BaseDirectory, "UltraVNC", "vncviewer.exe"), Path.Combine(AppContext.BaseDirectory, "vncviewer.exe") })
            if (File.Exists(p)) return p;
        return null;
    }

    /// <summary>設定的路徑有效就用它,否則自動偵測</summary>
    public static string? Resolve(string? configuredPath) =>
        !string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath) ? configuredPath : Detect();

    /// <summary>
    /// 組出 UltraVNC Viewer 命令列參數。
    /// 格式:vncviewer.exe [選項] host::port
    /// </summary>
    public static List<string> BuildArguments(RdpConnection c, string password, bool fullScreen)
    {
        var args = new List<string>();
        if (fullScreen || c.ScreenMode == ScreenMode.FullScreen) args.Add("/fullscreen");
        if (c.SmartSizing || c.ScreenMode != ScreenMode.Custom) args.Add("/autoscaling");
        if (c.VncViewOnly) args.Add("/viewonly");
        if (!c.RedirectClipboard) args.Add("/disableclipboard");
        if (!string.IsNullOrWhiteSpace(c.Username))
        {
            args.Add("/user");
            args.Add(c.FullUserName); // UltraVNC MS-Logon 帳號
        }
        if (!string.IsNullOrEmpty(password))
        {
            args.Add("/password");
            args.Add(password);
        }
        args.Add("/autoreconnect");
        args.Add("10");
        var host = c.Host.Trim();
        if (host.Contains(':') && !host.StartsWith('[')) host = $"[{host}]";
        args.Add($"{host}::{c.Port}");
        return args;
    }

    public static void Launch(string viewerPath, RdpConnection c, string password, bool fullScreen)
    {
        var psi = new ProcessStartInfo(viewerPath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(viewerPath) ?? "",
        };
        foreach (var a in BuildArguments(c, password, fullScreen)) psi.ArgumentList.Add(a);
        Process.Start(psi);
    }
}
