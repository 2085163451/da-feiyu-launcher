// 设置窗口：只干一件事——手动指定四个程序的路径。留空就是自动查找。
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace QQBotLauncher
{
    internal class SettingsForm : Form
    {
        private readonly BotEnvironment _env;
        private TextBox _qq, _astr, _snow, _wb;
        private int _hoverClose;
        private RectangleF _closeRect;

        public SettingsForm(BotEnvironment env)
        {
            _env = env;
            Text = "设置路径";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            ClientSize = new Size(660, 400);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(255, 248, 252, 255);
            Font = Draw.F(9f, FontStyle.Regular);

            _qq = AddRow(0, "QQ.exe", env.QQExe ?? env.QQPathSetting, "文件");
            _astr = AddRow(1, "AstrBot 目录", env.AstrDir ?? env.AstrSetting, "目录");
            _snow = AddRow(2, "SnowLuma 目录", env.SnowDir ?? env.SnowSetting, "目录");
            _wb = AddRow(3, "wb2api 目录", env.WbDir ?? env.WbSetting, "目录");

            AnimeButton ok = new AnimeButton();
            ok.Text = "保存";
            ok.Icon = IconKind.Check;
            ok.Primary = true;
            ok.SetBounds(424, 336, 108, 40);
            ok.Click += delegate { Save(); };
            Controls.Add(ok);

            AnimeButton cancel = new AnimeButton();
            cancel.Text = "取消";
            cancel.Accent = Palette.Coral;
            cancel.SetBounds(544, 336, 96, 40);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);
        }

        private void Save()
        {
            string qq = Clean(_qq.Text);
            string astr = Clean(_astr.Text);
            string snow = Clean(_snow.Text);
            string wb = Clean(_wb.Text);
            try
            {
                // 先落盘，成功了再改内存；写失败时点「取消」不会留下改了一半的状态
                _env.SaveConfig(qq, astr, snow, wb);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "写配置失败：" + ex.Message, "大肥鱼启动器",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _env.QQPathSetting = qq;
            _env.AstrSetting = astr;
            _env.SnowSetting = snow;
            _env.WbSetting = wb;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            using (GraphicsPath p = Draw.Round(new RectangleF(0, 0, Width, Height), 20f))
                Region = new Region(p);
        }

        private static string Clean(string s)
        {
            if (s == null) return null;
            s = s.Trim();
            return s.Length == 0 ? null : s;
        }

        private TextBox AddRow(int index, string label, string value, string kind)
        {
            int y = 104 + index * 52;
            Label l = new Label();
            l.Text = label;
            l.SetBounds(28, y + 8, 108, 22);
            l.ForeColor = Palette.Ink;
            l.Font = Draw.F(9f, FontStyle.Bold);
            l.BackColor = Color.Transparent;
            Controls.Add(l);

            TextBox t = new TextBox();
            t.BorderStyle = BorderStyle.None;
            t.SetBounds(140, y + 8, 392, 22);
            t.Text = value ?? "";
            t.BackColor = Color.White;
            t.ForeColor = Palette.Ink;
            t.Font = Draw.F(9f, FontStyle.Regular);
            Controls.Add(t);

            AnimeButton browse = new AnimeButton();
            browse.Text = kind == "文件" ? "选择" : "浏览";
            browse.Icon = IconKind.Folder;
            browse.TextSize = 8.5f;
            browse.Accent = Palette.Lagoon;
            browse.SetBounds(548, y, 84, 34);
            string k = kind;
            browse.Click += delegate
            {
                if (k == "文件")
                {
                    using (OpenFileDialog d = new OpenFileDialog())
                    {
                        d.Filter = "QQ.exe|QQ.exe|可执行文件|*.exe";
                        if (d.ShowDialog() == DialogResult.OK) t.Text = d.FileName;
                    }
                }
                else
                {
                    using (FolderBrowserDialog d = new FolderBrowserDialog())
                    {
                        if (d.ShowDialog() == DialogResult.OK) t.Text = d.SelectedPath;
                    }
                }
            };
            Controls.Add(browse);
            return t;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // 底
            using (LinearGradientBrush b = new LinearGradientBrush(
                new RectangleF(0, 0, Width, Height), Color.FromArgb(255, 252, 255, 255), Color.FromArgb(255, 230, 245, 255), 90f))
                g.FillRectangle(b, 0, 0, Width, Height);

            // 标题条
            RectangleF head = new RectangleF(0, 0, Width, 72);
            using (GraphicsPath p = Draw.Round(new RectangleF(0, 0, Width, 72), 20f))
            using (LinearGradientBrush b = new LinearGradientBrush(head,
                Color.FromArgb(255, 22, 62, 118), Color.FromArgb(255, 68, 148, 208), 12f))
                g.FillPath(b, p);

            g.SetClip(new RectangleF(0, 56, Width, 16));
            using (Brush b = new SolidBrush(Color.FromArgb(255, 230, 245, 255)))
                g.FillRectangle(b, 0, 56, Width, 20);
            g.ResetClip();

            // 头像 + 标题
            RectangleF av = new RectangleF(22, 10, 50, 50);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(av);
                using (Brush b = new SolidBrush(Color.FromArgb(255, 246, 253, 255)))
                    g.FillPath(b, p);
            }
            GraphicsState st = g.Save();
            g.SetClip(new RectangleF(av.X + 1, av.Y + 1, av.Width - 2, av.Height - 2));
            Mascot.Draw(g, RectangleF.Inflate(av, 1f, 3f), true);
            g.Restore(st);

            Draw.String(g, "设置程序位置", Draw.F(13f, FontStyle.Bold), Color.White,
                new RectangleF(84, 18, 380, 26), ContentAlignment.MiddleLeft);
            Draw.String(g, "留空 = 自动查找（推荐）", Draw.F(8.5f, FontStyle.Regular), Color.FromArgb(230, 255, 255, 255),
                new RectangleF(84, 40, 380, 18), ContentAlignment.MiddleLeft);

            // 关闭按钮
            _closeRect = new RectangleF(Width - 52, 20, 28, 28);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(_closeRect);
                using (Brush b = new SolidBrush(Color.FromArgb(_hoverClose == 1 ? 120 : 55, 255, 255, 255)))
                    g.FillPath(b, p);
            }
            using (Pen pen = new Pen(Color.White, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                g.DrawLine(pen, _closeRect.X + 10, _closeRect.Y + 10, _closeRect.Right - 10, _closeRect.Bottom - 10);
                g.DrawLine(pen, _closeRect.Right - 10, _closeRect.Y + 10, _closeRect.X + 10, _closeRect.Bottom - 10);
            }

            // 输入框底板
            for (int i = 0; i < 4; i++)
            {
                RectangleF r = new RectangleF(132, 102 + i * 52, 408, 36);
                Draw.Card(g, r, 12f, Color.White, Palette.CardBorder, false);
            }

            // 当前自动识别情况：全都找到了就不用填，缺哪个就点名
            string missing = "";
            if (_env.QQExe == null) missing += "QQ ";
            if (_env.AstrDir == null) missing += "AstrBot ";
            if (_env.SnowDir == null) missing += "SnowLuma ";
            if (_env.WbDir == null) missing += "wb2api ";
            string tip = missing.Length == 0
                ? "上面就是自动识别到的位置，你不用填；只有不对的时候才改，清空 = 重新自动查找。"
                : "没找到：" + missing.Trim() + "　—— 在对应行填上路径，或者把程序放回本程序旁边。";
            Draw.String(g, tip, Draw.F(8f, FontStyle.Regular), Palette.InkSoft,
                new RectangleF(28, 302, 604, 22), ContentAlignment.MiddleLeft);

            using (Pen pen = new Pen(Color.FromArgb(255, 204, 233, 250), 1.2f))
            using (GraphicsPath p = Draw.Round(new RectangleF(0.6f, 0.6f, Width - 1.2f, Height - 1.2f), 20f))
                g.DrawPath(pen, p);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hot = _closeRect.Contains(e.Location) ? 1 : 0;
            if (hot != _hoverClose) { _hoverClose = hot; Cursor = hot == 1 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverClose = 0;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_closeRect.Contains(e.Location))
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }
            if (e.Y < 72)
            {
                Native.ReleaseCapture();
                Native.SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
            }
            base.OnMouseDown(e);
        }
    }
}