// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

using RDPManager.Services;
using static RDPManager.Forms.FormHelpers;

namespace RDPManager.Forms;

/// <summary>連線紀錄</summary>
public class HistoryForm : Form
{
    private readonly DataStore _store;
    private readonly ListView _list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
    private readonly TextBox _txtFilter = new() { Width = 240, PlaceholderText = "篩選名稱 / 主機 / 事件…" };

    public HistoryForm(DataStore store)
    {
        _store = store;
        Text = "連線紀錄";
        Font = UiFont;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Size = new Size(900, 520);
        MinimizeBox = false;

        _list.Columns.Add("時間", 150);
        _list.Columns.Add("名稱", 160);
        _list.Columns.Add("主機", 150);
        _list.Columns.Add("方式", 70);
        _list.Columns.Add("事件", 90);
        _list.Columns.Add("說明", 260);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 8, 8, 0) };
        var btnClear = new Button { Text = "清除全部紀錄", AutoSize = true };
        var btnClose = new Button { Text = "關閉", AutoSize = true, DialogResult = DialogResult.Cancel };
        var lbl = new Label { Text = "篩選:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        top.Controls.AddRange(new Control[] { lbl, _txtFilter, btnClear, btnClose });
        CancelButton = btnClose;

        _txtFilter.TextChanged += (_, _) => Fill();
        btnClear.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "確定要清除所有連線紀錄?", "清除紀錄", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _store.ClearHistory();
            Fill();
        };

        Controls.Add(_list);
        Controls.Add(top);
        Fill();
    }

    private void Fill()
    {
        var f = _txtFilter.Text.Trim();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var h in _store.History)
        {
            if (f.Length > 0 && !(h.Name.Contains(f, StringComparison.OrdinalIgnoreCase)
                                  || h.Host.Contains(f, StringComparison.OrdinalIgnoreCase)
                                  || h.Event.Contains(f, StringComparison.OrdinalIgnoreCase)
                                  || h.Detail.Contains(f, StringComparison.OrdinalIgnoreCase)))
                continue;
            var item = new ListViewItem(h.Time.ToString("yyyy/MM/dd HH:mm:ss"));
            item.SubItems.Add(h.Name);
            item.SubItems.Add(h.Host);
            item.SubItems.Add(h.Mode);
            item.SubItems.Add(h.Event);
            item.SubItems.Add(h.Detail);
            if (h.Event.Contains("失敗")) item.ForeColor = Color.Firebrick;
            _list.Items.Add(item);
        }
        _list.EndUpdate();
    }
}
