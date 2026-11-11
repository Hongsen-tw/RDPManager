// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.Diagnostics;
using RDPManager.Models;
using RDPManager.Services;
using static RDPManager.Forms.FormHelpers;

namespace RDPManager.Forms;

public class SettingsForm : Form
{
    private readonly AppSettings _s;
    private readonly ComboBox _cboLaunch = DropList("內嵌分頁 (在本程式中開啟)", "外部 mstsc 視窗");
    private readonly CheckBox _chkReuse = new() { Text = "連線已開啟時切換到該分頁,不重複開啟", AutoSize = true };
    private readonly CheckBox _chkTray = new() { Text = "關閉視窗時縮小到系統匣", AutoSize = true };
    private readonly CheckBox _chkCheck = new() { Text = "啟動時檢查所有主機狀態", AutoSize = true };
    private readonly NumericUpDown _numTimeout = new() { Minimum = 200, Maximum = 10000, Increment = 100, Width = 80 };
    private readonly NumericUpDown _numAuto = new() { Minimum = 0, Maximum = 1440, Width = 80 };
    private readonly CheckBox _chkConfirm = new() { Text = "刪除連線前確認", AutoSize = true };
    private readonly CheckBox _chkBackup = new() { Text = "每天自動備份連線清單", AutoSize = true };
    private readonly CheckBox _chkFull = new() { Text = "所有連線一律以全螢幕開啟", AutoSize = true };
    private readonly CheckBox _chkHover = new() { Text = "全螢幕時滑鼠移到螢幕左緣,自動展開側邊面板", AutoSize = true };
    private readonly ComboBox _cboKeys = DropList("僅全螢幕時傳送到遠端", "一律傳送到遠端", "保留在本機");
    private static readonly int[] KeyModes = { 2, 1, 0 };
    private readonly CheckBox _chkRemember = new() { Text = "新連線預設「記住密碼」", AutoSize = true };
    private readonly CheckBox _chkAsk = new() { Text = "未儲存密碼時,連線前詢問帳號密碼", AutoSize = true };
    private readonly TextBox _txtVnc = new() { Width = 180, PlaceholderText = "空白 = 自動偵測" };
    private readonly Label _lblVnc = new() { AutoSize = true, MaximumSize = new Size(360, 0) };

