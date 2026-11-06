using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RevisionTracker.App
{
    public static class Theme
    {
        public static readonly Font Base = new Font("Segoe UI", 9f);
        public static readonly Font Bold = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font Header = new Font("Segoe UI Semibold", 11f);
        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public static readonly Color Muted = Color.FromArgb(100, 116, 139);
        public static readonly Color Panel = Color.FromArgb(248, 250, 252);
    }

    /// <summary>Simple labelled-rows dialog used by all edit forms.</summary>
    public class EditDialog : Form
    {
        protected readonly TableLayoutPanel Grid;
        protected readonly Button OkButton;
        private readonly FlowLayoutPanel _buttons;

        public EditDialog(string title, int width = 480)
        {
            Text = title;
            Font = Theme.Base;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var outer = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
            Grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Width = width };
            Grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            Grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width - 130));

            _buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
            OkButton = new Button { Text = "OK", Width = 90 };
            OkButton.Click += (s, e) => { if (ValidateAndApply()) { DialogResult = DialogResult.OK; Close(); } };
            _buttons.Controls.Add(cancel);
            _buttons.Controls.Add(OkButton);
            AcceptButton = OkButton;
            CancelButton = cancel;

            outer.Controls.Add(Grid);
            outer.Controls.Add(_buttons);
            Controls.Add(outer);
        }

        /// <summary>Return false to keep the dialog open.</summary>
        protected virtual bool ValidateAndApply() { return true; }

        protected T AddRow<T>(string label, T control) where T : Control
        {
            var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 6) };
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 3, 0, 3);
            Grid.Controls.Add(l);
            Grid.Controls.Add(control);
            return control;
        }

        protected Label AddNote(string text, Color? color = null)
        {
            var l = new Label { Text = text, AutoSize = true, MaximumSize = new Size(Grid.Width - 10, 0), ForeColor = color ?? Theme.Muted, Margin = new Padding(0, 4, 0, 4) };
            Grid.Controls.Add(l);
            Grid.SetColumnSpan(l, 2);
            return l;
        }

        protected static ComboBox Combo(IEnumerable<string> items, string value, bool editable = true)
        {
            var c = new ComboBox { DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList };
            foreach (var i in items) c.Items.Add(i);
            if (editable) c.Text = value ?? "";
            else if (value != null && c.Items.Contains(value)) c.SelectedItem = value;
            else if (c.Items.Count > 0) c.SelectedIndex = 0;
            return c;
        }

        protected static NumericUpDown Number(int value)
        {
            return new NumericUpDown { Minimum = 0, Maximum = 999, Value = Math.Max(0, Math.Min(999, value)) };
        }

        protected static TextBox MultiLine(string text)
        {
            return new TextBox { Multiline = true, Height = 60, ScrollBars = ScrollBars.Vertical, Text = text ?? "" };
        }

        protected void Warn(string message)
        {
            MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public static class Format
    {
        public static string Size(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.#") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / 1024.0 / 1024).ToString("0.#") + " MB";
            return (bytes / 1024.0 / 1024 / 1024).ToString("0.##") + " GB";
        }

        public static string When(DateTime t)
        {
            var now = DateTime.Now;
            if (t.Date == now.Date) return "Today " + t.ToString("HH:mm");
            if (t.Date == now.Date.AddDays(-1)) return "Yesterday " + t.ToString("HH:mm");
            return t.ToString("yyyy-MM-dd HH:mm");
        }
    }
}
