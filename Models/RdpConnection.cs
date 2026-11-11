// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.Text.Json.Serialization;
using RDPManager.Services;

namespace RDPManager.Models;

/// <summary>畫面模式</summary>
public enum ScreenMode { FitWindow, FullScreen, Custom }

/// <summary>連線方式:Default = 依全域設定</summary>
public enum LaunchMode { Default, Embedded, External }

/// <summary>遠端音訊:0 = 本機播放、1 = 遠端播放、2 = 不播放 (與 RDP 定義一致)</summary>
public enum AudioMode { Local = 0, Remote = 1, None = 2 }

public enum HostStatus { Unknown, Checking, Online, Offline }

/// <summary>連線類型</summary>
public enum ConnectionProtocol { Rdp, Vnc }

public class RdpConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // 一般
    /// <summary>
    /// 預設連線類型 (雙擊 / Enter 時使用):RDP (預設) 或 VNC。
    /// 同一個連線設定都可以用 RDP 或 VNC 連線 (右鍵或工具列按鈕指定)。
    /// </summary>
    public ConnectionProtocol Protocol { get; set; } = ConnectionProtocol.Rdp;
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    /// <summary>RDP 連接埠</summary>
    public int Port { get; set; } = 3389;
    public string Username { get; set; } = "";
    public string Domain { get; set; } = "";
    /// <summary>以 Windows DPAPI (目前使用者) 加密後的密碼</summary>
    public string? EncryptedPassword { get; set; }
    public string Group { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool IsFavorite { get; set; }

    // 顯示
    public ScreenMode ScreenMode { get; set; } = ScreenMode.FullScreen;
    public int DesktopWidth { get; set; } = 1920;
    public int DesktopHeight { get; set; } = 1080;
    public int ColorDepth { get; set; } = 32;
    public bool SmartSizing { get; set; }
    public bool MultiMonitor { get; set; }

    // 本機資源
    public bool RedirectClipboard { get; set; } = true;
    public bool RedirectDrives { get; set; }
    public bool RedirectPrinters { get; set; }
    public AudioMode AudioMode { get; set; } = AudioMode.Local;

    // 進階
    public bool AdminSession { get; set; }
    public LaunchMode LaunchMode { get; set; } = LaunchMode.Default;
    public string MacAddress { get; set; } = "";

    // VNC (以 UltraVNC Viewer 開啟)
    /// <summary>VNC 連接埠;null = 預設 5900 (null 也代表 v1.3 以前的舊資料,見 MigrateLegacyVnc)</summary>
    public int? VncPort { get; set; }
    /// <summary>VNC 密碼 (DPAPI 加密);與 RDP 密碼分開</summary>
    public string? VncEncryptedPassword { get; set; }
    /// <summary>UltraVNC MS-Logon 帳號 (選填)</summary>
    public string VncUsername { get; set; } = "";
    /// <summary>VNC:僅檢視 (不傳送鍵盤滑鼠)</summary>
    public bool VncViewOnly { get; set; }

    // 統計
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastConnectedAt { get; set; }
    public int ConnectCount { get; set; }

    // 執行期狀態 (不儲存)
    [JsonIgnore] public HostStatus Status { get; set; } = HostStatus.Unknown;
    [JsonIgnore] public long? LatencyMs { get; set; }

    /// <summary>預設連線類型是否為 VNC</summary>
    [JsonIgnore] public bool IsVnc => Protocol == ConnectionProtocol.Vnc;
    [JsonIgnore] public string ProtocolName => IsVnc ? "VNC" : "RDP";
    [JsonIgnore] public int VncPortValue => VncPort ?? AddressParser.DefaultVncPort;
    /// <summary>預設連線類型使用的連接埠 (狀態檢查用)</summary>
    [JsonIgnore] public int ActivePort => IsVnc ? VncPortValue : Port;

    [JsonIgnore] public string RdpAddress => AddressParser.Format(Host, Port, AddressParser.DefaultPort);
    [JsonIgnore] public string VncAddress => AddressParser.Format(Host, VncPortValue, AddressParser.DefaultVncPort);
    /// <summary>預設連線類型的位址</summary>
    [JsonIgnore] public string DisplayAddress => IsVnc ? VncAddress : RdpAddress;

    /// <summary>
    /// v1.3.0 的 VNC 連線把 VNC 連接埠 / 帳號 / 密碼存在 RDP 欄位;
    /// 移到新的 VNC 欄位 (預設連線類型仍為 VNC)。回傳是否有轉換。
    /// </summary>
    public bool MigrateLegacyVnc()
    {
        if (Protocol != ConnectionProtocol.Vnc || VncPort != null) return false;
        VncPort = Port;
        VncUsername = FullUserName;
        VncEncryptedPassword = EncryptedPassword;
        Port = AddressParser.DefaultPort;
        Username = "";
        Domain = "";
        EncryptedPassword = null;
        return true;
    }

    [JsonIgnore]
    public string FullUserName =>
        string.IsNullOrWhiteSpace(Username) ? "" :
        string.IsNullOrWhiteSpace(Domain) ? Username : $"{Domain}\\{Username}";

    public RdpConnection Clone() => (RdpConnection)MemberwiseClone();
}
