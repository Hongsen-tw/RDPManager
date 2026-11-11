// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using RDPManager.Models;
using RDPManager.Services;
using static RDPManager.Forms.FormHelpers;

namespace RDPManager.Forms;

/// <summary>新增 / 編輯連線 (同一個連線設定可同時設定 RDP 與 VNC)</summary>
public class ConnectionEditForm : Form
{
    private readonly RdpConnection _conn;
    private readonly bool _rememberDefault;

    // 一般
    private readonly ComboBox _cboProtocol = DropList("RDP (Windows 遠端桌面)", "VNC (UltraVNC Viewer)");
    private readonly TextBox _txtName = new();
    private readonly TextBox _txtHost = new() { PlaceholderText = "IP 或主機名稱,例如 192.168.1.10" };
    private readonly ComboBox _cboGroup = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly CheckBox _chkFav = new() { Text = "加入我的最愛", AutoSize = true };
    private readonly TextBox _txtNotes = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 90 };

    // RDP
    private readonly NumericUpDown _numPort = new() { Minimum = 1, Maximum = 65535, Value = 3389, Width = 90 };
    private readonly Button _btnTest = new() { Text = "測試連線埠", AutoSize = true };
    private readonly Label _lblTest = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TextBox _txtUser = new() { Width = 200 };
    private readonly TextBox _txtDomain = new() { Width = 200, PlaceholderText = "選填" };
    private readonly TextBox _txtPassword = new() { Width = 200, UseSystemPasswordChar = true };
    private readonly CheckBox _chkShowPwd = new() { Text = "顯示", AutoSize = true };
    private readonly CheckBox _chkRemember = new() { Text = "記住密碼", AutoSize = true };
    private bool _hadSavedPassword;

    // VNC
    private readonly NumericUpDown _numVncPort = new() { Minimum = 1, Maximum = 65535, Value = 5900, Width = 90 };
    private readonly Button _btnVncTest = new() { Text = "測試連線埠", AutoSize = true };
    private readonly Label _lblVncTest = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TextBox _txtVncPassword = new() { Width = 200, UseSystemPasswordChar = true };
    private readonly CheckBox _chkVncShowPwd = new() { Text = "顯示", AutoSize = true };
    private readonly CheckBox _chkVncRemember = new() { Text = "記住密碼", AutoSize = true };
    private readonly TextBox _txtVncUser = new() { Width = 200, PlaceholderText = "選填 (UltraVNC MS-Logon)" };
    private readonly CheckBox _chkViewOnly = new() { Text = "僅檢視,不控制遠端", AutoSize = true };
    private bool _hadSavedVncPassword;

    // 顯示
    private readonly ComboBox _cboScreen = DropList("符合分頁大小 (自動調整解析度)", "全螢幕", "自訂解析度");
    private readonly NumericUpDown _numW = new() { Minimum = 640, Maximum = 8192, Value = 1920, Width = 80 };
    private readonly NumericUpDown _numH = new() { Minimum = 480, Maximum = 8192, Value = 1080, Width = 80 };
    private readonly ComboBox _cboColor = DropList("15 位元", "16 位元", "24 位元", "32 位元 (最高品質)");
    private readonly CheckBox _chkSmart = new() { Text = "智慧縮放 (遠端畫面縮放以符合視窗)", AutoSize = true };
    private readonly CheckBox _chkMulti = new() { Text = "使用所有螢幕 (僅外部 mstsc)", AutoSize = true };

    // 本機資源
    private readonly CheckBox _chkClip = new() { Text = "剪貼簿 (RDP 與 VNC)", AutoSize = true };
    private readonly CheckBox _chkDrives = new() { Text = "本機磁碟機", AutoSize = true };
    private readonly CheckBox _chkPrinters = new() { Text = "印表機", AutoSize = true };
    private readonly ComboBox _cboAudio = DropList("在此電腦播放", "在遠端電腦播放", "不要播放");

    // 進階
    private readonly ComboBox _cboLaunch = DropList("使用全域預設", "內嵌分頁", "外部 mstsc 視窗");
    private readonly CheckBox _chkAdmin = new() { Text = "連線到主控台 / 系統管理工作階段 (/admin)", AutoSize = true };
    private readonly TextBox _txtMac = new() { Width = 200, PlaceholderText = "例如 00-11-22-33-44-55" };

    private static readonly int[] ColorDepths = { 15, 16, 24, 32 };

    /// <param name="rememberDefault">新連線 (尚未儲存密碼) 時「記住密碼」的預設值</param>
    public ConnectionEditForm(RdpConnection conn, IEnumerable<string> groups, string title, bool rememberDefault = false)
    {
        _conn = conn;
        _rememberDefault = rememberDefault;
        SetupDialog(this, title, new Size(580, 560));

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(10, 4) };
        tabs.TabPages.Add(BuildGeneral(groups));
        tabs.TabPages.Add(BuildRdp());
        tabs.TabPages.Add(BuildVnc());
        tabs.TabPages.Add(BuildDisplay());
        tabs.TabPages.Add(BuildResources());
        tabs.TabPages.Add(BuildAdvanced());

        var buttons = DialogButtons(this, out var ok, out _, "儲存");
        ok.Click += OnOk;

        Controls.Add(tabs);
        Controls.Add(buttons);
        LoadValues();
        Shown += (_, _) => (_txtHost.Text.Length == 0 ? _txtHost : _txtName).Focus();
    }

    private static Label Note(string text) => new()
    {
        Text = text, AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(500, 0),
        Margin = new Padding(3, 10, 3, 3),
    };

    private TabPage BuildGeneral(IEnumerable<string> groups)
    {
        var t = NewTable();
        _cboGroup.Items.AddRange(groups.Cast<object>().ToArray());
        _txtHost.Leave += (_, _) =>
        {
            // 若輸入 host:port,連接埠填到目前預設連線類型
            if (_txtHost.Text.Contains(':') && AddressParser.TryParse(_txtHost.Text, out var h, out var p))
            {
                _txtHost.Text = h;
                if (_cboProtocol.SelectedIndex == (int)ConnectionProtocol.Vnc) _numVncPort.Value = p;
                else _numPort.Value = p;
            }
            if (_txtName.Text.Trim().Length == 0) _txtName.Text = _txtHost.Text.Trim();
        };

        AddRow(t, "名稱", _txtName);
        AddRow(t, "主機 / IP *", _txtHost);
        AddRow(t, "預設連線類型", _cboProtocol, false);
        AddRow(t, "群組", _cboGroup);
        AddRow(t, "", _chkFav, false);
        AddRow(t, "備註", _txtNotes);
        AddFull(t, Note(
            "※ 同一個連線可以用 RDP 或 VNC 連線:雙擊 / Enter 使用「預設連線類型」;" +
            "按右鍵選「以 VNC 連線」或工具列「VNC」按鈕即可改用 VNC。\n" +
            "※ RDP 與 VNC 的帳號密碼分別在「RDP」與「VNC」分頁設定。"));
        return new TabPage("一般") { Controls = { t } };
    }

    private TabPage BuildRdp()
    {
        var t = NewTable();
        _chkShowPwd.CheckedChanged += (_, _) => _txtPassword.UseSystemPasswordChar = !_chkShowPwd.Checked;
        _chkRemember.CheckedChanged += (_, _) => UpdatePasswordEnabled(_chkRemember, _txtPassword, _chkShowPwd);
        _btnTest.Click += async (_, _) => await TestPortAsync(_numPort, _btnTest, _lblTest);

        AddRow(t, "RDP 連接埠", Flow(_numPort, _btnTest, _lblTest), false);
        AddRow(t, "使用者名稱", _txtUser, false);
        AddRow(t, "網域", _txtDomain, false);
        AddRow(t, "密碼", Flow(_chkRemember, _txtPassword, _chkShowPwd), false);
        AddFull(t, Note("※ 預設不儲存密碼,每次連線時輸入。勾選「記住密碼」才會儲存,並以 Windows DPAPI 加密 (只有目前的 Windows 帳戶能解密)。"));
        return new TabPage("RDP") { Controls = { t } };
    }

    private TabPage BuildVnc()
    {
        var t = NewTable();
        _chkVncShowPwd.CheckedChanged += (_, _) => _txtVncPassword.UseSystemPasswordChar = !_chkVncShowPwd.Checked;
        _chkVncRemember.CheckedChanged += (_, _) => UpdatePasswordEnabled(_chkVncRemember, _txtVncPassword, _chkVncShowPwd);
        _btnVncTest.Click += async (_, _) => await TestPortAsync(_numVncPort, _btnVncTest, _lblVncTest);

        AddRow(t, "VNC 連接埠", Flow(_numVncPort, _btnVncTest, _lblVncTest), false);
        AddRow(t, "VNC 密碼", Flow(_chkVncRemember, _txtVncPassword, _chkVncShowPwd), false);
        AddRow(t, "MS-Logon 帳號", _txtVncUser, false);
        AddRow(t, "", _chkViewOnly, false);
        AddFull(t, Note(
            "※ VNC 連線以本機的 UltraVNC Viewer 開啟 (請在「設定」確認已偵測到 vncviewer.exe)。\n" +
            "※ VNC 密碼是遠端 VNC 伺服器設定的密碼 (不是 Windows 密碼),傳統 VNC 密碼最多 8 個字元;使用 UltraVNC MS-Logon 時才需要填帳號。\n" +
            "※ 全螢幕 / 縮放沿用「顯示」分頁設定,剪貼簿沿用「本機資源」分頁設定。\n" +
            "※ 一般 VNC 連線沒有加密,請只在內網或 VPN 中使用。"));
        return new TabPage("VNC") { Controls = { t } };
    }

    private TabPage BuildDisplay()
    {
        var t = NewTable();
        _cboScreen.SelectedIndexChanged += (_, _) => _numW.Enabled = _numH.Enabled = _cboScreen.SelectedIndex == 2;
        var presets = DropList("自訂…", "1280 x 720", "1366 x 768", "1600 x 900", "1920 x 1080", "2560 x 1440", "3840 x 2160");
        presets.Width = 120;
        presets.SelectedIndexChanged += (_, _) =>
        {
            if (presets.SelectedIndex <= 0) return;
            var parts = presets.Text.Split('x');
            _numW.Value = int.Parse(parts[0].Trim());
            _numH.Value = int.Parse(parts[1].Trim());
        };

        AddRow(t, "畫面模式", _cboScreen);
        AddRow(t, "解析度", Flow(_numW, new Label { Text = "x", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }, _numH, presets), false);
        AddRow(t, "色彩深度", _cboColor, false);
        AddRow(t, "", _chkSmart, false);
        AddRow(t, "", _chkMulti, false);
        return new TabPage("顯示") { Controls = { t } };
    }

    private TabPage BuildResources()
    {
        var t = NewTable();
        AddRow(t, "重新導向", _chkClip, false);
        AddRow(t, "", _chkDrives, false);
        AddRow(t, "", _chkPrinters, false);
        AddRow(t, "遠端音訊", _cboAudio, false);
        return new TabPage("本機資源") { Controls = { t } };
    }

    private TabPage BuildAdvanced()
    {
        var t = NewTable();
        AddRow(t, "RDP 連線方式", _cboLaunch, false);
        AddRow(t, "", _chkAdmin, false);
        AddRow(t, "MAC 位址", _txtMac, false);
        AddFull(t, Note("填寫 MAC 位址後,可在右鍵選單使用「網路喚醒 (Wake-on-LAN)」開啟關機中的電腦 (需同一區網且主機已啟用 WOL)。"));
        if (_conn.LastConnectedAt != null || _conn.ConnectCount > 0)
        {
            AddFull(t, new Label
            {
                Text = $"建立時間:{_conn.CreatedAt:yyyy/MM/dd HH:mm}\n最後連線:{_conn.LastConnectedAt:yyyy/MM/dd HH:mm}\n連線次數:{_conn.ConnectCount}",
                AutoSize = true, Margin = new Padding(3, 14, 3, 3),
            });
        }
        return new TabPage("進階") { Controls = { t } };
    }

    private void LoadValues()
    {
        var c = _conn;
        _cboProtocol.SelectedIndex = (int)c.Protocol;
        _txtName.Text = c.Name;
        _txtHost.Text = c.Host;
        _cboGroup.Text = c.Group;
        _chkFav.Checked = c.IsFavorite;
        _txtNotes.Text = c.Notes;

        _numPort.Value = Math.Clamp(c.Port, 1, 65535);
        _txtUser.Text = c.Username;
        _txtDomain.Text = c.Domain;
        _hadSavedPassword = !string.IsNullOrEmpty(c.EncryptedPassword);
        _txtPassword.Text = PasswordProtector.Unprotect(c.EncryptedPassword);
        _chkRemember.Checked = _hadSavedPassword || _rememberDefault;
        UpdatePasswordEnabled(_chkRemember, _txtPassword, _chkShowPwd);

        _numVncPort.Value = Math.Clamp(c.VncPortValue, 1, 65535);
        _txtVncUser.Text = c.VncUsername;
        _chkViewOnly.Checked = c.VncViewOnly;
        _hadSavedVncPassword = !string.IsNullOrEmpty(c.VncEncryptedPassword);
        _txtVncPassword.Text = PasswordProtector.Unprotect(c.VncEncryptedPassword);
        _chkVncRemember.Checked = _hadSavedVncPassword || _rememberDefault;
        UpdatePasswordEnabled(_chkVncRemember, _txtVncPassword, _chkVncShowPwd);

        _cboScreen.SelectedIndex = (int)c.ScreenMode;
        _numW.Value = Math.Clamp(c.DesktopWidth, 640, 8192);
        _numH.Value = Math.Clamp(c.DesktopHeight, 480, 8192);
        _numW.Enabled = _numH.Enabled = c.ScreenMode == ScreenMode.Custom;
        _cboColor.SelectedIndex = Math.Max(0, Array.IndexOf(ColorDepths, c.ColorDepth));
        _chkSmart.Checked = c.SmartSizing;
        _chkMulti.Checked = c.MultiMonitor;

        _chkClip.Checked = c.RedirectClipboard;
        _chkDrives.Checked = c.RedirectDrives;
        _chkPrinters.Checked = c.RedirectPrinters;
        _cboAudio.SelectedIndex = (int)c.AudioMode;

        _cboLaunch.SelectedIndex = (int)c.LaunchMode;
        _chkAdmin.Checked = c.AdminSession;
        _txtMac.Text = c.MacAddress;
    }

    private static void UpdatePasswordEnabled(CheckBox remember, TextBox password, CheckBox show)
    {
        password.Enabled = show.Enabled = remember.Checked;
        password.PlaceholderText = remember.Checked ? "" : "連線時輸入";
        if (!remember.Checked) password.Clear();
    }

    private void OnOk(object? sender, EventArgs e)
    {
        var hostText = _txtHost.Text.Trim();
        if (!AddressParser.TryParse(hostText, out var host, out var parsedPort))
        {
            MessageBox.Show(this, "請輸入有效的主機名稱或 IP 位址。", "資料不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            _txtHost.Focus();
            return;
        }
        if (_txtMac.Text.Trim().Length > 0 && !WakeOnLan.TryParseMac(_txtMac.Text, out _))
        {
            MessageBox.Show(this, "MAC 位址格式不正確 (應為 12 位十六進位數字)。", "資料錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        var c = _conn;
        c.Protocol = (ConnectionProtocol)_cboProtocol.SelectedIndex;
        bool portInHost = hostText.Contains(':') && !host.Contains(':');
        c.Host = host;
        c.Name = _txtName.Text.Trim().Length > 0 ? _txtName.Text.Trim() : host;
        c.Group = _cboGroup.Text.Trim();
        c.IsFavorite = _chkFav.Checked;
        c.Notes = _txtNotes.Text.Trim();

        // RDP
        c.Port = portInHost && !c.IsVnc ? parsedPort : (int)_numPort.Value;
        c.Username = _txtUser.Text.Trim();
        c.Domain = _txtDomain.Text.Trim();
        c.EncryptedPassword = _chkRemember.Checked ? PasswordProtector.Protect(_txtPassword.Text) : null;
        // 取消記住密碼時,一併清除 Windows 認證管理員中的暫存帳密
        if (_hadSavedPassword && c.EncryptedPassword == null)
        {
            try { CredentialManager.DeleteRdpCredential(host); } catch { /* ignore */ }
        }

        // VNC (一律寫入 VncPort,代表已是新格式)
        c.VncPort = portInHost && c.IsVnc ? parsedPort : (int)_numVncPort.Value;
        c.VncUsername = _txtVncUser.Text.Trim();
        c.VncEncryptedPassword = _chkVncRemember.Checked ? PasswordProtector.Protect(_txtVncPassword.Text) : null;
        c.VncViewOnly = _chkViewOnly.Checked;

        c.ScreenMode = (ScreenMode)_cboScreen.SelectedIndex;
        c.DesktopWidth = (int)_numW.Value;
        c.DesktopHeight = (int)_numH.Value;
        c.ColorDepth = ColorDepths[_cboColor.SelectedIndex];
        c.SmartSizing = _chkSmart.Checked;
        c.MultiMonitor = _chkMulti.Checked;

        c.RedirectClipboard = _chkClip.Checked;
        c.RedirectDrives = _chkDrives.Checked;
        c.RedirectPrinters = _chkPrinters.Checked;
        c.AudioMode = (AudioMode)_cboAudio.SelectedIndex;

        c.LaunchMode = (LaunchMode)_cboLaunch.SelectedIndex;
        c.AdminSession = _chkAdmin.Checked;
        c.MacAddress = _txtMac.Text.Trim();
    }

    private async Task TestPortAsync(NumericUpDown portBox, Button button, Label result)
    {
        if (!AddressParser.TryParse(_txtHost.Text, out var host, out _))
        {
            result.ForeColor = Color.Firebrick;
            result.Text = "請先在「一般」輸入主機";
            return;
        }
        int port = (int)portBox.Value;
        button.Enabled = false;
        result.ForeColor = SystemColors.GrayText;
        result.Text = "測試中…";
        var (ok, ms) = await HostChecker.CheckAsync(host, port, 3000);
        if (IsDisposed) return;
        result.ForeColor = ok ? Color.ForestGreen : Color.Firebrick;
        result.Text = ok ? $"✔ 可連線 ({ms} ms)" : "✖ 無法連線";
        button.Enabled = true;
    }
}
