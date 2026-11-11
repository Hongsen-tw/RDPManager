// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using RDPManager.Models;
using static RDPManager.Forms.FormHelpers;

namespace RDPManager.Forms;

/// <summary>連線前輸入帳號密碼 (未儲存密碼時)</summary>
public class PasswordPromptForm : Form
{
    private readonly TextBox _txtUser = new() { Width = 250 };
    private readonly TextBox _txtPassword = new() { Width = 250, UseSystemPasswordChar = true };
    private readonly CheckBox _chkShow = new() { Text = "顯示", AutoSize = true };
    private readonly CheckBox _chkRemember = new() { Text = "記住密碼 (以 Windows DPAPI 加密儲存)", AutoSize = true };

    /// <summary>使用者名稱 (可為 DOMAIN\user)</summary>
    public string UserName => _txtUser.Text.Trim();
    public string Password => _txtPassword.Text;
    // 注意:對話框關閉後 Control.Visible 一律為 false,所以另外記錄是否允許記住
    public bool Remember => _allowRemember && _chkRemember.Checked;
    private readonly bool _allowRemember;

    /// <param name="vnc">true = 詢問 VNC 密碼 (帳號為 MS-Logon,選填)</param>
    public PasswordPromptForm(RdpConnection c, bool allowRemember, bool vnc = false)
    {
        _allowRemember = allowRemember;
        SetupDialog(this, $"{(vnc ? "VNC" : "RDP")} 連線到 {c.Name}", new Size(440, 260));
        TopMost = true; // 全螢幕時也要顯示在最上層

        var t = NewTable();
        var title = new Label
        {
            Text = $"{c.Name}\n{(vnc ? $"VNC · {c.VncAddress}" : $"RDP · {c.RdpAddress}")}", AutoSize = true,
            Font = new Font(UiFont, FontStyle.Bold), Margin = new Padding(3, 0, 3, 8),
        };
        AddFull(t, title);
        AddRow(t, "使用者名稱", _txtUser, false);
        AddRow(t, "密碼", Flow(_txtPassword, _chkShow), false);
        AddRow(t, "", _chkRemember, false);
        var note = new Label
        {
            AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(380, 0),
            Text = "未勾選時,密碼只用於這次連線,不會儲存。",
        };
        AddFull(t, note);

        _chkRemember.Visible = allowRemember;
        _chkShow.CheckedChanged += (_, _) => _txtPassword.UseSystemPasswordChar = !_chkShow.Checked;
        _chkRemember.CheckedChanged += (_, _) => note.Visible = !_chkRemember.Checked;
        _txtUser.Text = vnc ? c.VncUsername : c.FullUserName;
        _txtUser.PlaceholderText = vnc ? "VNC 通常不需要 (MS-Logon 才需要)" : "例如 administrator 或 DOMAIN\\user";

        var buttons = DialogButtons(this, out _, out _, "連線");
        Controls.Add(t);
        Controls.Add(buttons);
        Shown += (_, _) =>
        {
            Activate();
            (_txtUser.Text.Length == 0 && !vnc ? _txtUser : _txtPassword).Focus();
        };
    }

    /// <summary>把 DOMAIN\user 拆成網域與使用者名稱</summary>
    public static (string Domain, string User) SplitUser(string full)
    {
        var i = full.IndexOf('\\');
        return i > 0 ? (full[..i], full[(i + 1)..]) : ("", full);
    }
}
