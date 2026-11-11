// Copyright © 2026 鴻森智能科技股份有限公司. All rights reserved.

namespace RDPManager.Forms;

internal static class FormHelpers
{
    public static readonly Font UiFont = new("Microsoft JhengHei UI", 9F);

    public static TableLayoutPanel NewTable()
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12, 10, 12, 10),
            AutoScroll = true,
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    public static void AddRow(TableLayoutPanel t, string label, Control c, bool stretch = true)
    {
        int row = t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(3, 7, 10, 3),
        }, 0, row);
        c.Margin = new Padding(3, 4, 3, 4);
        c.Anchor = stretch ? AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top : AnchorStyles.Left | AnchorStyles.Top;
        t.Controls.Add(c, 1, row);
    }

    public static void AddFull(TableLayoutPanel t, Control c)
    {
        int row = t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        c.Margin = new Padding(3, 4, 3, 4);
        c.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        t.Controls.Add(c, 0, row);
        t.SetColumnSpan(c, 2);
    }

    public static ComboBox DropList(params string[] items)
    {
        var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
        cb.Items.AddRange(items);
        cb.SelectedIndex = 0;
        return cb;
    }

    public static FlowLayoutPanel Flow(params Control[] controls)
    {
        var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        foreach (var c in controls) c.Margin = new Padding(0, 0, 6, 0);
        f.Controls.AddRange(controls);
        return f;
    }

    public static FlowLayoutPanel DialogButtons(Form form, out Button ok, out Button cancel, string okText = "確定")
    {
        ok = new Button { Text = okText, DialogResult = DialogResult.OK, Width = 90, Height = 30 };
        cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 90, Height = 30 };
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(8, 8, 8, 8),
        };
        panel.Controls.Add(cancel);
        panel.Controls.Add(ok);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return panel;
    }

    public static void SetupDialog(Form f, string title, Size size)
    {
        f.Text = title;
        f.Font = UiFont;
        f.FormBorderStyle = FormBorderStyle.FixedDialog;
        f.MaximizeBox = false;
        f.MinimizeBox = false;
        f.ShowInTaskbar = false;
        f.StartPosition = FormStartPosition.CenterParent;
        f.AutoScaleMode = AutoScaleMode.Dpi;
        f.ClientSize = size;
    }

    /// <summary>簡易輸入框</summary>
    public static string? InputBox(IWin32Window owner, string title, string prompt, string value = "")
    {
        using var f = new Form();
        SetupDialog(f, title, new Size(380, 140));
        var lbl = new Label { Text = prompt, AutoSize = true, Location = new Point(12, 14) };
        var txt = new TextBox { Text = value, Location = new Point(14, 40), Width = 350 };
        var buttons = DialogButtons(f, out _, out _);
        f.Controls.AddRange(new Control[] { lbl, txt, buttons });
        f.Shown += (_, _) => { txt.Focus(); txt.SelectAll(); };
        return f.ShowDialog(owner) == DialogResult.OK ? txt.Text.Trim() : null;
    }
}
