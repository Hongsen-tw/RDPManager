// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.ComponentModel;
using RDPManager.Models;

namespace RDPManager.Controls;

public enum SessionState { Idle, Connecting, Connected, Disconnected }

/// <summary>一個內嵌 RDP 工作階段的分頁</summary>
public class RdpSessionPage : TabPage
{
    public RdpConnection Connection { get; }
    public SessionState State { get; private set; } = SessionState.Idle;
    public bool IsActive => State is SessionState.Connecting or SessionState.Connected;
    public string LastMessage { get; private set; } = "";

    /// <summary>(page, 事件, 說明)</summary>
    public event Action<RdpSessionPage, string, string>? SessionEvent;
    public event Action<RdpSessionPage>? StateChanged;
    public event Action<RdpSessionPage>? CloseRequested;

    private readonly string _password;
    private RdpClientControl? _rdp;
    private bool _loggedIn;
    private Size _lastSize;
    private readonly System.Windows.Forms.Timer _resizeTimer = new() { Interval = 700 };

    private readonly Panel _overlay = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 34), Visible = false };
    private readonly FlowLayoutPanel _overlayBox = new() { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, BackColor = Color.Transparent };
    private readonly Label _overlayTitle = new() { AutoSize = true, ForeColor = Color.White, Font = new Font("Microsoft JhengHei UI", 14F, FontStyle.Bold) };
    private readonly Label _overlayText = new() { AutoSize = true, ForeColor = Color.Gainsboro, MaximumSize = new Size(560, 0), Margin = new Padding(3, 8, 3, 16) };

    public RdpSessionPage(RdpConnection connection, string password)
    {
        Connection = connection;
        _password = password;
        Text = connection.Name;
        ToolTipText = $"{connection.Name}  ({connection.DisplayAddress})";
        UseVisualStyleBackColor = false;
        BackColor = Color.Black;

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent };
        var btnReconnect = new Button { Text = "重新連線", AutoSize = true, Padding = new Padding(10, 3, 10, 3), BackColor = SystemColors.Control };
        var btnClose = new Button { Text = "關閉分頁", AutoSize = true, Padding = new Padding(10, 3, 10, 3), BackColor = SystemColors.Control };
        btnReconnect.Click += (_, _) => Connect();
        btnClose.Click += (_, _) => CloseRequested?.Invoke(this);
        buttons.Controls.AddRange(new Control[] { btnReconnect, btnClose });

        _overlayBox.Controls.AddRange(new Control[] { _overlayTitle, _overlayText, buttons });
        _overlay.Controls.Add(_overlayBox);
        _overlay.Resize += (_, _) => CenterOverlay();
        Controls.Add(_overlay);

        _resizeTimer.Tick += (_, _) => { _resizeTimer.Stop(); ApplyResize(); };
    }

    public void Connect()
    {
        DestroyClient();
        _overlay.Visible = false;
        _loggedIn = false;

        var c = Connection;
        var rdp = new RdpClientControl { Dock = DockStyle.Fill };
        rdp.Connected += (_, _) => SetState(SessionState.Connected, "已連線");
        rdp.LoginComplete += (_, _) =>
        {
            _loggedIn = true;
            _lastSize = ClientSize;
            SessionEvent?.Invoke(this, "登入完成", "");
        };
        rdp.Disconnected += (_, reason) => OnDisconnected(reason);
        rdp.FatalError += (_, code) => OnDisconnected(-code);

        ((ISupportInitialize)rdp).BeginInit();
        Controls.Add(rdp);
        ((ISupportInitialize)rdp).EndInit();
        rdp.CreateControl();
        _rdp = rdp;

        dynamic o = rdp.Ocx;

        int w, h;
        switch (c.ScreenMode)
        {
            case ScreenMode.Custom:
                w = c.DesktopWidth; h = c.DesktopHeight; break;
            case ScreenMode.FullScreen:
                var b = Screen.FromControl(this).Bounds;
                w = b.Width; h = b.Height; break;
            default:
                w = ClientSize.Width; h = ClientSize.Height; break;
        }
        w = Math.Clamp(w, 640, 8192) & ~1;
        h = Math.Clamp(h, 480, 8192);

        string host = c.Host.Trim();
        TrySet(() => o.Server = host);
        if (!string.IsNullOrWhiteSpace(c.Username)) TrySet(() => o.UserName = c.Username);
        if (!string.IsNullOrWhiteSpace(c.Domain)) TrySet(() => o.Domain = c.Domain);
        TrySet(() => o.DesktopWidth = w);
        TrySet(() => o.DesktopHeight = h);
        TrySet(() => o.ColorDepth = c.ColorDepth);
        TrySet(() => o.ConnectingText = $"正在連線到 {c.Name} …");
        TrySet(() => o.DisconnectedText = "已中斷連線");

        dynamic? adv = TryGet(() => o.AdvancedSettings9) ?? TryGet(() => o.AdvancedSettings2);
        if (adv != null)
        {
            TrySet(() => adv.RDPPort = c.Port);
            if (!string.IsNullOrEmpty(_password)) TrySet(() => adv.ClearTextPassword = _password);
            TrySet(() => adv.EnableCredSspSupport = true);   // NLA
            TrySet(() => adv.AuthenticationLevel = 2);       // 憑證有問題時警告
            TrySet(() => adv.RedirectClipboard = c.RedirectClipboard);
            TrySet(() => adv.RedirectDrives = c.RedirectDrives);
            TrySet(() => adv.RedirectPrinters = c.RedirectPrinters);
            TrySet(() => adv.SmartSizing = c.SmartSizing);
            TrySet(() => adv.EnableAutoReconnect = true);
            TrySet(() => adv.ConnectToAdministerServer = c.AdminSession);
            TrySet(() => adv.BitmapPersistence = 1);
            TrySet(() => adv.GrabFocusOnConnect = true);
        }
        int audio = (int)c.AudioMode;
        if (!TrySet(() => o.SecuredSettings3.AudioRedirectionMode = audio))
            TrySet(() => o.SecuredSettings2.AudioRedirectionMode = audio);
        if (c.ScreenMode == ScreenMode.FullScreen) TrySet(() => o.FullScreen = true);

        SetState(SessionState.Connecting, $"正在連線到 {c.DisplayAddress} …");
        o.Connect();
    }

    public void Disconnect()
    {
        if (_rdp == null || !IsActive) return;
        try { _rdp.Ocx.Disconnect(); } catch { /* ignore */ }
    }

    public void EnterFullScreen()
    {
        if (_rdp == null || State != SessionState.Connected) return;
        try { _rdp.Ocx.FullScreen = true; } catch { /* ignore */ }
    }

    public new void Focus()
    {
        try { _rdp?.Focus(); } catch { /* ignore */ }
    }

    private void OnDisconnected(int reason)
    {
        if (State == SessionState.Disconnected) return;
        string desc = "";
        try
        {
            if (reason >= 0 && _rdp != null)
            {
                dynamic o = _rdp.Ocx;
                uint ext = Convert.ToUInt32(o.ExtendedDisconnectReason);
                desc = (string)o.GetErrorDescription((uint)reason, ext);
            }
        }
        catch { /* 舊版元件不支援 */ }

        bool normal = reason is 1 or 2 or 3;
        string msg = reason switch
        {
            < 0 => $"RDP 元件發生嚴重錯誤 (代碼 {-reason})",
            1 => "已由本機中斷連線",
            2 => "遠端使用者已登出或中斷連線",
            3 => "伺服器已中斷連線",
            _ => $"連線失敗或中斷 (代碼 {reason})",
        };
        if (!string.IsNullOrWhiteSpace(desc) && !normal) msg += "\n" + desc;

        SetState(SessionState.Disconnected, msg);
        SessionEvent?.Invoke(this, normal ? "中斷連線" : "連線失敗", msg.Replace('\n', ' '));

        // 不可在 ActiveX 自己的事件中釋放它,延後處理
        BeginInvoke(new MethodInvoker(() =>
        {
            DestroyClient();
            _overlayTitle.Text = $"{Connection.Name} - 已中斷連線";
            _overlayText.Text = msg;
            _overlay.Visible = true;
            _overlay.BringToFront();
            CenterOverlay();
        }));
    }

    private void SetState(SessionState state, string message)
    {
        State = state;
        LastMessage = message;
        Text = state switch
        {
            SessionState.Connecting => $"{Connection.Name} (連線中)",
            SessionState.Disconnected => $"{Connection.Name} (已中斷)",
            _ => Connection.Name,
        };
        StateChanged?.Invoke(this);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_loggedIn && State == SessionState.Connected && Connection.ScreenMode == ScreenMode.FitWindow)
        {
            _resizeTimer.Stop();
            _resizeTimer.Start();
        }
    }

    /// <summary>視窗大小改變時,動態調整遠端解析度 (需 RDP 8.1 以上伺服器)</summary>
    private void ApplyResize()
    {
        if (_rdp == null || State != SessionState.Connected) return;
        var size = ClientSize;
        int w = size.Width & ~1, h = size.Height;
        if (w < 200 || h < 200 || size == _lastSize) return;
        try
        {
            _rdp.Ocx.UpdateSessionDisplaySettings((uint)w, (uint)h, (uint)w, (uint)h, 0u, 100u, 100u);
            _lastSize = size;
        }
        catch { /* 伺服器或元件不支援 */ }
    }

    private void CenterOverlay()
    {
        _overlayBox.Location = new Point(
            Math.Max(10, (_overlay.ClientSize.Width - _overlayBox.Width) / 2),
            Math.Max(10, (_overlay.ClientSize.Height - _overlayBox.Height) / 2));
    }

    private void DestroyClient()
    {
        var r = _rdp;
        if (r == null) return;
        _rdp = null;
        Controls.Remove(r);
        try { r.Dispose(); } catch { /* ignore */ }
    }

    private static bool TrySet(Action a)
    {
        try { a(); return true; } catch { return false; }
    }

    private static dynamic? TryGet(Func<dynamic> f)
    {
        try { return f(); } catch { return null; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _resizeTimer.Dispose();
            Disconnect();
            DestroyClient();
        }
        base.Dispose(disposing);
    }
}
