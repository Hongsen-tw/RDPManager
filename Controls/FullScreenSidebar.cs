// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using System.Drawing.Drawing2D;
using RDPManager.Models;

namespace RDPManager.Controls;

/// <summary>全螢幕模式的側邊面板:切換工作階段、開啟其他連線、縮小 / 離開全螢幕</summary>
public class FullScreenSidebar : Panel
{
    // 深色配色
    private static readonly Color Bg = Color.FromArgb(28, 29, 33);
    private static readonly Color BgItem = Color.FromArgb(40, 42, 48);
    private static readonly Color BgActive = Color.FromArgb(0, 95, 184);
    private static readonly Color BgHover = Color.FromArgb(52, 55, 62);
    private static readonly Color TextMain = Color.White;
    private static readonly Color TextSub = Color.FromArgb(160, 166, 176);
    private static readonly Color Green = Color.FromArgb(46, 190, 90);
    private static readonly Color Orange = Color.FromArgb(240, 160, 40);
    private static readonly Color Red = Color.FromArgb(230, 70, 80);
    private static readonly Color Gray = Color.FromArgb(120, 124, 132);

    public event Action<RdpSessionPage>? SessionClicked;
    /// <summary>(連線, 指定類型;null = 預設連線類型)</summary>
    public event Action<RdpConnection, ConnectionProtocol?>? ConnectRequested;
    /// <summary>右鍵選單開啟中 (避免面板自動收合)</summary>
    public bool MenuOpen { get; private set; }
    public event Action? PinToggled;
    public event Action? CollapseRequested;
    public event Action? MinimizeRequested;
    public event Action? ExitFullScreenRequested;
    public event Action? ReconnectRequested;
    public event Action? DisconnectRequested;
    public event Action? NextScreenRequested;

    private readonly ListBox _sessions = new();
    private readonly ListBox _conns = new();
    private readonly TextBox _search = new();
    private readonly Label _lblSessions = new();
    private readonly Button _btnPin;
    private readonly Button _btnNextScreen;
    private readonly ToolTip _tip = new();

    private List<RdpConnection> _allConns = new();
    private RdpSessionPage? _active;
    private int _hoverSession = -1, _hoverConn = -1;

