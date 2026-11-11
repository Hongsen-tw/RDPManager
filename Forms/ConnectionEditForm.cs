// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using RDPManager.Models;
using RDPManager.Services;
using static RDPManager.Forms.FormHelpers;

namespace RDPManager.Forms;

/// <summary>新增 / 編輯連線</summary>
public class ConnectionEditForm : Form
{
    private readonly RdpConnection _conn;

    // 一般
    private readonly TextBox _txtName = new();
    private readonly TextBox _txtHost = new() { PlaceholderText = "IP 或主機名稱,例如 192.168.1.10 或 server01:3390" };
    private readonly NumericUpDown _numPort = new() { Minimum = 1, Maximum = 65535, Value = 3389, Width = 90 };
    private readonly TextBox _txtUser = new() { Width = 200 };
    private readonly TextBox _txtDomain = new() { Width = 200, PlaceholderText = "選填" };
    private readonly TextBox _txtPassword = new() { Width = 200, UseSystemPasswordChar = true };
    private readonly CheckBox _chkShowPwd = new() { Text = "顯示", AutoSize = true };
    private readonly CheckBox _chkRemember = new() { Text = "記住密碼", AutoSize = true };
    private readonly bool _rememberDefault;
    private bool _hadSavedPassword;
    private readonly ComboBox _cboGroup = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly CheckBox _chkFav = new() { Text = "加入我的最愛", AutoSize = true };
    private readonly TextBox _txtNotes = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 80 };
    private readonly Button _btnTest = new() { Text = "測試連線埠", AutoSize = true };
    private readonly Label _lblTest = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    // 顯示
    private readonly ComboBox _cboScreen = DropList("符合分頁大小 (自動調整解析度)", "全螢幕", "自訂解析度");
    private readonly NumericUpDown _numW = new() { Minimum = 640, Maximum = 8192, Value = 1920, Width = 80 };
    private readonly NumericUpDown _numH = new() { Minimum = 480, Maximum = 8192, Value = 1080, Width = 80 };
    private readonly ComboBox _cboColor = DropList("15 位元", "16 位元", "24 位元", "32 位元 (最高品質)");
    private readonly CheckBox _chkSmart = new() { Text = "智慧縮放 (遠端畫面縮放以符合視窗)", AutoSize = true };
    private readonly CheckBox _chkMulti = new() { Text = "使用所有螢幕 (僅外部 mstsc)", AutoSize = true };

    // 本機資源
    private readonly CheckBox _chkClip = new() { Text = "剪貼簿", AutoSize = true };
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
        SetupDialog(this, title, new Size(540, 520));

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 4) };
        tabs.TabPages.Add(BuildGeneral(groups));
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

    private TabPage BuildGeneral(IEnumerable<string> groups)
    {
        var t = NewTable();
        _cboGroup.Items.AddRange(groups.Cast<object>().ToArray());
        _chkShowPwd.CheckedChanged += (_, _) => _txtPassword.UseSystemPasswordChar = !_chkShowPwd.Checked;
        _chkRemember.CheckedChanged += (_, _) => UpdatePasswordEnabled();
        _txtHost.Leave += (_, _) =>
        {
            // 若輸入 host:port 自動拆開
            if (_txtHost.Text.Contains(':') && AddressParser.TryParse(_txtHost.Text, out var h, out var p))
            {
                _txtHost.Text = h;
                _numPort.Value = p;
            }
            if (_txtName.Text.Trim().Length == 0) _txtName.Text = _txtHost.Text.Trim();
        };
        _btnTest.Click += async (_, _) => await TestPortAsync();

        AddRow(t, "名稱", _txtName);
        AddRow(t, "主機 / IP *", _txtHost);
        AddRow(t, "連接埠", Flow(_numPort, _btnTest, _lblTest), false);
        AddRow(t, "使用者名稱", _txtUser, false);
        AddRow(t, "網域", _txtDomain, false);
        AddRow(t, "密碼", Flow(_chkRemember, _txtPassword, _chkShowPwd), false);
        AddRow(t, "群組", _cboGroup);
        AddRow(t, "", _chkFav, false);
        AddRow(t, "備註", _txtNotes);
        AddFull(t, new Label
        {
            Text = "※ 預設不儲存密碼,每次連線時輸入。勾選「記住密碼」才會儲存,並以 Windows DPAPI 加密 (只有目前的 Windows 帳戶能解密)。",
            AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(480, 0),
        });
        return new TabPage("一般") { Controls = { t }, Padding = new Padding(4) };
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
        AddRow(t, "連線方式", _cboLaunch, false);
        AddRow(t, "", _chkAdmin, false);
        AddRow(t, "MAC 位址", _txtMac, false);
        AddFull(t, new Label
        {
            Text = "填寫 MAC 位址後,可在右鍵選單使用「網路喚醒 (Wake-on-LAN)」開啟關機中的電腦 (需同一區網且主機已啟用 WOL)。",
            AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(480, 0),
        });
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
        _txtName.Text = c.Name;
        _txtHost.Text = c.Host;
        _numPort.Value = Math.Clamp(c.Port, 1, 65535);
        _txtUser.Text = c.Username;
        _txtDomain.Text = c.Domain;
        _hadSavedPassword = !string.IsNullOrEmpty(c.EncryptedPassword);
        _txtPassword.Text = PasswordProtector.Unprotect(c.EncryptedPassword);
        _chkRemember.Checked = _hadSavedPassword || _rememberDefault;
        UpdatePasswordEnabled();
        _cboGroup.Text = c.Group;
        _chkFav.Checked = c.IsFavorite;
        _txtNotes.Text = c.Notes;

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

    private void UpdatePasswordEnabled()
    {
        _txtPassword.Enabled = _chkShowPwd.Enabled = _chkRemember.Checked;
        _txtPassword.PlaceholderText = _chkRemember.Checked ? "" : "連線時輸入";
        if (!_chkRemember.Checked) _txtPassword.Clear();
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
        c.Host = host;
        c.Port = hostText.Contains(':') && !host.Contains(':') ? parsedPort : (int)_numPort.Value;
        c.Name = _txtName.Text.Trim().Length > 0 ? _txtName.Text.Trim() : host;
        c.Username = _txtUser.Text.Trim();
        c.Domain = _txtDomain.Text.Trim();
        c.EncryptedPassword = _chkRemember.Checked ? PasswordProtector.Protect(_txtPassword.Text) : null;
        // 取消記住密碼時,一併清除 Windows 認證管理員中的暫存帳密
        if (_hadSavedPassword && c.EncryptedPassword == null)
        {
            try { CredentialManager.DeleteRdpCredential(host); } catch { /* ignore */ }
        }
        c.Group = _cboGroup.Text.Trim();
        c.IsFavorite = _chkFav.Checked;
        c.Notes = _txtNotes.Text.Trim();

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

    private async Task TestPortAsync()
    {
        if (!AddressParser.TryParse(_txtHost.Text, out var host, out var port))
        {
            _lblTest.ForeColor = Color.Firebrick;
            _lblTest.Text = "請先輸入主機";
            return;
        }
        if (!_txtHost.Text.Contains(':') || host.Contains(':')) port = (int)_numPort.Value;
        _btnTest.Enabled = false;
        _lblTest.ForeColor = SystemColors.GrayText;
        _lblTest.Text = "測試中…";
        var (ok, ms) = await HostChecker.CheckAsync(host, port, 3000);
        if (IsDisposed) return;
        _lblTest.ForeColor = ok ? Color.ForestGreen : Color.Firebrick;
        _lblTest.Text = ok ? $"✔ 可連線 ({ms} ms)" : "✖ 無法連線";
        _btnTest.Enabled = true;
    }
}
