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
    /// <summary>RDP (預設) 或 VNC (以 UltraVNC Viewer 開啟)</summary>
    public ConnectionProtocol Protocol { get; set; } = ConnectionProtocol.Rdp;
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
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
    /// <summary>VNC:僅檢視 (不傳送鍵盤滑鼠)</summary>
    public bool VncViewOnly { get; set; }

    // 統計
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastConnectedAt { get; set; }
    public int ConnectCount { get; set; }

    // 執行期狀態 (不儲存)
    [JsonIgnore] public HostStatus Status { get; set; } = HostStatus.Unknown;
    [JsonIgnore] public long? LatencyMs { get; set; }

    [JsonIgnore] public bool IsVnc => Protocol == ConnectionProtocol.Vnc;
    [JsonIgnore] public int DefaultPort => IsVnc ? AddressParser.DefaultVncPort : AddressParser.DefaultPort;
    [JsonIgnore] public string ProtocolName => IsVnc ? "VNC" : "RDP";

    [JsonIgnore] public string DisplayAddress => AddressParser.Format(Host, Port, DefaultPort);

    [JsonIgnore]
    public string FullUserName =>
        string.IsNullOrWhiteSpace(Username) ? "" :
        string.IsNullOrWhiteSpace(Domain) ? Username : $"{Domain}\\{Username}";

    public RdpConnection Clone() => (RdpConnection)MemberwiseClone();
}
