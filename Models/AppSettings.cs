// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

namespace RDPManager.Models;

public class AppSettings
{
    /// <summary>雙擊或按 Enter 時的預設連線方式</summary>
    public LaunchMode DefaultLaunchMode { get; set; } = LaunchMode.Embedded;
    public bool MinimizeToTray { get; set; }
    public bool CheckStatusOnStartup { get; set; } = true;
    public int StatusTimeoutMs { get; set; } = 1500;
    /// <summary>自動檢查狀態間隔 (分鐘),0 = 關閉</summary>
    public int AutoCheckMinutes { get; set; }
    public bool ConfirmDelete { get; set; } = true;
    /// <summary>連線已開啟時切換到該分頁,而非開新分頁</summary>
    public bool ReuseOpenSession { get; set; } = true;
    public bool DailyAutoBackup { get; set; } = true;

    // 密碼
    /// <summary>新連線預設「記住密碼」(預設不儲存)</summary>
    public bool RememberPasswordByDefault { get; set; }
    /// <summary>未儲存密碼時,連線前先詢問帳號密碼</summary>
    public bool AskPasswordBeforeConnect { get; set; } = true;

    // VNC
    /// <summary>UltraVNC Viewer (vncviewer.exe) 路徑;空白 = 自動偵測</summary>
    public string UltraVncPath { get; set; } = "";

    // 全螢幕
    /// <summary>所有連線一律以全螢幕開啟 (預設開啟)</summary>
    public bool AlwaysFullScreen { get; set; } = true;
    /// <summary>Windows 組合鍵 (Alt+Tab 等):0 = 本機、1 = 一律遠端、2 = 僅全螢幕時遠端</summary>
    public int KeyboardHookMode { get; set; } = 2;
    /// <summary>全螢幕側邊面板固定顯示</summary>
    public bool SidebarPinned { get; set; }
    /// <summary>滑鼠移到螢幕左緣時自動展開側邊面板</summary>
    public bool SidebarHoverExpand { get; set; } = true;

    // 視窗狀態
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public int WindowWidth { get; set; } = 1200;
    public int WindowHeight { get; set; } = 750;
    public bool WindowMaximized { get; set; }
    public int SplitterDistance { get; set; } = 230;
}

public class HistoryEntry
{
    public DateTime Time { get; set; } = DateTime.Now;
    public Guid ConnectionId { get; set; }
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Event { get; set; } = "";
    public string Detail { get; set; } = "";
}
