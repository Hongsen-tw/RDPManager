// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.Diagnostics;
using RDPManager.Controls;
using RDPManager.Models;
using RDPManager.Services;

namespace RDPManager.Forms;

public class MainForm : Form
{
    private readonly DataStore _store = new();

    private readonly MenuStrip _menu = new();
    private readonly ToolStrip _toolbar = new() { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 2, 6, 2) };
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _lblMessage = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _lblCount = new();
    private readonly ToolStripStatusLabel _lblSessions = new();

    private readonly SplitContainer _split = new() { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false, AllowDrop = true, FullRowSelect = true, ShowLines = false, ItemHeight = 26, BorderStyle = BorderStyle.None };
    private readonly TabControl _tabs = new() { ShowToolTips = true, Padding = new Point(10, 4) };
    /// <summary>分頁容器:全螢幕時把頁籤列推出可視範圍 (不重建 ActiveX 視窗)</summary>
    private readonly Panel _sessionHost = new() { Dock = DockStyle.Fill };

    // 全螢幕工作區
    private readonly FullScreenSidebar _sidebar = new() { Visible = false };
    private readonly Button _sidebarHandle = new();
    private readonly System.Windows.Forms.Timer _edgeTimer = new() { Interval = 100 };
    private bool _fullScreen;
    private bool _fsTransition;
    private bool _sidebarExpanded;
    private Rectangle _restoreBounds;
    private FormWindowState _restoreState;
    private DateTime? _edgeSince;
    private DateTime? _leaveSince;
    private readonly TabPage _listPage = new("  連線清單  ");
    private readonly ListView _list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = true, AllowDrop = true, BorderStyle = BorderStyle.None };
    private readonly ToolStripTextBox _txtSearch = new() { Size = new Size(200, 25) };
    private readonly ToolStripTextBox _txtQuick = new() { Size = new Size(170, 25) };
    private readonly ContextMenuStrip _listMenu = new();
    private readonly ContextMenuStrip _treeMenu = new();
    private readonly NotifyIcon _tray = new();
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly ImageList _statusImages = AppIcon.CreateStatusImages();
    private readonly System.Windows.Forms.Timer _autoCheckTimer = new();

    private string _filterKey = "all";
    private int _sortColumn = 1;
    private bool _sortAsc = true;
    private bool _checking;
    private bool _reallyExit;
    private bool _trayTipShown;

    private readonly string? _startupTarget;

    public MainForm(string? startupTarget = null)
    {
        _startupTarget = startupTarget;
        Text = "RDP 遠端連線管理員";
        Icon = AppIcon.Get();
        Font = FormHelpers.UiFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(820, 520);
        Size = new Size(1200, 750);
        StartPosition = FormStartPosition.CenterScreen;

        BuildMenu();
        BuildToolbar();
        BuildStatusBar();
        BuildMainArea();
        BuildTray();

        Controls.Add(_split);
        Controls.Add(_toolbar);
        Controls.Add(_menu);
        Controls.Add(_statusStrip);
        MainMenuStrip = _menu;

        _autoCheckTimer.Tick += async (_, _) => await CheckStatusAsync(_store.Connections);
    }

    // =====================================================================
    // 介面建構
    // =====================================================================

    private static ToolStripMenuItem Item(string text, Keys keys, EventHandler onClick, string? shortcutText = null)
    {
        var i = new ToolStripMenuItem(text, null, onClick);
        if (keys != Keys.None) i.ShortcutKeys = keys;
        if (shortcutText != null) i.ShortcutKeyDisplayString = shortcutText;
        return i;
    }

    /// <summary>連線相關選單項目 (主選單與右鍵選單共用,所以每次產生新的)</summary>
    private ToolStripItem[] ConnectionItems(bool withShortcuts) => new ToolStripItem[]
    {
        Item("連線 (預設類型)(&C)", Keys.None, (_, _) => ConnectSelected(), "Enter"),
        Item("以 RDP 連線 (內嵌分頁)", Keys.None, (_, _) => ConnectSelected(LaunchMode.Embedded)),
        Item("以 RDP 連線 (外部 mstsc)", withShortcuts ? Keys.Control | Keys.Shift | Keys.Enter : Keys.None, (_, _) => ConnectSelected(LaunchMode.External)),
        Item("以 VNC 連線 (UltraVNC)(&V)", withShortcuts ? Keys.Control | Keys.Shift | Keys.V : Keys.None, (_, _) => ConnectSelected(protocol: ConnectionProtocol.Vnc)),
        new ToolStripSeparator(),
        Item("編輯(&E)...", withShortcuts ? Keys.Control | Keys.E : Keys.None, (_, _) => EditSelected()),
        Item("建立複本(&D)", withShortcuts ? Keys.Control | Keys.D : Keys.None, (_, _) => DuplicateSelected()),
        Item("刪除", Keys.None, (_, _) => DeleteSelected(), "Del"),
        new ToolStripSeparator(),
        Item("加入 / 移除我的最愛", withShortcuts ? Keys.Control | Keys.B : Keys.None, (_, _) => ToggleFavorite()),
        Item("移動到群組...", Keys.None, (_, _) => MoveSelectedToGroup()),
        Item("複製 IP 位址", Keys.None, (_, _) => CopyAddress()),
        Item("檢查選取主機狀態", Keys.None, async (_, _) => await CheckStatusAsync(SelectedConnections())),
        Item("網路喚醒 (Wake-on-LAN)", Keys.None, (_, _) => WakeSelected()),
        Item("匯出為 .rdp 檔...", Keys.None, (_, _) => ExportSelectedRdp()),
    };

    private void BuildMenu()
    {
        var mFile = new ToolStripMenuItem("檔案(&F)");
        mFile.DropDownItems.AddRange(new ToolStripItem[]
        {
            Item("新增連線(&N)...", Keys.Control | Keys.N, (_, _) => NewConnection()),
            new ToolStripSeparator(),
            Item("匯入 (JSON / CSV / .rdp)...", Keys.Control | Keys.I, (_, _) => ImportConnections()),
            Item("匯出為 JSON...", Keys.None, (_, _) => ExportConnections(json: true)),
            Item("匯出為 CSV (Excel)...", Keys.None, (_, _) => ExportConnections(json: false)),
            new ToolStripSeparator(),
            Item("立即備份", Keys.None, (_, _) => BackupNow()),
            Item("從備份還原...", Keys.None, (_, _) => RestoreBackup()),
            Item("開啟資料夾", Keys.None, (_, _) => Process.Start("explorer.exe", DataStore.DataDir)),
            new ToolStripSeparator(),
            Item("結束(&X)", Keys.None, (_, _) => ExitApp()),
        });

        var mConn = new ToolStripMenuItem("連線(&C)");
        mConn.DropDownItems.AddRange(ConnectionItems(withShortcuts: true));

        var mView = new ToolStripMenuItem("檢視(&V)");
        mView.DropDownItems.AddRange(new ToolStripItem[]
        {
            Item("搜尋", Keys.Control | Keys.F, (_, _) => _txtSearch.Focus()),
            Item("快速連線", Keys.Control | Keys.Q, (_, _) => _txtQuick.Focus()),
            Item("重新檢查全部狀態", Keys.F5, async (_, _) => await CheckStatusAsync(_store.Connections)),
            new ToolStripSeparator(),
            Item("回到連線清單", Keys.Control | Keys.L, (_, _) => _tabs.SelectedTab = _listPage),
            Item("全螢幕 / 視窗模式切換", Keys.F11, (_, _) => ToggleFullScreen()),
            Item("關閉目前工作階段", Keys.Control | Keys.W, (_, _) => { if (_tabs.SelectedTab is RdpSessionPage p) CloseSession(p, true); }),
            Item("中斷全部工作階段", Keys.None, (_, _) => CloseAllSessions()),
            new ToolStripSeparator(),
            Item("連線紀錄...", Keys.Control | Keys.H, (_, _) => { using var f = new HistoryForm(_store); f.ShowDialog(this); }),
        });

        var mTools = new ToolStripMenuItem("工具(&T)");
        mTools.DropDownItems.AddRange(new ToolStripItem[]
        {
            Item("設定...", Keys.None, (_, _) => ShowSettings()),
            Item("開啟 Windows 遠端桌面連線 (mstsc)", Keys.None, (_, _) => Process.Start("mstsc.exe")),
        });

        var mHelp = new ToolStripMenuItem("說明(&H)");
        mHelp.DropDownItems.AddRange(new ToolStripItem[]
        {
            Item("快捷鍵一覽", Keys.F1, (_, _) => ShowShortcuts()),
            Item("關於", Keys.None, (_, _) => MessageBox.Show(this,
                $"RDP 遠端連線管理員 v{Application.ProductVersion.Split('+')[0]}\n\n" +
                "管理並快速連線到 Windows 遠端桌面。\n\n" +
                "Copyright © 2026 鴻森智能科技股份有限公司\nAll rights reserved.\n\n" +
                "資料位置:" + DataStore.DataDir,
                "關於", MessageBoxButtons.OK, MessageBoxIcon.Information)),
        });

        _menu.Items.AddRange(new ToolStripItem[] { mFile, mConn, mView, mTools, mHelp });
        _listMenu.Items.AddRange(ConnectionItems(withShortcuts: false));
        _listMenu.Items.Add(new ToolStripSeparator());
        _listMenu.Items.Add(Item("新增連線...", Keys.None, (_, _) => NewConnection()));
        _listMenu.Opening += (_, e) =>
        {
            bool any = _list.SelectedItems.Count > 0;
            for (int i = 0; i < _listMenu.Items.Count - 1; i++) _listMenu.Items[i].Enabled = any;
        };
    }

    private void BuildToolbar()
    {
        ToolStripButton Btn(string text, string tip, EventHandler click) =>
            new(text, null, click) { ToolTipText = tip, DisplayStyle = ToolStripItemDisplayStyle.Text, Padding = new Padding(4, 0, 4, 0) };

        _txtSearch.TextBox.PlaceholderText = "搜尋名稱 / IP / 使用者 / 備註";
        _txtSearch.TextChanged += (_, _) => RefreshList();
        _txtSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { _txtSearch.Clear(); e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                if (_list.Items.Count > 0)
                {
                    _list.SelectedItems.Clear();
                    _list.Items[0].Selected = true;
                    _list.Items[0].Focused = true;
                    if (e.KeyCode == Keys.Enter && _list.Items.Count == 1) ConnectSelected();
                    else _list.Focus();
                }
            }
        };

        _txtQuick.TextBox.PlaceholderText = "IP[:連接埠] 按 Enter";
        _txtQuick.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            QuickConnect(_txtQuick.Text);
        };

        _toolbar.Items.AddRange(new ToolStripItem[]
        {
            Btn("➕ 新增", "新增連線 (Ctrl+N)", (_, _) => NewConnection()),
            Btn("✎ 編輯", "編輯連線 (Ctrl+E)", (_, _) => EditSelected()),
            Btn("✖ 刪除", "刪除連線 (Del)", (_, _) => DeleteSelected()),
            new ToolStripSeparator(),
            Btn("▶ 連線", "連線 (Enter / 雙擊)", (_, _) => ConnectSelected()),
            Btn("⧉ 外部 mstsc", "以 Windows 遠端桌面視窗開啟 (Ctrl+Shift+Enter)", (_, _) => ConnectSelected(LaunchMode.External)),
            Btn("🖵 VNC", "以 VNC 連線 (UltraVNC Viewer) (Ctrl+Shift+V)", (_, _) => ConnectSelected(protocol: ConnectionProtocol.Vnc)),
            Btn("⛶ 全螢幕", "目前的工作階段切換到全螢幕 (F11)", (_, _) => EnterFullScreen()),
            new ToolStripSeparator(),
            Btn("⟳ 檢查狀態", "檢查所有主機是否在線上 (F5)", async (_, _) => await CheckStatusAsync(_store.Connections)),
            Btn("🕘 紀錄", "連線紀錄 (Ctrl+H)", (_, _) => { using var f = new HistoryForm(_store); f.ShowDialog(this); }),
            Btn("⚙ 設定", "設定", (_, _) => ShowSettings()),
            new ToolStripSeparator(),
            new ToolStripLabel("搜尋:"),
            _txtSearch,
            new ToolStripSeparator(),
            new ToolStripLabel("快速連線:"),
            _txtQuick,
            Btn("→", "快速連線", (_, _) => QuickConnect(_txtQuick.Text)),
        });
    }

    private void BuildStatusBar()
    {
        _statusStrip.Items.AddRange(new ToolStripItem[] { _lblMessage, _lblSessions, _lblCount });
        _lblSessions.BorderSides = _lblCount.BorderSides = ToolStripStatusLabelBorderSides.Left;
        _lblMessage.Text = "就緒";
    }

    private void BuildMainArea()
    {
        _split.Panel1.Controls.Add(_tree);
        _split.Panel2.Controls.Add(_sessionHost);
        _sessionHost.Controls.Add(_tabs);
        _sessionHost.Resize += (_, _) => LayoutTabs();
        _listPage.Controls.Add(_list);
        _tabs.TabPages.Add(_listPage);

        _list.Columns.Add("狀態", 80);
        _list.Columns.Add("名稱", 200);
        _list.Columns.Add("類型", 50);
        _list.Columns.Add("主機 / IP", 160);
        _list.Columns.Add("使用者", 160);
        _list.Columns.Add("群組", 110);
        _list.Columns.Add("最後連線", 130);
        _list.Columns.Add("次數", 50, HorizontalAlignment.Right);
        _list.Columns.Add("備註", 240);
        _list.SmallImageList = _statusImages;
        _list.ContextMenuStrip = _listMenu;

        _list.DoubleClick += (_, _) => ConnectSelected();
        _list.ColumnClick += (_, e) =>
        {
            if (_sortColumn == e.Column) _sortAsc = !_sortAsc;
            else { _sortColumn = e.Column; _sortAsc = true; }
            RefreshList();
        };
        _list.KeyDown += (_, e) =>
        {
            switch (e.KeyCode)
            {
                case Keys.Enter when !e.Control: ConnectSelected(); e.Handled = true; break;
                case Keys.Delete: DeleteSelected(); e.Handled = true; break;
                case Keys.F2: EditSelected(); e.Handled = true; break;
                case Keys.A when e.Control:
                    foreach (ListViewItem i in _list.Items) i.Selected = true;
                    e.Handled = true; break;
            }
        };
        _list.SelectedIndexChanged += (_, _) => UpdateCount();

        // 拖曳連線到左側群組
        _list.ItemDrag += (_, _) =>
        {
            var sel = SelectedConnections().ToArray();
            if (sel.Length > 0) _list.DoDragDrop(sel, DragDropEffects.Move);
        };
        _list.DragEnter += (_, e) => e.Effect = HasRdpFiles(e) ? DragDropEffects.Copy : DragDropEffects.None;
        _list.DragDrop += (_, e) => DropFiles(e);

        _tree.AfterSelect += (_, e) =>
        {
            _filterKey = e.Node?.Tag as string ?? "all";
            RefreshList();
        };
        _tree.NodeMouseClick += (_, e) => { if (e.Button == MouseButtons.Right) _tree.SelectedNode = e.Node; };
        _tree.DragOver += (_, e) =>
        {
            if (HasRdpFiles(e)) { e.Effect = DragDropEffects.Copy; return; }
            var node = _tree.GetNodeAt(_tree.PointToClient(new Point(e.X, e.Y)));
            var key = node?.Tag as string ?? "";
            e.Effect = e.Data?.GetDataPresent(typeof(RdpConnection[])) == true &&
                       (key.StartsWith("group:") || key == "nogroup" || key == "fav")
                ? DragDropEffects.Move : DragDropEffects.None;
            if (e.Effect != DragDropEffects.None) _tree.SelectedNode = node;
        };
        _tree.DragDrop += (_, e) =>
        {
            if (HasRdpFiles(e)) { DropFiles(e); return; }
            if (e.Data?.GetData(typeof(RdpConnection[])) is not RdpConnection[] conns) return;
            var node = _tree.GetNodeAt(_tree.PointToClient(new Point(e.X, e.Y)));
            var key = node?.Tag as string ?? "";
            foreach (var c in conns)
            {
                if (key == "fav") c.IsFavorite = true;
                else if (key == "nogroup") c.Group = "";
                else if (key.StartsWith("group:")) c.Group = key["group:".Length..];
            }
            SaveAndRefresh();
            SetMessage($"已移動 {conns.Length} 個連線");
        };

        _treeMenu.Items.AddRange(new ToolStripItem[]
        {
            Item("在此群組新增連線...", Keys.None, (_, _) => NewConnection()),
            Item("新增群組...", Keys.None, (_, _) => NewGroup()),
            Item("重新命名群組...", Keys.None, (_, _) => RenameGroup()),
            Item("刪除群組 (連線移到未分組)", Keys.None, (_, _) => DeleteGroup()),
            new ToolStripSeparator(),
            Item("檢查此群組狀態", Keys.None, async (_, _) => await CheckStatusAsync(FilteredConnections())),
            Item("連線此群組全部主機", Keys.None, (_, _) => ConnectAllInView()),
        });
        _treeMenu.Opening += (_, _) =>
        {
            bool isGroup = (_tree.SelectedNode?.Tag as string)?.StartsWith("group:") == true;
            _treeMenu.Items[2].Enabled = _treeMenu.Items[3].Enabled = isGroup;
        };
        _tree.ContextMenuStrip = _treeMenu;

        // 工作階段分頁:右鍵選單 / 中鍵關閉
        _tabs.MouseUp += (_, e) =>
        {
            for (int i = 0; i < _tabs.TabCount; i++)
            {
                if (!_tabs.GetTabRect(i).Contains(e.Location) || _tabs.TabPages[i] is not RdpSessionPage page) continue;
                if (e.Button == MouseButtons.Middle) CloseSession(page, true);
                else if (e.Button == MouseButtons.Right) ShowTabMenu(page, e.Location);
                return;
            }
        };
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            // 全螢幕時不顯示連線清單頁
            if (_fullScreen && _tabs.SelectedTab == _listPage && SessionPages().FirstOrDefault() is { } first)
            {
                _tabs.SelectedTab = first;
                return;
            }
            if (_tabs.SelectedTab is RdpSessionPage p)
            {
                SetMessage($"{p.Connection.Name}:{p.LastMessage}");
                p.Focus();
            }
            RefreshSidebarSessions();
        };

        BuildFullScreenUi();
    }

    // =====================================================================
    // 全螢幕工作區
    // =====================================================================

    private void BuildFullScreenUi()
    {
        _sidebarHandle.Text = "›";
        _sidebarHandle.FlatStyle = FlatStyle.Flat;
        _sidebarHandle.FlatAppearance.BorderSize = 0;
        _sidebarHandle.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 95, 184);
        _sidebarHandle.BackColor = Color.FromArgb(45, 47, 54);
        _sidebarHandle.ForeColor = Color.White;
        _sidebarHandle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _sidebarHandle.Cursor = Cursors.Hand;
        _sidebarHandle.TabStop = false;
        _sidebarHandle.Visible = false;
        _sidebarHandle.Click += (_, _) => ExpandSidebar();
        new ToolTip().SetToolTip(_sidebarHandle, "展開工作階段面板");

        _sidebar.SessionClicked += p =>
        {
            _tabs.SelectedTab = p;
            AfterSidebarAction();
        };
        _sidebar.ConnectRequested += (c, protocol) =>
        {
            Connect(c, LaunchMode.Default, protocol);
            AfterSidebarAction();
        };
        _sidebar.PinToggled += () =>
        {
            _store.Settings.SidebarPinned = !_store.Settings.SidebarPinned;
            try { _store.SaveSettings(); } catch { /* ignore */ }
            _sidebarExpanded = false;
            ApplySidebarLayout();
            FocusActiveSession();
        };
        _sidebar.CollapseRequested += () =>
        {
            if (_store.Settings.SidebarPinned)
            {
                _store.Settings.SidebarPinned = false;
                try { _store.SaveSettings(); } catch { /* ignore */ }
            }
            CollapseSidebar();
        };
        _sidebar.MinimizeRequested += () => { CollapseSidebar(); WindowState = FormWindowState.Minimized; };
        _sidebar.ExitFullScreenRequested += ExitFullScreen;
        _sidebar.ReconnectRequested += () => { (_tabs.SelectedTab as RdpSessionPage)?.Connect(); AfterSidebarAction(); };
        _sidebar.DisconnectRequested += () => { if (_tabs.SelectedTab is RdpSessionPage p) CloseSession(p, true); };
        _sidebar.NextScreenRequested += MoveToNextScreen;

        Controls.Add(_sidebar);
        Controls.Add(_sidebarHandle);
        _sidebar.BringToFront();
        _sidebarHandle.BringToFront();

        _edgeTimer.Tick += (_, _) => OnEdgeTimer();
    }

    private void ToggleFullScreen()
    {
        if (_fullScreen) ExitFullScreen();
        else EnterFullScreen();
    }

    private void EnterFullScreen()
    {
        if (_fullScreen || _fsTransition) return;
        var page = _tabs.SelectedTab as RdpSessionPage ?? SessionPages().FirstOrDefault();
        if (page == null)
        {
            SetMessage("目前沒有開啟中的工作階段,無法進入全螢幕。");
            return;
        }

        _fsTransition = true;
        try
        {
            _restoreState = WindowState == FormWindowState.Minimized ? FormWindowState.Normal : WindowState;
            _restoreBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var screen = Screen.FromControl(this);

            SuspendLayout();
            _menu.Visible = _toolbar.Visible = _statusStrip.Visible = false;
            _split.Panel1Collapsed = true;
            if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = screen.Bounds;
            _fullScreen = true;
            _sidebarExpanded = false;
            ResumeLayout(true);

            _tabs.SelectedTab = page;
            LayoutTabs();
            ApplySidebarLayout();
            foreach (var p in SessionPages()) p.SetRemoteFullScreen(true);
            RefreshSidebarConnections();
            RefreshSidebarSessions();
            _edgeTimer.Start();
            Activate();
            page.Focus();
        }
        finally { _fsTransition = false; }
    }

    private void ExitFullScreen()
    {
        if (!_fullScreen || _fsTransition) return;
        _fsTransition = true;
        try
        {
            _edgeTimer.Stop();
            _fullScreen = false;
            _sidebarExpanded = false;
            foreach (var p in SessionPages()) p.SetRemoteFullScreen(false);

            SuspendLayout();
            _sidebar.Visible = _sidebarHandle.Visible = false;
            Padding = Padding.Empty;
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = _restoreBounds;
            _menu.Visible = _toolbar.Visible = _statusStrip.Visible = true;
            _split.Panel1Collapsed = false;
            ResumeLayout(true);
            if (_restoreState == FormWindowState.Maximized) WindowState = FormWindowState.Maximized;
            LayoutTabs();
            FocusActiveSession();
        }
        finally { _fsTransition = false; }
    }

    /// <summary>一般模式:分頁填滿容器;全螢幕:把頁籤列與邊框推到容器外,只露出遠端畫面</summary>
    private void LayoutTabs()
    {
        var r = _sessionHost.ClientRectangle;
        if (!_fullScreen)
        {
            _tabs.Bounds = r;
            return;
        }
        var dr = _tabs.DisplayRectangle;
        int extraW = _tabs.Width - dr.Width, extraH = _tabs.Height - dr.Height;
        _tabs.Bounds = new Rectangle(-dr.X, -dr.Y, r.Width + extraW, r.Height + extraH);
    }

    private int SidebarWidth => LogicalToDeviceUnits(290);

    private void ApplySidebarLayout()
    {
        if (!_fullScreen)
        {
            _sidebar.Visible = _sidebarHandle.Visible = false;
            Padding = Padding.Empty;
            return;
        }
        bool pinned = _store.Settings.SidebarPinned;
        int w = SidebarWidth;
        var cs = ClientSize;
        Padding = pinned ? new Padding(w, 0, 0, 0) : Padding.Empty;
        _sidebar.SetBounds(0, 0, w, cs.Height);
        _sidebar.Visible = pinned || _sidebarExpanded;
        _sidebar.SetPinned(pinned);

        int hw = LogicalToDeviceUnits(14), hh = LogicalToDeviceUnits(64);
        _sidebarHandle.SetBounds(0, (cs.Height - hh) / 2, hw, hh);
        _sidebarHandle.Visible = !_sidebar.Visible;

        _sidebar.BringToFront();
        _sidebarHandle.BringToFront();
        LayoutTabs();
    }

    private void ExpandSidebar()
    {
        if (!_fullScreen) return;
        _sidebarExpanded = true;
        _leaveSince = null;
        RefreshSidebarSessions();
        ApplySidebarLayout();
    }

    private void CollapseSidebar()
    {
        if (!_fullScreen) return;
        _sidebarExpanded = false;
        _edgeSince = null;
        ApplySidebarLayout();
        FocusActiveSession();
    }

    /// <summary>從側邊面板切換或開啟連線後:未固定時自動收合,並把鍵盤焦點交給遠端畫面</summary>
    private void AfterSidebarAction()
    {
        if (_fullScreen && !_store.Settings.SidebarPinned) CollapseSidebar();
        else FocusActiveSession();
        RefreshSidebarSessions();
    }

    /// <summary>偵測滑鼠停在螢幕左緣 → 展開;離開面板 → 自動收合</summary>
    private void OnEdgeTimer()
    {
        if (!_fullScreen || _store.Settings.SidebarPinned || WindowState == FormWindowState.Minimized || ActiveForm != this)
        {
            _edgeSince = null;
            return;
        }
        var pos = Cursor.Position;
        var b = Bounds;
        var now = DateTime.Now;
        if (!_sidebarExpanded)
        {
            bool atEdge = _store.Settings.SidebarHoverExpand && pos.X <= b.Left + 1 && pos.Y >= b.Top && pos.Y < b.Bottom;
            if (!atEdge) { _edgeSince = null; return; }
            _edgeSince ??= now;
            if ((now - _edgeSince.Value).TotalMilliseconds >= 250) ExpandSidebar();
        }
        else
        {
            var area = _sidebar.RectangleToScreen(_sidebar.ClientRectangle);
            area.Inflate(LogicalToDeviceUnits(24), 0);
            if (area.Contains(pos) || _sidebar.ContainsFocus || _sidebar.MenuOpen) { _leaveSince = null; return; }
            _leaveSince ??= now;
            if ((now - _leaveSince.Value).TotalMilliseconds >= 600) CollapseSidebar();
        }
    }

    private void MoveToNextScreen()
    {
        if (!_fullScreen) return;
        var screens = Screen.AllScreens;
        if (screens.Length < 2) return;
        var current = Screen.FromControl(this);
        int i = Array.IndexOf(screens, screens.First(s => s.DeviceName == current.DeviceName));
        Bounds = screens[(i + 1) % screens.Length].Bounds;
        ApplySidebarLayout();
        FocusActiveSession();
    }

    private void FocusActiveSession()
    {
        if (_tabs.SelectedTab is RdpSessionPage p) BeginInvoke(new MethodInvoker(p.Focus));
    }

    private void RefreshSidebarSessions()
    {
        if (!_fullScreen) return;
        _sidebar.SetSessions(SessionPages().ToList(), _tabs.SelectedTab as RdpSessionPage);
    }

    private void RefreshSidebarConnections()
    {
        if (_fullScreen) _sidebar.SetConnections(_store.Connections);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        // 從工作列 / Alt+Tab 回來時,把鍵盤交還給遠端畫面
        if (_fullScreen && !_sidebar.ContainsFocus) FocusActiveSession();
    }

    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        if (_fullScreen && !_fsTransition && WindowState != FormWindowState.Minimized) ApplySidebarLayout();
    }

    private void ShowTabMenu(RdpSessionPage page, Point location)
    {
        var m = new ContextMenuStrip();
        m.Items.Add(Item("重新連線", Keys.None, (_, _) => page.Connect()));
        m.Items.Add(Item("全螢幕", Keys.None, (_, _) => { _tabs.SelectedTab = page; EnterFullScreen(); }));
        m.Items.Add(Item("改用外部 mstsc 開啟", Keys.None, (_, _) => { CloseSession(page, false); Connect(page.Connection, LaunchMode.External); }));
        if (!_store.Connections.Any(x => x.Id == page.Connection.Id))
            m.Items.Add(Item("儲存為連線...", Keys.None, (_, _) => SaveQuickConnection(page.Connection)));
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(Item("中斷並關閉分頁", Keys.None, (_, _) => CloseSession(page, false)));
        m.Items.Add(Item("關閉其他分頁", Keys.None, (_, _) =>
        {
            foreach (var p in SessionPages().Where(p => p != page).ToList()) CloseSession(p, false);
        }));
        m.Closed += (_, _) => BeginInvoke(new MethodInvoker(m.Dispose));
        m.Show(_tabs, location);
    }

    private void BuildTray()
    {
        _tray.Icon = new Icon(AppIcon.Get(), SystemInformation.SmallIconSize);
        _tray.Text = "RDP 遠端連線管理員";
        _tray.ContextMenuStrip = _trayMenu;
        _tray.DoubleClick += (_, _) => RestoreFromTray();
        _trayMenu.Opening += (_, _) =>
        {
            _trayMenu.Items.Clear();
            _trayMenu.Items.Add(Item("顯示主視窗", Keys.None, (_, _) => RestoreFromTray()));
            _trayMenu.Items.Add(new ToolStripSeparator());
            var recent = _store.Connections.Where(c => c.LastConnectedAt != null)
                                           .OrderByDescending(c => c.LastConnectedAt).Take(10).ToList();
            var favs = _store.Connections.Where(c => c.IsFavorite).OrderBy(c => c.Name).Take(15).ToList();
            void AddGroup(string title, List<RdpConnection> list)
            {
                if (list.Count == 0) return;
                var parent = new ToolStripMenuItem(title);
                foreach (var c in list)
                {
                    var conn = c;
                    parent.DropDownItems.Add(Item($"{c.Name}  ({c.DisplayAddress})", Keys.None, (_, _) =>
                    {
                        RestoreFromTray();
                        Connect(conn);
                    }));
                }
                _trayMenu.Items.Add(parent);
            }
            AddGroup("★ 我的最愛", favs);
            AddGroup("🕘 最近連線", recent);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(Item("結束", Keys.None, (_, _) => ExitApp()));
        };
        _tray.Visible = true;
    }

    // =====================================================================
    // 啟動 / 關閉
    // =====================================================================

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _store.Load();
        var s = _store.Settings;

        if (s.WindowWidth > 400 && s.WindowHeight > 300)
        {
            var rect = new Rectangle(s.WindowX, s.WindowY, s.WindowWidth, s.WindowHeight);
            if (s.WindowX >= 0 && Screen.AllScreens.Any(sc => sc.WorkingArea.IntersectsWith(rect)))
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = rect;
            }
            else Size = rect.Size;
        }
        if (s.WindowMaximized) WindowState = FormWindowState.Maximized;
        try { _split.SplitterDistance = Math.Clamp(s.SplitterDistance, 150, 500); } catch { /* ignore */ }

        if (s.DailyAutoBackup) { try { _store.AutoDailyBackup(); } catch { /* ignore */ } }

        ApplySettings();
        RefreshTree();
        RefreshList();

        if (_store.LoadWarnings.Count > 0)
            MessageBox.Show(this, string.Join("\n", _store.LoadWarnings), "資料讀取警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (!string.IsNullOrWhiteSpace(_startupTarget))
        {
            var byName = _store.Connections.FirstOrDefault(c => c.Name.Equals(_startupTarget.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byName != null) Connect(byName);
            else QuickConnect(_startupTarget);
        }

        if (_store.Connections.Count == 0)
            SetMessage("尚無連線,按「➕ 新增」或 Ctrl+N 建立第一個連線,也可以把 .rdp 檔拖曳到清單匯入。");
        else if (_store.Settings.CheckStatusOnStartup)
            await CheckStatusAsync(_store.Connections);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_reallyExit && e.CloseReason == CloseReason.UserClosing && _store.Settings.MinimizeToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        int active = SessionPages().Count(p => p.IsActive);
        if (active > 0 && e.CloseReason == CloseReason.UserClosing &&
            MessageBox.Show(this, $"目前仍有 {active} 個遠端工作階段,確定要中斷並結束?", "結束程式",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            e.Cancel = true;
            _reallyExit = false;
            return;
        }

        SaveWindowState();
        foreach (var p in SessionPages().ToList()) p.Disconnect();
        _tray.Visible = false;
        base.OnFormClosing(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized && _store.Settings.MinimizeToTray) HideToTray();
    }

    private void SaveWindowState()
    {
        var s = _store.Settings;
        s.WindowMaximized = _fullScreen ? _restoreState == FormWindowState.Maximized : WindowState == FormWindowState.Maximized;
        var b = _fullScreen ? _restoreBounds : WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        s.WindowX = b.X; s.WindowY = b.Y; s.WindowWidth = b.Width; s.WindowHeight = b.Height;
        s.SplitterDistance = _split.SplitterDistance;
        try { _store.SaveSettings(); } catch { /* ignore */ }
    }

    private void HideToTray()
    {
        Hide();
        if (!_trayTipShown)
        {
            _tray.ShowBalloonTip(2500, "RDP 遠端連線管理員", "程式仍在系統匣執行,雙擊圖示可還原。", ToolTipIcon.Info);
            _trayTipShown = true;
        }
    }

    private void RestoreFromTray()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _reallyExit = true;
        Close();
        _reallyExit = false; // 使用者取消結束時還原
    }

    private void ApplySettings()
    {
        var m = _store.Settings.AutoCheckMinutes;
        _autoCheckTimer.Enabled = false;
        if (m > 0)
        {
            _autoCheckTimer.Interval = m * 60_000;
            _autoCheckTimer.Enabled = true;
        }
    }

    // =====================================================================
    // 清單 / 樹狀
    // =====================================================================

    private void RefreshTree()
    {
        var selected = _filterKey;
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        var conns = _store.Connections;

        TreeNode N(string text, string key) => new(text) { Tag = key };
        _tree.Nodes.Add(N($"全部連線 ({conns.Count})", "all"));
        _tree.Nodes.Add(N($"★ 我的最愛 ({conns.Count(c => c.IsFavorite)})", "fav"));
        _tree.Nodes.Add(N("🕘 最近使用", "recent"));
        var groups = N("📁 群組", "all");
        foreach (var g in _store.GetGroups())
            groups.Nodes.Add(N($"{g} ({conns.Count(c => c.Group.Trim().Equals(g, StringComparison.OrdinalIgnoreCase))})", "group:" + g));
        groups.Nodes.Add(N($"(未分組) ({conns.Count(c => c.Group.Trim().Length == 0)})", "nogroup"));
        _tree.Nodes.Add(groups);
        groups.Expand();

        _tree.SelectedNode = FindNode(_tree.Nodes, selected) ?? _tree.Nodes[0];
        _tree.EndUpdate();
    }

    private static TreeNode? FindNode(TreeNodeCollection nodes, string key)
    {
        foreach (TreeNode n in nodes)
        {
            if (n.Tag as string == key && n.Nodes.Count == 0) return n;
            var child = FindNode(n.Nodes, key);
            if (child != null) return child;
        }
        return null;
    }

    private IEnumerable<RdpConnection> FilteredConnections()
    {
        IEnumerable<RdpConnection> q = _store.Connections;
        if (_filterKey == "fav") q = q.Where(c => c.IsFavorite);
        else if (_filterKey == "recent") q = q.Where(c => c.LastConnectedAt != null).OrderByDescending(c => c.LastConnectedAt).Take(20);
        else if (_filterKey == "nogroup") q = q.Where(c => c.Group.Trim().Length == 0);
        else if (_filterKey.StartsWith("group:"))
        {
            var g = _filterKey["group:".Length..];
            q = q.Where(c => c.Group.Trim().Equals(g, StringComparison.OrdinalIgnoreCase));
        }

        var s = _txtSearch.Text.Trim();
        if (s.Length > 0)
        {
            var terms = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            q = q.Where(c => terms.All(t =>
                c.Name.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                c.Host.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                c.Username.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                c.Group.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                c.Notes.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                c.ProtocolName.Equals(t, StringComparison.OrdinalIgnoreCase)));
        }
        return q;
    }

    private void RefreshList(IEnumerable<Guid>? selectIds = null)
    {
        var keep = (selectIds ?? SelectedConnections().Select(c => c.Id)).ToHashSet();
        var items = FilteredConnections().ToList();

        if (_filterKey != "recent" || _sortColumn != 1)
        {
            Func<RdpConnection, IComparable> key = _sortColumn switch
            {
                0 => c => (long)c.Status * 1_000_000_000L + (c.LatencyMs ?? 999_999_999L),
                2 => c => c.ProtocolName,
                3 => c => c.Host,
                4 => c => c.FullUserName,
                5 => c => c.Group,
                6 => c => c.LastConnectedAt ?? DateTime.MinValue,
                7 => c => c.ConnectCount,
                8 => c => c.Notes,
                _ => c => c.Name,
            };
            items = (_sortAsc ? items.OrderBy(key) : items.OrderByDescending(key)).ToList();
        }

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var c in items)
        {
            var item = new ListViewItem(StatusText(c), c.Status.ToString()) { Tag = c };
            item.SubItems.Add((c.IsFavorite ? "★ " : "") + c.Name);
            item.SubItems.Add(c.ProtocolName);
            item.SubItems.Add(c.DisplayAddress);
            item.SubItems.Add(c.FullUserName);
            item.SubItems.Add(c.Group);
            item.SubItems.Add(c.LastConnectedAt?.ToString("yyyy/MM/dd HH:mm") ?? "");
            item.SubItems.Add(c.ConnectCount.ToString());
            item.SubItems.Add(c.Notes.Replace("\r\n", "  ").Replace('\n', ' '));
            if (keep.Contains(c.Id)) item.Selected = true;
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        if (_list.SelectedItems.Count > 0) _list.SelectedItems[0].EnsureVisible();
        UpdateCount();
    }

    private void SaveAndRefresh(IEnumerable<Guid>? selectIds = null)
    {
        try { _store.SaveConnections(); }
        catch (Exception ex) { ShowError("儲存失敗", ex); }
        RefreshTree();
        RefreshList(selectIds);
        RefreshSidebarConnections();
    }

    private static string StatusText(RdpConnection c) => c.Status switch
    {
        HostStatus.Online => $"{c.LatencyMs} ms",
        HostStatus.Offline => "離線",
        HostStatus.Checking => "檢查中",
        _ => "",
    };

    private void UpdateStatusFor(RdpConnection c)
    {
        if (_fullScreen) _sidebar.InvalidateStatus();
        foreach (ListViewItem item in _list.Items)
        {
            if (item.Tag != c) continue;
            item.ImageKey = c.Status.ToString();
            item.Text = StatusText(c);
        }
    }

    private void UpdateCount()
    {
        _lblCount.Text = $"顯示 {_list.Items.Count} / 共 {_store.Connections.Count} 個連線" +
                         (_list.SelectedItems.Count > 1 ? $"(已選 {_list.SelectedItems.Count})" : "");
        int active = SessionPages().Count(p => p.IsActive);
        _lblSessions.Text = $"工作階段:{active}";
    }

    private List<RdpConnection> SelectedConnections() =>
        _list.SelectedItems.Cast<ListViewItem>().Select(i => (RdpConnection)i.Tag!).ToList();

    private IEnumerable<RdpSessionPage> SessionPages() => _tabs.TabPages.OfType<RdpSessionPage>();

    private void SetMessage(string text) => _lblMessage.Text = text;

    private void ShowError(string title, Exception ex) =>
        MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

    // =====================================================================
    // 連線 CRUD
    // =====================================================================

    private void NewConnection()
    {
        var c = new RdpConnection();
        if (_filterKey.StartsWith("group:")) c.Group = _filterKey["group:".Length..];
        if (_filterKey == "fav") c.IsFavorite = true;
        using var f = new ConnectionEditForm(c, _store.GetGroups(), "新增連線", _store.Settings.RememberPasswordByDefault);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        _store.Connections.Add(c);
        SaveAndRefresh(new[] { c.Id });
        SetMessage($"已新增「{c.Name}」");
        _ = CheckStatusAsync(new[] { c });
    }

    private void EditSelected()
    {
        var c = SelectedConnections().FirstOrDefault();
        if (c == null) return;
        var copy = c.Clone();
        using var f = new ConnectionEditForm(copy, _store.GetGroups(), $"編輯連線 - {c.Name}", _store.Settings.RememberPasswordByDefault);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        var idx = _store.Connections.IndexOf(c);
        if (idx >= 0) _store.Connections[idx] = copy;
        SaveAndRefresh(new[] { copy.Id });
        SetMessage($"已更新「{copy.Name}」");
        if (copy.Host != c.Host || copy.ActivePort != c.ActivePort) _ = CheckStatusAsync(new[] { copy });
    }

    private void DuplicateSelected()
    {
        var src = SelectedConnections().FirstOrDefault();
        if (src == null) return;
        var c = src.Clone();
        c.Id = Guid.NewGuid();
        c.Name = src.Name + " - 複本";
        c.CreatedAt = DateTime.Now;
        c.LastConnectedAt = null;
        c.ConnectCount = 0;
        _store.Connections.Add(c);
        SaveAndRefresh(new[] { c.Id });
        EditSelected();
    }

    private void DeleteSelected()
    {
        var sel = SelectedConnections();
        if (sel.Count == 0) return;
        if (_store.Settings.ConfirmDelete)
        {
            var msg = sel.Count == 1 ? $"確定要刪除「{sel[0].Name}」({sel[0].DisplayAddress})?" : $"確定要刪除選取的 {sel.Count} 個連線?";
            if (MessageBox.Show(this, msg, "刪除連線", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        }
        foreach (var c in sel) _store.Connections.Remove(c);
        SaveAndRefresh();
        SetMessage($"已刪除 {sel.Count} 個連線");
    }

    private void ToggleFavorite()
    {
        var sel = SelectedConnections();
        if (sel.Count == 0) return;
        bool value = !sel.All(c => c.IsFavorite);
        foreach (var c in sel) c.IsFavorite = value;
        SaveAndRefresh();
    }

    private void MoveSelectedToGroup()
    {
        var sel = SelectedConnections();
        if (sel.Count == 0) return;
        var g = FormHelpers.InputBox(this, "移動到群組", "群組名稱 (留白 = 未分組):", sel[0].Group);
        if (g == null) return;
        foreach (var c in sel) c.Group = g;
        SaveAndRefresh();
    }

    private void CopyAddress()
    {
        var sel = SelectedConnections();
        if (sel.Count == 0) return;
        Clipboard.SetText(string.Join(Environment.NewLine, sel.Select(c => c.DisplayAddress)));
        SetMessage("已複製到剪貼簿");
    }

    private void SaveQuickConnection(RdpConnection temp)
    {
        var c = temp.Clone();
        using var f = new ConnectionEditForm(c, _store.GetGroups(), "儲存連線", _store.Settings.RememberPasswordByDefault);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        _store.Connections.Add(c);
        SaveAndRefresh(new[] { c.Id });
    }

    // ---------- 群組 ----------
    private void NewGroup()
    {
        var name = FormHelpers.InputBox(this, "新增群組", "群組名稱:");
        if (string.IsNullOrWhiteSpace(name)) return;
        var sel = SelectedConnections();
        if (sel.Count > 0 && MessageBox.Show(this, $"要把目前選取的 {sel.Count} 個連線移到「{name}」嗎?", "新增群組",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            foreach (var c in sel) c.Group = name;
            _filterKey = "group:" + name;
            SaveAndRefresh();
        }
        else
        {
            // 群組由連線的 Group 欄位產生,先建立一筆連線放進新群組
            _filterKey = "group:" + name;
            NewConnection();
        }
    }

    private void RenameGroup()
    {
        if (_tree.SelectedNode?.Tag is not string key || !key.StartsWith("group:")) return;
        var old = key["group:".Length..];
        var name = FormHelpers.InputBox(this, "重新命名群組", "新名稱:", old);
        if (string.IsNullOrWhiteSpace(name) || name == old) return;
        foreach (var c in _store.Connections.Where(c => c.Group.Trim().Equals(old, StringComparison.OrdinalIgnoreCase)))
            c.Group = name;
        _filterKey = "group:" + name;
        SaveAndRefresh();
    }

    private void DeleteGroup()
    {
        if (_tree.SelectedNode?.Tag is not string key || !key.StartsWith("group:")) return;
        var g = key["group:".Length..];
        if (MessageBox.Show(this, $"刪除群組「{g}」?\n群組內的連線不會被刪除,會移到「未分組」。", "刪除群組",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        foreach (var c in _store.Connections.Where(c => c.Group.Trim().Equals(g, StringComparison.OrdinalIgnoreCase)))
            c.Group = "";
        _filterKey = "nogroup";
        SaveAndRefresh();
    }

    // =====================================================================
    // 連線
    // =====================================================================

    /// <param name="protocol">null = 使用連線的預設連線類型</param>
    private void ConnectSelected(LaunchMode force = LaunchMode.Default, ConnectionProtocol? protocol = null)
    {
        var sel = SelectedConnections();
        if (sel.Count > 5 && MessageBox.Show(this, $"確定要同時開啟 {sel.Count} 個連線?", "連線",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        foreach (var c in sel) Connect(c, force, protocol);
    }

    private void ConnectAllInView()
    {
        var list = FilteredConnections().ToList();
        if (list.Count == 0) return;
        if (MessageBox.Show(this, $"確定要開啟 {list.Count} 個連線?", "連線", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        foreach (var c in list) Connect(c);
    }

    private void QuickConnect(string text)
    {
        // 「vnc://host[:port]」或「vnc:host」= VNC,其餘為 RDP
        bool vnc = AddressParser.StripVncPrefix(text, out var address);
        var protocol = vnc ? ConnectionProtocol.Vnc : ConnectionProtocol.Rdp;
        if (!AddressParser.TryParse(address, out var host, out var port, vnc ? AddressParser.DefaultVncPort : AddressParser.DefaultPort))
        {
            SetMessage("請輸入有效的 IP 或主機名稱,例如 192.168.1.10、server:3390,VNC 請輸入 vnc://192.168.1.20");
            return;
        }
        var saved = _store.Connections.FirstOrDefault(c =>
            c.Host.Equals(host, StringComparison.OrdinalIgnoreCase) && (vnc ? c.VncPortValue == port : c.Port == port));
        var conn = saved ?? (vnc
            ? new RdpConnection { Protocol = protocol, Host = host, VncPort = port }
            : new RdpConnection { Protocol = protocol, Host = host, Port = port });
        if (saved == null) conn.Name = (vnc ? "VNC " : "") + conn.DisplayAddress;
        Connect(conn, LaunchMode.Default, protocol);
        _txtQuick.Clear();
        if (saved == null && !vnc) SetMessage("快速連線不會自動儲存,可在分頁上按右鍵「儲存為連線」。");
    }

    /// <param name="force">指定 RDP 的內嵌 / 外部方式 (指定時一律用 RDP)</param>
    /// <param name="protocol">指定 RDP 或 VNC;null = 使用連線的預設連線類型</param>
    private void Connect(RdpConnection c, LaunchMode force = LaunchMode.Default, ConnectionProtocol? protocol = null)
    {
        if (string.IsNullOrWhiteSpace(c.Host)) return;
        bool vnc = (protocol ?? (force != LaunchMode.Default ? ConnectionProtocol.Rdp : c.Protocol)) == ConnectionProtocol.Vnc;

        // VNC:以外部 UltraVNC Viewer 開啟,先確認已安裝 (避免輸入密碼後才發現無法開啟)
        string? vncViewer = null;
        if (vnc)
        {
            vncViewer = VncLauncher.Resolve(_store.Settings.UltraVncPath);
            if (vncViewer == null)
            {
                ShowVncViewerMissing();
                return;
            }
        }

        var mode = force != LaunchMode.Default ? force
                 : c.LaunchMode != LaunchMode.Default ? c.LaunchMode
                 : _store.Settings.DefaultLaunchMode;
        if (mode == LaunchMode.Default) mode = LaunchMode.Embedded;

        if (!vnc && mode == LaunchMode.Embedded && _store.Settings.ReuseOpenSession)
        {
            var existing = SessionPages().FirstOrDefault(p => p.Connection.Id == c.Id && p.IsActive);
            if (existing != null)
            {
                _tabs.SelectedTab = existing;
                if (WantsFullScreen(c)) EnterFullScreen();
                FocusActiveSession();
                return;
            }
        }

        // 密碼:有儲存就用;否則 (預設) 連線前詢問,只用於這次連線。RDP 與 VNC 的密碼分開
        var password = PasswordProtector.Unprotect(vnc ? c.VncEncryptedPassword : c.EncryptedPassword);
        bool passwordSaved = password.Length > 0;
        var target = c; // 這次工作階段實際使用的連線設定
        if (!passwordSaved && _store.Settings.AskPasswordBeforeConnect)
        {
            bool persisted = _store.Connections.Contains(c);
            using var dlg = new PasswordPromptForm(c, allowRemember: persisted, vnc: vnc);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            password = dlg.Password;
            if (vnc)
            {
                if (dlg.UserName != c.VncUsername)
                {
                    if (dlg.Remember || !persisted) c.VncUsername = dlg.UserName;
                    else { target = c.Clone(); target.VncUsername = dlg.UserName; }
                }
                if (dlg.Remember && password.Length > 0)
                {
                    c.VncEncryptedPassword = PasswordProtector.Protect(password);
                    passwordSaved = true;
                }
            }
            else
            {
                var (domain, user) = PasswordPromptForm.SplitUser(dlg.UserName);
                if (user != c.Username || domain != c.Domain)
                {
                    if (dlg.Remember || !persisted) { c.Username = user; c.Domain = domain; }
                    else { target = c.Clone(); target.Username = user; target.Domain = domain; }
                }
                if (dlg.Remember && password.Length > 0)
                {
                    c.EncryptedPassword = PasswordProtector.Protect(password);
                    passwordSaved = true;
                }
            }
        }

        if (_store.Connections.Contains(c))
        {
            c.LastConnectedAt = DateTime.Now;
            c.ConnectCount++;
            try { _store.SaveConnections(); } catch { /* ignore */ }
        }

        if (vnc)
        {
            try
            {
                VncLauncher.Launch(vncViewer!, target, password, WantsFullScreen(c));
                AddHistory(c, "VNC", "啟動 UltraVNC", "");
                SetMessage($"已以 UltraVNC Viewer 開啟「{c.Name}」");
            }
            catch (Exception ex)
            {
                AddHistory(c, "VNC", "連線失敗", ex.Message);
                ShowError("無法啟動 UltraVNC Viewer", ex);
            }
        }
        else if (mode == LaunchMode.External)
        {
            try
            {
                ExternalLauncher.Launch(target, password, WantsFullScreen(c), keepCredential: passwordSaved);
                AddHistory(c, "外部", "啟動 mstsc", "");
                SetMessage($"已以 mstsc 開啟「{c.Name}」");
            }
            catch (Exception ex)
            {
                AddHistory(c, "外部", "連線失敗", ex.Message);
                ShowError("無法啟動 mstsc", ex);
            }
        }
        else
        {
            var page = new RdpSessionPage(target, password) { KeyboardHookMode = _store.Settings.KeyboardHookMode };
            page.StateChanged += p =>
            {
                if (_tabs.SelectedTab == p) SetMessage($"{p.Connection.Name}:{p.LastMessage.Replace('\n', ' ')}");
                UpdateCount();
                RefreshSidebarSessions();
            };
            page.SessionEvent += (p, evt, detail) => AddHistory(p.Connection, "內嵌", evt, detail);
            page.CloseRequested += p => CloseSession(p, false);
            // 遠端按 Ctrl+Alt+Break 等:由本程式切換全螢幕 (延後執行,不在 ActiveX 事件中改視窗樣式)
            page.FullScreenRequested += (p, on) => BeginInvoke(new MethodInvoker(() =>
            {
                if (on) { _tabs.SelectedTab = p; EnterFullScreen(); }
                else ExitFullScreen();
            }));
            page.MinimizeRequested += _ => BeginInvoke(new MethodInvoker(() => WindowState = FormWindowState.Minimized));
            _tabs.TabPages.Add(page);
            _tabs.SelectedTab = page;
            // 先切換全螢幕,讓遠端解析度直接符合整個螢幕
            if (WantsFullScreen(c)) EnterFullScreen();
            if (_fullScreen) page.SetRemoteFullScreen(true);
            try
            {
                page.Connect();
                AddHistory(c, "內嵌", "開始連線", "");
            }
            catch (Exception ex)
            {
                _tabs.TabPages.Remove(page);
                page.Dispose();
                if (_fullScreen && !SessionPages().Any()) ExitFullScreen();
                AddHistory(c, "內嵌", "連線失敗", ex.Message);
                if (MessageBox.Show(this, $"內嵌遠端桌面元件無法啟動:\n{ex.Message}\n\n要改用外部 mstsc 連線嗎?", "連線失敗",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    try { ExternalLauncher.Launch(target, password, WantsFullScreen(c), keepCredential: passwordSaved); }
                    catch (Exception ex2) { ShowError("無法啟動 mstsc", ex2); }
                }
            }
        }

        if (_tabs.SelectedTab == _listPage || mode == LaunchMode.External || vnc) RefreshList();
        UpdateCount();
        RefreshSidebarSessions();
    }

    private void ShowVncViewerMissing()
    {
        var r = MessageBox.Show(this,
            "找不到 UltraVNC Viewer (vncviewer.exe),VNC 連線需要先安裝它。\n\n" +
            "按「是」:開啟設定,指定 vncviewer.exe 的位置\n" +
            "按「否」:開啟 UltraVNC 官方下載頁面",
            "VNC 連線", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
        if (r == DialogResult.Yes) ShowSettings();
        else if (r == DialogResult.No)
        {
            try { Process.Start(new ProcessStartInfo(VncLauncher.DownloadUrl) { UseShellExecute = true }); }
            catch (Exception ex) { ShowError("無法開啟網頁", ex); }
        }
    }

    private bool WantsFullScreen(RdpConnection c) =>
        _store.Settings.AlwaysFullScreen || c.ScreenMode == ScreenMode.FullScreen;

    private void CloseSession(RdpSessionPage page, bool confirm)
    {
        if (confirm && page.IsActive &&
            MessageBox.Show(this, $"確定要中斷「{page.Connection.Name}」的連線?", "中斷連線",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        int idx = _tabs.TabPages.IndexOf(page);
        if (page.IsActive) AddHistory(page.Connection, "內嵌", "中斷連線", "使用者關閉分頁");
        page.Disconnect();
        _tabs.TabPages.Remove(page);
        page.Dispose();
        var remaining = SessionPages().ToList();
        if (_fullScreen && remaining.Count == 0)
        {
            ExitFullScreen();
            _tabs.SelectedTab = _listPage;
        }
        else if (_fullScreen)
        {
            // 全螢幕中關閉時,切到相鄰的工作階段
            _tabs.SelectedTab = remaining[Math.Clamp(idx - 2, 0, remaining.Count - 1)];
            FocusActiveSession();
        }
        else if (_tabs.TabCount > 0) _tabs.SelectedIndex = Math.Clamp(idx - 1, 0, _tabs.TabCount - 1);
        UpdateCount();
        RefreshSidebarSessions();
    }

    private void CloseAllSessions()
    {
        var pages = SessionPages().ToList();
        if (pages.Count == 0) return;
        if (MessageBox.Show(this, $"確定要中斷全部 {pages.Count} 個工作階段?", "中斷全部",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        foreach (var p in pages) CloseSession(p, false);
    }

    private void AddHistory(RdpConnection c, string mode, string evt, string detail) =>
        _store.AddHistory(new HistoryEntry
        {
            ConnectionId = c.Id, Name = c.Name, Host = c.DisplayAddress, Mode = mode, Event = evt, Detail = detail,
        });

    // =====================================================================
    // 狀態檢查 / WOL
    // =====================================================================

    private async Task CheckStatusAsync(IEnumerable<RdpConnection> targets)
    {
        if (_checking) return;
        var list = targets.ToList();
        if (list.Count == 0) return;
        _checking = true;
        try
        {
            foreach (var c in list) { c.Status = HostStatus.Checking; UpdateStatusFor(c); }
            SetMessage($"正在檢查 {list.Count} 台主機…");
            int timeout = _store.Settings.StatusTimeoutMs;
            using var sem = new SemaphoreSlim(24);
            await Task.WhenAll(list.Select(async c =>
            {
                await sem.WaitAsync();
                try
                {
                    var (ok, ms) = await HostChecker.CheckAsync(c.Host, c.ActivePort, timeout);
                    c.Status = ok ? HostStatus.Online : HostStatus.Offline;
                    c.LatencyMs = ok ? ms : null;
                }
                finally { sem.Release(); }
                if (!IsDisposed) UpdateStatusFor(c);
            }));
            if (IsDisposed) return;
            SetMessage($"狀態檢查完成({DateTime.Now:HH:mm:ss}):{list.Count(c => c.Status == HostStatus.Online)} 台線上,{list.Count(c => c.Status == HostStatus.Offline)} 台離線");
            if (_sortColumn == 0) RefreshList();
        }
        finally { _checking = false; }
    }

    private void WakeSelected()
    {
        var sel = SelectedConnections();
        var sent = new List<string>();
        var missing = new List<string>();
        foreach (var c in sel)
        {
            if (WakeOnLan.TryParseMac(c.MacAddress, out var mac))
            {
                try { WakeOnLan.Send(mac); sent.Add(c.Name); AddHistory(c, "WOL", "網路喚醒", c.MacAddress); }
                catch (Exception ex) { missing.Add($"{c.Name}:{ex.Message}"); }
            }
            else missing.Add($"{c.Name}:未設定 MAC 位址");
        }
        var msg = "";
        if (sent.Count > 0) msg += $"已送出喚醒封包:{string.Join("、", sent)}\n";
        if (missing.Count > 0) msg += "\n未送出:\n" + string.Join("\n", missing);
        if (msg.Length > 0) MessageBox.Show(this, msg.Trim(), "網路喚醒", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // =====================================================================
    // 匯入 / 匯出 / 備份
    // =====================================================================

    private static bool HasRdpFiles(DragEventArgs e) =>
        e.Data?.GetData(DataFormats.FileDrop) is string[] files &&
        files.Any(f => Path.GetExtension(f).ToLowerInvariant() is ".rdp" or ".json" or ".csv");

    private void DropFiles(DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files) ImportFiles(files);
    }

    private void ImportConnections()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "匯入連線",
            Filter = "支援的檔案 (*.json;*.csv;*.rdp)|*.json;*.csv;*.rdp|JSON (*.json)|*.json|CSV (*.csv)|*.csv|遠端桌面檔 (*.rdp)|*.rdp",
            Multiselect = true,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) ImportFiles(dlg.FileNames);
    }

    private void ImportFiles(IEnumerable<string> files)
    {
        var imported = new List<RdpConnection>();
        var errors = new List<string>();
        foreach (var f in files)
        {
            try
            {
                switch (Path.GetExtension(f).ToLowerInvariant())
                {
                    case ".json": imported.AddRange(ImportExport.ImportJson(f)); break;
                    case ".csv": imported.AddRange(ImportExport.ImportCsv(f)); break;
                    case ".rdp": imported.Add(ImportExport.ImportRdpFile(f)); break;
                }
            }
            catch (Exception ex) { errors.Add($"{Path.GetFileName(f)}:{ex.Message}"); }
        }

        int added = 0, skipped = 0;
        var newIds = new List<Guid>();
        foreach (var c in imported)
        {
            if (string.IsNullOrWhiteSpace(c.Host)) { skipped++; continue; }
            bool dup = _store.Connections.Any(x =>
                x.Host.Equals(c.Host, StringComparison.OrdinalIgnoreCase) && x.Port == c.Port &&
                x.Username.Equals(c.Username, StringComparison.OrdinalIgnoreCase));
            if (dup) { skipped++; continue; }
            if (c.Id == Guid.Empty || _store.Connections.Any(x => x.Id == c.Id)) c.Id = Guid.NewGuid();
            c.Status = HostStatus.Unknown;
            _store.Connections.Add(c);
            newIds.Add(c.Id);
            added++;
        }
        if (added > 0) SaveAndRefresh(newIds);

        var msg = $"新增 {added} 筆,略過 {skipped} 筆 (重複或缺少主機)。";
        if (errors.Count > 0) msg += "\n\n錯誤:\n" + string.Join("\n", errors);
        MessageBox.Show(this, msg, "匯入結果", MessageBoxButtons.OK, errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        if (added > 0) _ = CheckStatusAsync(_store.Connections.Where(c => newIds.Contains(c.Id)));
    }

    private void ExportConnections(bool json)
    {
        var sel = SelectedConnections();
        IEnumerable<RdpConnection> list = _store.Connections;
        if (sel.Count > 1 && MessageBox.Show(this, $"只匯出選取的 {sel.Count} 個連線嗎?\n(選「否」匯出全部)", "匯出",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            list = sel;

        using var dlg = new SaveFileDialog
        {
            Title = "匯出連線",
            Filter = json ? "JSON (*.json)|*.json" : "CSV (*.csv)|*.csv",
            FileName = $"RDP連線-{DateTime.Now:yyyyMMdd}" + (json ? ".json" : ".csv"),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (json) ImportExport.ExportJson(dlg.FileName, list);
            else ImportExport.ExportCsv(dlg.FileName, list);
            MessageBox.Show(this, $"已匯出 {list.Count()} 個連線。\n(基於安全性,匯出檔不包含密碼)", "匯出完成",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { ShowError("匯出失敗", ex); }
    }

    private void ExportSelectedRdp()
    {
        var c = SelectedConnections().FirstOrDefault();
        if (c == null) return;
        using var dlg = new SaveFileDialog { Filter = "遠端桌面檔 (*.rdp)|*.rdp", FileName = c.Name + ".rdp" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { ExternalLauncher.SaveRdpFile(c, dlg.FileName); SetMessage("已匯出 " + dlg.FileName); }
        catch (Exception ex) { ShowError("匯出失敗", ex); }
    }

    private void BackupNow()
    {
        try
        {
            var path = _store.CreateBackup();
            SetMessage("已備份到 " + path);
            MessageBox.Show(this, "已備份到:\n" + path, "備份完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { ShowError("備份失敗", ex); }
    }

    private void RestoreBackup()
    {
        Directory.CreateDirectory(DataStore.BackupDir);
        using var dlg = new OpenFileDialog
        {
            Title = "選擇要還原的備份",
            InitialDirectory = DataStore.BackupDir,
            Filter = "備份檔 (*.json)|*.json",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var list = DataStore.ReadBackup(dlg.FileName);
            if (MessageBox.Show(this, $"備份內有 {list.Count} 個連線。\n還原會取代目前的 {_store.Connections.Count} 個連線 (目前資料會先自動備份),確定?",
                    "從備份還原", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _store.CreateBackup();
            _store.Connections = list;
            _filterKey = "all";
            SaveAndRefresh();
            SetMessage("已從備份還原");
        }
        catch (Exception ex) { ShowError("還原失敗", ex); }
    }

    // =====================================================================
    // 其他
    // =====================================================================

    private void ShowSettings()
    {
        using var f = new SettingsForm(_store.Settings);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        try { _store.SaveSettings(); } catch (Exception ex) { ShowError("儲存設定失敗", ex); }
        ApplySettings();
    }

    private void ShowShortcuts() => MessageBox.Show(this,
        "Ctrl+N\t新增連線\n" +
        "Enter / 雙擊\t連線\n" +
        "Ctrl+Shift+Enter\t以 RDP 連線 (外部 mstsc)\n" +
        "Ctrl+Shift+V\t以 VNC 連線 (UltraVNC)\n" +
        "Ctrl+E / F2\t編輯\n" +
        "Ctrl+D\t建立複本\n" +
        "Del\t刪除\n" +
        "Ctrl+B\t加入 / 移除我的最愛\n" +
        "Ctrl+F\t搜尋\n" +
        "Ctrl+Q\t快速連線\n" +
        "F5\t重新檢查狀態\n" +
        "Ctrl+H\t連線紀錄\n" +
        "Ctrl+I\t匯入\n" +
        "Ctrl+L\t回到連線清單\n" +
        "F11\t全螢幕 / 視窗模式切換\n" +
        "Ctrl+Alt+Break\t在遠端畫面中切換全螢幕 / 視窗\n" +
        "Ctrl+W / 中鍵\t關閉工作階段分頁\n\n" +
        "全螢幕時:滑鼠移到螢幕左緣 (或點左側小把手) 展開工作階段面板,可切換電腦、縮小或回到視窗模式。\n" +
        "提示:可將清單中的連線拖曳到左側群組,或把 .rdp/.csv/.json 檔拖曳到清單匯入。",
        "快捷鍵一覽", MessageBoxButtons.OK, MessageBoxIcon.Information);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _autoCheckTimer.Dispose();
            _edgeTimer.Dispose();
            _statusImages.Dispose();
        }
        base.Dispose(disposing);
    }
}