    public SettingsForm(AppSettings settings)
    {
        _s = settings;
        SetupDialog(this, "設定", new Size(540, 690));

        var t = NewTable();
        AddRow(t, "預設連線方式", _cboLaunch);
        AddRow(t, "全螢幕", _chkFull, false);
        AddRow(t, "", _chkHover, false);
        AddRow(t, "Windows 組合鍵", _cboKeys, false);
        AddRow(t, "", new Label { Text = "(Alt+Tab、Win 鍵等;已連線的工作階段需重新連線才會套用)", AutoSize = true, ForeColor = SystemColors.GrayText }, false);
        AddRow(t, "密碼", _chkAsk, false);
        AddRow(t, "", _chkRemember, false);
        AddRow(t, "", new Label { Text = "(預設不儲存密碼;個別連線可在編輯視窗勾選「記住密碼」)", AutoSize = true, ForeColor = SystemColors.GrayText }, false);
        var btnBrowse = new Button { Text = "瀏覽…", AutoSize = true };
        var btnDetect = new Button { Text = "自動偵測", AutoSize = true };
        btnBrowse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Title = "選擇 UltraVNC Viewer", Filter = "vncviewer.exe|vncviewer.exe|程式 (*.exe)|*.exe" };
            if (dlg.ShowDialog(this) == DialogResult.OK) { _txtVnc.Text = dlg.FileName; UpdateVncStatus(); }
        };
        btnDetect.Click += (_, _) => { _txtVnc.Text = VncLauncher.Detect() ?? ""; UpdateVncStatus(); };
        _txtVnc.TextChanged += (_, _) => UpdateVncStatus();
        AddRow(t, "UltraVNC Viewer", Flow(_txtVnc, btnBrowse, btnDetect), false);
        var download = new LinkLabel { Text = "下載 UltraVNC", AutoSize = true };
        download.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(VncLauncher.DownloadUrl) { UseShellExecute = true });
        AddRow(t, "", Flow(_lblVnc, download), false);
        AddRow(t, "一般", _chkReuse, false);
        AddRow(t, "", _chkTray, false);
        AddRow(t, "", _chkConfirm, false);
        AddRow(t, "", _chkCheck, false);
        AddRow(t, "狀態檢查逾時", Flow(_numTimeout, new Label { Text = "毫秒", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }), false);
        AddRow(t, "自動檢查間隔", Flow(_numAuto, new Label { Text = "分鐘 (0 = 關閉)", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }), false);
        AddRow(t, "", _chkBackup, false);

        var openDir = new LinkLabel { Text = "開啟資料夾:" + DataStore.DataDir, AutoSize = true, MaximumSize = new Size(430, 0) };
        openDir.LinkClicked += (_, _) => Process.Start("explorer.exe", DataStore.DataDir);
        AddFull(t, openDir);

        var buttons = DialogButtons(this, out var ok, out _);
        ok.Click += (_, _) => Save();
        Controls.Add(t);
        Controls.Add(buttons);

        _cboLaunch.SelectedIndex = settings.DefaultLaunchMode == LaunchMode.External ? 1 : 0;
        _chkReuse.Checked = settings.ReuseOpenSession;
        _chkTray.Checked = settings.MinimizeToTray;
        _chkCheck.Checked = settings.CheckStatusOnStartup;
        _numTimeout.Value = Math.Clamp(settings.StatusTimeoutMs, 200, 10000);
        _numAuto.Value = Math.Clamp(settings.AutoCheckMinutes, 0, 1440);
        _chkConfirm.Checked = settings.ConfirmDelete;
        _chkBackup.Checked = settings.DailyAutoBackup;
        _chkFull.Checked = settings.AlwaysFullScreen;
        _chkRemember.Checked = settings.RememberPasswordByDefault;
        _chkAsk.Checked = settings.AskPasswordBeforeConnect;
        _chkHover.Checked = settings.SidebarHoverExpand;
        _cboKeys.SelectedIndex = Math.Max(0, Array.IndexOf(KeyModes, settings.KeyboardHookMode));
        _txtVnc.Text = settings.UltraVncPath;
        UpdateVncStatus();
    }

    private void UpdateVncStatus()
    {
        var path = VncLauncher.Resolve(_txtVnc.Text.Trim());
        if (_txtVnc.Text.Trim().Length > 0 && !File.Exists(_txtVnc.Text.Trim()))
        {
            _lblVnc.ForeColor = Color.Firebrick;
            _lblVnc.Text = "✖ 找不到此檔案";
        }
        else if (path != null)
        {
            _lblVnc.ForeColor = Color.ForestGreen;
            _lblVnc.Text = _txtVnc.Text.Trim().Length > 0 ? "✔ 已設定" : "✔ 已自動偵測到:" + path;
        }
        else
        {
            _lblVnc.ForeColor = Color.DarkOrange;
            _lblVnc.Text = "尚未安裝 UltraVNC Viewer (VNC 連線需要)";
        }
    }

    private void Save()
    {
        _s.DefaultLaunchMode = _cboLaunch.SelectedIndex == 1 ? LaunchMode.External : LaunchMode.Embedded;
        _s.ReuseOpenSession = _chkReuse.Checked;
        _s.MinimizeToTray = _chkTray.Checked;
        _s.CheckStatusOnStartup = _chkCheck.Checked;
        _s.StatusTimeoutMs = (int)_numTimeout.Value;
        _s.AutoCheckMinutes = (int)_numAuto.Value;
        _s.ConfirmDelete = _chkConfirm.Checked;
        _s.DailyAutoBackup = _chkBackup.Checked;
        _s.AlwaysFullScreen = _chkFull.Checked;
        _s.UltraVncPath = _txtVnc.Text.Trim();
        _s.RememberPasswordByDefault = _chkRemember.Checked;
        _s.AskPasswordBeforeConnect = _chkAsk.Checked;
        _s.SidebarHoverExpand = _chkHover.Checked;
        _s.KeyboardHookMode = KeyModes[_cboKeys.SelectedIndex];
    }
}