    public FullScreenSidebar()
    {
        BackColor = Bg;
        ForeColor = TextMain;
        Font = new Font("Microsoft JhengHei UI", 9.5F);
        DoubleBuffered = true;

        int S(int v) => LogicalToDeviceUnits(v);

        // ---- 標題列 ----
        var header = new Panel { Dock = DockStyle.Top, Height = S(46), BackColor = Color.FromArgb(22, 23, 26), Padding = new Padding(S(10), 0, S(4), 0) };
        var title = new Label
        {
            Text = "遠端工作階段", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft JhengHei UI", 11F, FontStyle.Bold), ForeColor = TextMain,
        };
        var btnCollapse = IconButton("«", "收合面板");
        _btnPin = IconButton("📌", "固定面板 (不自動收合,遠端畫面縮小讓出空間)");
        btnCollapse.Click += (_, _) => CollapseRequested?.Invoke();
        _btnPin.Click += (_, _) => PinToggled?.Invoke();
        header.Controls.Add(title);
        header.Controls.Add(_btnPin);
        header.Controls.Add(btnCollapse);

        // ---- 已開啟的工作階段 ----
        _lblSessions.Dock = DockStyle.Top;
        _lblSessions.Height = S(30);
        _lblSessions.Padding = new Padding(S(10), S(8), 0, 0);
        _lblSessions.ForeColor = TextSub;
        _lblSessions.Text = "已開啟";

        SetupList(_sessions, S(46));
        _sessions.Dock = DockStyle.Top;
        _sessions.DrawItem += DrawSession;
        _sessions.MouseClick += (_, e) =>
        {
            int i = _sessions.IndexFromPoint(e.Location);
            if (i >= 0 && _sessions.Items[i] is RdpSessionPage p) SessionClicked?.Invoke(p);
        };
        _sessions.MouseMove += (_, e) => SetHover(_sessions, ref _hoverSession, _sessions.IndexFromPoint(e.Location));
        _sessions.MouseLeave += (_, _) => SetHover(_sessions, ref _hoverSession, -1);

        // ---- 所有連線 ----
        var lblConns = new Label
        {
            Text = "所有連線 (雙擊開啟,右鍵可選 VNC)", Dock = DockStyle.Top, Height = S(30),
            Padding = new Padding(S(10), S(10), 0, 0), ForeColor = TextSub,
        };
        var searchBox = new Panel { Dock = DockStyle.Top, Height = S(36), Padding = new Padding(S(10), S(4), S(10), S(4)) };
        _search.Dock = DockStyle.Fill;
        _search.BorderStyle = BorderStyle.FixedSingle;
        _search.BackColor = BgItem;
        _search.ForeColor = TextMain;
        _search.PlaceholderText = "搜尋名稱 / IP…";
        _search.TextChanged += (_, _) => FillConnections();
        _search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && _conns.Items.Count > 0)
            {
                e.SuppressKeyPress = true;
                ConnectRequested?.Invoke((RdpConnection)_conns.Items[_conns.SelectedIndex >= 0 ? _conns.SelectedIndex : 0], null);
            }
            else if (e.KeyCode == Keys.Down && _conns.Items.Count > 0)
            {
                e.SuppressKeyPress = true;
                _conns.SelectedIndex = 0;
                _conns.Focus();
            }
            else if (e.KeyCode == Keys.Escape) { _search.Clear(); e.SuppressKeyPress = true; }
        };
        searchBox.Controls.Add(_search);

        SetupList(_conns, S(42));
        _conns.Dock = DockStyle.Fill;
        _conns.DrawItem += DrawConnection;
        _conns.DoubleClick += (_, _) => { if (_conns.SelectedItem is RdpConnection c) ConnectRequested?.Invoke(c, null); };
        _conns.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && _conns.SelectedItem is RdpConnection c) { e.SuppressKeyPress = true; ConnectRequested?.Invoke(c, null); }
        };

        // 右鍵:選擇以 RDP 或 VNC 連線
        var menu = new ContextMenuStrip();
        menu.Items.Add("連線 (預設類型)", null, (_, _) => { if (_conns.SelectedItem is RdpConnection c) ConnectRequested?.Invoke(c, null); });
        menu.Items.Add("以 RDP 連線", null, (_, _) => { if (_conns.SelectedItem is RdpConnection c) ConnectRequested?.Invoke(c, ConnectionProtocol.Rdp); });
        menu.Items.Add("以 VNC 連線 (UltraVNC)", null, (_, _) => { if (_conns.SelectedItem is RdpConnection c) ConnectRequested?.Invoke(c, ConnectionProtocol.Vnc); });
        menu.Opening += (_, e) =>
        {
            int i = _conns.IndexFromPoint(_conns.PointToClient(Cursor.Position));
            if (i < 0) { e.Cancel = true; return; }
            _conns.SelectedIndex = i;
            MenuOpen = true;
        };
        menu.Closed += (_, _) => MenuOpen = false;
        _conns.ContextMenuStrip = menu;
        _conns.MouseMove += (_, e) => SetHover(_conns, ref _hoverConn, _conns.IndexFromPoint(e.Location));
        _conns.MouseLeave += (_, _) => SetHover(_conns, ref _hoverConn, -1);

        // ---- 底部動作 ----
        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom, ColumnCount = 2, AutoSize = true,
            Padding = new Padding(S(8), S(6), S(8), S(4)), BackColor = Color.FromArgb(22, 23, 26),
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        Button A(string text, string tip, Action click)
        {
            var b = ActionButton(text, tip);
            b.Click += (_, _) => click();
            actions.Controls.Add(b);
            return b;
        }
        A("⟳ 重新連線", "重新連線目前的工作階段", () => ReconnectRequested?.Invoke());
        A("✖ 中斷連線", "中斷並關閉目前的工作階段", () => DisconnectRequested?.Invoke());
        A("▁ 縮小", "縮小到工作列,點工作列圖示即可回到全螢幕", () => MinimizeRequested?.Invoke());
        A("🗗 視窗模式", "離開全螢幕,回到主視窗 (遠端按 Ctrl+Alt+Break 也可切換)", () => ExitFullScreenRequested?.Invoke());
        _btnNextScreen = A("🖵 移到下一個螢幕", "把全螢幕移到另一個螢幕", () => NextScreenRequested?.Invoke());
        actions.SetColumnSpan(_btnNextScreen, 2);

        var hint = new Label
        {
            Dock = DockStyle.Bottom, Height = S(40), ForeColor = Gray, BackColor = Color.FromArgb(22, 23, 26),
            Padding = new Padding(S(10), S(2), S(6), S(4)),
            Font = new Font("Microsoft JhengHei UI", 8.5F),
            Text = "滑鼠停在左側小把手上 (或點一下) 可展開此面板\nCtrl+Alt+Break:切換全螢幕 / 視窗",
        };

        // Dock 順序:先加 Fill,再由下往上加 Top,最後加 Bottom
        Controls.Add(_conns);
        Controls.Add(searchBox);
        Controls.Add(lblConns);
        Controls.Add(_sessions);
        Controls.Add(_lblSessions);
        Controls.Add(header);
        Controls.Add(actions);
        Controls.Add(hint);
    }

    // =====================================================================

    public void SetPinned(bool pinned)
    {
        _btnPin.BackColor = pinned ? BgActive : Color.Transparent;
        _tip.SetToolTip(_btnPin, pinned ? "取消固定 (面板自動收合,遠端畫面使用整個螢幕)" : "固定面板 (不自動收合)");
    }

    public void SetSessions(IReadOnlyList<RdpSessionPage> pages, RdpSessionPage? active)
    {
        _active = active;
        _sessions.BeginUpdate();
        _sessions.Items.Clear();
        foreach (var p in pages) _sessions.Items.Add(p);
        _sessions.EndUpdate();
        _lblSessions.Text = $"已開啟 ({pages.Count})";
        // 最多佔面板高度的 45%,其餘留給連線清單
        int max = (int)(ClientSize.Height * 0.45);
        _sessions.Height = Math.Max(_sessions.ItemHeight, Math.Min(max, _sessions.ItemHeight * Math.Max(1, pages.Count) + 2));
        _btnNextScreen.Visible = Screen.AllScreens.Length > 1;
        _sessions.Invalidate();
    }

    public void SetConnections(IEnumerable<RdpConnection> conns)
    {
        _allConns = conns.OrderByDescending(c => c.IsFavorite)
                         .ThenBy(c => c.Group, StringComparer.CurrentCulture)
                         .ThenBy(c => c.Name, StringComparer.CurrentCulture)
                         .ToList();
        FillConnections();
    }

    public void InvalidateStatus()
    {
        _sessions.Invalidate();
        _conns.Invalidate();
    }

    public void FocusSearch()
    {
        _search.Focus();
        _search.SelectAll();
    }

    private void FillConnections()
    {
        var terms = _search.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var selected = _conns.SelectedItem;
        _conns.BeginUpdate();
        _conns.Items.Clear();
        foreach (var c in _allConns.Where(c => terms.All(t =>
                     c.Name.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                     c.Host.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                     c.Group.Contains(t, StringComparison.OrdinalIgnoreCase))))
            _conns.Items.Add(c);
        if (selected != null && _conns.Items.Contains(selected)) _conns.SelectedItem = selected;
        _conns.EndUpdate();
    }

    // =====================================================================
    // 繪製
    // =====================================================================

    private void DrawSession(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _sessions.Items[e.Index] is not RdpSessionPage p) return;
        bool active = p == _active;
        var bg = active ? BgActive : e.Index == _hoverSession ? BgHover : Bg;
        var dot = p.State switch
        {
            SessionState.Connected => Green,
            SessionState.Connecting => Orange,
            SessionState.Disconnected => Red,
            _ => Gray,
        };
        var state = p.State switch
        {
            SessionState.Connected => "已連線",
            SessionState.Connecting => "連線中…",
            SessionState.Disconnected => "已中斷",
            _ => "",
        };
        DrawRow(e, bg, dot, p.Connection.Name, $"{p.Connection.DisplayAddress}  ·  {state}", active ? Color.FromArgb(210, 230, 255) : TextSub);
    }

    private void DrawConnection(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _conns.Items[e.Index] is not RdpConnection c) return;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        var bg = sel ? BgItem : e.Index == _hoverConn ? BgHover : Bg;
        var dot = c.Status switch
        {
            HostStatus.Online => Green,
            HostStatus.Offline => Red,
            HostStatus.Checking => Orange,
            _ => Gray,
        };
        var addr = c.IsVnc ? $"VNC  ·  {c.DisplayAddress}" : c.DisplayAddress;
        var sub = string.IsNullOrEmpty(c.Group) ? addr : $"{c.Group}  ·  {addr}";
        DrawRow(e, bg, dot, (c.IsFavorite ? "★ " : "") + c.Name, sub, TextSub);
    }

    private void DrawRow(DrawItemEventArgs e, Color bg, Color dot, string main, string sub, Color subColor)
    {
        var g = e.Graphics;
        var r = e.Bounds;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(bg)) g.FillRectangle(b, r);

        int pad = LogicalToDeviceUnits(12);
        int d = LogicalToDeviceUnits(9);
        using (var b = new SolidBrush(dot))
            g.FillEllipse(b, r.X + pad, r.Y + (r.Height - d) / 2, d, d);

        int x = r.X + pad + d + LogicalToDeviceUnits(10);
        var textRect = new Rectangle(x, r.Y + LogicalToDeviceUnits(4), r.Right - x - pad / 2, r.Height / 2);
        var subRect = new Rectangle(x, r.Y + r.Height / 2, r.Right - x - pad / 2, r.Height / 2 - LogicalToDeviceUnits(4));
        const TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;
        using var bold = new Font(Font, FontStyle.Bold);
        using var small = new Font(Font.FontFamily, Font.Size - 1f);
        TextRenderer.DrawText(g, main, bold, textRect, TextMain, flags);
        TextRenderer.DrawText(g, sub, small, subRect, subColor, flags);
    }

    // =====================================================================

    private static void SetupList(ListBox lb, int itemHeight)
    {
        lb.DrawMode = DrawMode.OwnerDrawFixed;
        lb.ItemHeight = itemHeight;
        lb.BorderStyle = BorderStyle.None;
        lb.BackColor = Bg;
        lb.ForeColor = TextMain;
        lb.IntegralHeight = false;
    }

    private static void SetHover(ListBox lb, ref int field, int index)
    {
        if (field == index) return;
        field = index;
        lb.Invalidate();
    }

    private Button IconButton(string text, string tip)
    {
        var b = new Button
        {
            Text = text, Dock = DockStyle.Right, Width = LogicalToDeviceUnits(38),
            FlatStyle = FlatStyle.Flat, ForeColor = TextMain, BackColor = Color.Transparent,
            Font = new Font("Segoe UI Emoji", 11F), Cursor = Cursors.Hand, TabStop = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = BgHover;
        _tip.SetToolTip(b, tip);
        return b;
    }

    private Button ActionButton(string text, string tip)
    {
        var b = new Button
        {
            Text = text, Dock = DockStyle.Fill, Height = LogicalToDeviceUnits(34),
            FlatStyle = FlatStyle.Flat, ForeColor = TextMain, BackColor = BgItem,
            Margin = new Padding(LogicalToDeviceUnits(3)), Cursor = Cursors.Hand, TabStop = false,
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(60, 63, 70);
        b.FlatAppearance.MouseOverBackColor = BgHover;
        _tip.SetToolTip(b, tip);
        return b;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}
