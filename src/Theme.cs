// 二次元风格的配色、绘制工具、图标和自绘控件
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace QQBotLauncher
{
    // 「蓝色大肥鱼」配色：深海军蓝头发 + 天蓝发梢 + 白蕾丝。
    // Sea/SeaDeep 是主色（按钮、强调），Lagoon 是次级蓝，Coral 只给「停止」这类需要区分的地方。
    internal static class Palette
    {
        public static readonly Color Ink = Color.FromArgb(255, 28, 46, 80);
        public static readonly Color InkSoft = Color.FromArgb(255, 92, 122, 160);
        public static readonly Color InkFaint = Color.FromArgb(255, 156, 180, 208);
        public static readonly Color Sea = Color.FromArgb(255, 82, 192, 236);
        public static readonly Color SeaDeep = Color.FromArgb(255, 42, 144, 204);
        public static readonly Color Lagoon = Color.FromArgb(255, 88, 130, 212);
        public static readonly Color Sky = Color.FromArgb(255, 128, 214, 248);
        public static readonly Color Mint = Color.FromArgb(255, 64, 196, 182);
        public static readonly Color Amber = Color.FromArgb(255, 244, 180, 92);
        public static readonly Color Coral = Color.FromArgb(255, 242, 134, 146);
        public static readonly Color CardBorder = Color.FromArgb(255, 210, 234, 250);
        public static readonly Color LogBg = Color.FromArgb(255, 16, 38, 66);
        public static readonly Color LogText = Color.FromArgb(255, 164, 230, 246);
    }

    internal enum IconKind
    {
        None, Play, Stop, Restart, Globe, Gear, Folder, Power, Chat, Sparkle, Clock, Check
    }

    internal static class Draw
    {
        public static Font F(float size, FontStyle style)
        {
            try { return new Font("Microsoft YaHei UI", size, style); }
            catch { return new Font(FontFamily.GenericSansSerif, size, style); }
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            if (d <= 1.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        public static void Shadow(Graphics g, RectangleF r, float radius, int alpha, float offsetY)
        {
            for (int i = 3; i >= 1; i--)
            {
                RectangleF rr = RectangleF.Inflate(r, i * 1.6f, i * 1.6f);
                rr.Offset(0f, offsetY + i);
                int a = (int)(alpha / (i * 1.6f));
                using (GraphicsPath p = Round(rr, radius + i))
                using (Brush b = new SolidBrush(Color.FromArgb(Math.Max(0, a), 46, 104, 168)))
                    g.FillPath(b, p);
            }
        }

        public static void Card(Graphics g, RectangleF r, float radius, Color fill, Color border, bool shadow)
        {
            if (shadow) Shadow(g, r, radius, 46, 1.5f);
            using (GraphicsPath p = Round(r, radius))
            {
                using (Brush b = new SolidBrush(fill)) g.FillPath(b, p);
                if (border.A > 0)
                    using (Pen pen = new Pen(border, 1f)) g.DrawPath(pen, p);
            }
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static void String(Graphics g, string text, Font font, Color color, RectangleF rect, ContentAlignment align)
        {
            StringFormat sf = new StringFormat();
            sf.Trimming = StringTrimming.EllipsisCharacter;
            sf.FormatFlags = StringFormatFlags.NoWrap;
            switch (align)
            {
                case ContentAlignment.MiddleLeft: sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Center; break;
                case ContentAlignment.MiddleRight: sf.Alignment = StringAlignment.Far; sf.LineAlignment = StringAlignment.Center; break;
                case ContentAlignment.MiddleCenter: sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; break;
                case ContentAlignment.TopLeft: sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Near; break;
                default: sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Center; break;
            }
            using (Brush b = new SolidBrush(color))
            {
                // 文字稍微上移一点，视觉上更居中
                g.DrawString(text, font, b, rect, sf);
            }
            sf.Dispose();
        }

        // ------------------------------------------------------------------
        // 矢量图标
        // ------------------------------------------------------------------
        public static void Icon(Graphics g, IconKind kind, RectangleF r, Color color, float weight)
        {
            if (kind == IconKind.None) return;
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = Math.Max(1.4f, r.Width * weight);

            using (Pen pen = new Pen(color, w))
            using (Brush brush = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                switch (kind)
                {
                    case IconKind.Play:
                        g.FillPolygon(brush, new PointF[] {
                            new PointF(r.X + r.Width * 0.28f, r.Y + r.Height * 0.16f),
                            new PointF(r.X + r.Width * 0.84f, cy),
                            new PointF(r.X + r.Width * 0.28f, r.Y + r.Height * 0.84f) });
                        break;

                    case IconKind.Stop:
                        using (GraphicsPath p = Round(new RectangleF(
                            r.X + r.Width * 0.24f, r.Y + r.Height * 0.24f, r.Width * 0.52f, r.Height * 0.52f), w))
                            g.FillPath(brush, p);
                        break;

                    case IconKind.Restart:
                        g.DrawArc(pen, new RectangleF(r.X + r.Width * 0.18f, r.Y + r.Height * 0.18f, r.Width * 0.64f, r.Height * 0.64f), 40f, 280f);
                        g.FillPolygon(brush, new PointF[] {
                            new PointF(r.X + r.Width * 0.78f, r.Y + r.Height * 0.14f),
                            new PointF(r.X + r.Width * 0.90f, r.Y + r.Height * 0.40f),
                            new PointF(r.X + r.Width * 0.62f, r.Y + r.Height * 0.40f) });
                        break;

                    case IconKind.Globe:
                        g.DrawEllipse(pen, new RectangleF(r.X + r.Width * 0.14f, r.Y + r.Height * 0.14f, r.Width * 0.72f, r.Height * 0.72f));
                        g.DrawEllipse(pen, new RectangleF(r.X + r.Width * 0.34f, r.Y + r.Height * 0.14f, r.Width * 0.32f, r.Height * 0.72f));
                        g.DrawLine(pen, r.X + r.Width * 0.14f, cy, r.X + r.Width * 0.86f, cy);
                        break;

                    case IconKind.Gear:
                        for (int i = 0; i < 8; i++)
                        {
                            double ang = Math.PI * i / 4.0;
                            float x1 = cx + (float)Math.Cos(ang) * r.Width * 0.30f;
                            float y1 = cy + (float)Math.Sin(ang) * r.Width * 0.30f;
                            float x2 = cx + (float)Math.Cos(ang) * r.Width * 0.44f;
                            float y2 = cy + (float)Math.Sin(ang) * r.Width * 0.44f;
                            g.DrawLine(pen, x1, y1, x2, y2);
                        }
                        g.DrawEllipse(pen, new RectangleF(cx - r.Width * 0.26f, cy - r.Width * 0.26f, r.Width * 0.52f, r.Width * 0.52f));
                        g.DrawEllipse(pen, new RectangleF(cx - r.Width * 0.09f, cy - r.Width * 0.09f, r.Width * 0.18f, r.Width * 0.18f));
                        break;

                    case IconKind.Folder:
                        using (GraphicsPath p = new GraphicsPath())
                        {
                            p.AddLine(r.X + r.Width * 0.12f, r.Y + r.Height * 0.26f, r.X + r.Width * 0.40f, r.Y + r.Height * 0.26f);
                            p.AddLine(r.X + r.Width * 0.46f, r.Y + r.Height * 0.38f, r.X + r.Width * 0.88f, r.Y + r.Height * 0.38f);
                            p.AddLine(r.X + r.Width * 0.88f, r.Y + r.Height * 0.38f, r.X + r.Width * 0.88f, r.Y + r.Height * 0.76f);
                            p.AddLine(r.X + r.Width * 0.88f, r.Y + r.Height * 0.76f, r.X + r.Width * 0.12f, r.Y + r.Height * 0.76f);
                            p.AddLine(r.X + r.Width * 0.12f, r.Y + r.Height * 0.76f, r.X + r.Width * 0.12f, r.Y + r.Height * 0.26f);
                            g.DrawPath(pen, p);
                        }
                        break;

                    case IconKind.Power:
                        g.DrawArc(pen, new RectangleF(cx - r.Width * 0.32f, cy - r.Width * 0.28f, r.Width * 0.64f, r.Width * 0.64f), -50f, 280f);
                        g.DrawLine(pen, cx, cy - r.Width * 0.44f, cx, cy - r.Width * 0.04f);
                        break;

                    case IconKind.Chat:
                        using (GraphicsPath p = Round(new RectangleF(r.X + r.Width * 0.12f, r.Y + r.Height * 0.16f, r.Width * 0.76f, r.Height * 0.56f), r.Width * 0.22f))
                        {
                            g.FillPath(brush, p);
                        }
                        g.FillPolygon(brush, new PointF[] {
                            new PointF(r.X + r.Width * 0.30f, r.Y + r.Height * 0.66f),
                            new PointF(r.X + r.Width * 0.30f, r.Y + r.Height * 0.90f),
                            new PointF(r.X + r.Width * 0.52f, r.Y + r.Height * 0.68f) });
                        break;

                    case IconKind.Sparkle:
                        g.FillPolygon(brush, StarPoints(cx, cy, r.Width * 0.46f, r.Width * 0.16f, 4));
                        break;

                    case IconKind.Clock:
                        g.DrawEllipse(pen, new RectangleF(r.X + r.Width * 0.14f, r.Y + r.Height * 0.14f, r.Width * 0.72f, r.Height * 0.72f));
                        g.DrawLine(pen, cx, cy, cx, cy - r.Height * 0.22f);
                        g.DrawLine(pen, cx, cy, cx + r.Width * 0.18f, cy + r.Height * 0.08f);
                        break;

                    case IconKind.Check:
                        g.DrawLines(pen, new PointF[] {
                            new PointF(r.X + r.Width * 0.20f, r.Y + r.Height * 0.54f),
                            new PointF(r.X + r.Width * 0.42f, r.Y + r.Height * 0.76f),
                            new PointF(r.X + r.Width * 0.82f, r.Y + r.Height * 0.24f) });
                        break;
                }
            }
            g.SmoothingMode = old;
        }

        public static PointF[] StarPoints(float cx, float cy, float outer, float inner, int points)
        {
            PointF[] pts = new PointF[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                double ang = -Math.PI / 2 + Math.PI * i / points;
                float rad = (i % 2 == 0) ? outer : inner;
                pts[i] = new PointF(cx + (float)Math.Cos(ang) * rad, cy + (float)Math.Sin(ang) * rad);
            }
            return pts;
        }
    }

    // 所有自绘控件的基类：负责把自己的背景画成窗口的底色（保证圆角外不出现灰块）
    internal class GlassControl : Control
    {
        public GlassControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected void PaintBackdrop(Graphics g)
        {
            MainForm f = FindForm() as MainForm;
            if (f != null)
            {
                GraphicsState st = g.Save();
                g.TranslateTransform(-Left, -Top);
                f.PaintBackdrop(g);
                g.Restore(st);
                return;
            }
            // 对话框里没有窗口背景，用所在窗体底色填满即可
            using (Brush b = new SolidBrush(Parent != null ? Parent.BackColor : Color.White))
                g.FillRectangle(b, ClientRectangle);
        }
    }

    // ----------------------------------------------------------------------
    // 圆角按钮
    // ----------------------------------------------------------------------
    internal class AnimeButton : GlassControl
    {
        public IconKind Icon = IconKind.None;
        public bool Primary;
        public Color Accent = Palette.Sea;
        public float TextSize = 9.5f;

        private bool _hover;
        private bool _down;

        public AnimeButton()
        {
            Cursor = Cursors.Hand;
            Font = Draw.F(9.5f, FontStyle.Bold);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            PaintBackdrop(g);

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float radius = Height / 2f;
            Color fillA, fillB, fg, border;

            if (!Enabled)
            {
                fillA = Color.FromArgb(255, 240, 248, 253);
                fillB = Color.FromArgb(255, 231, 242, 251);
                fg = Palette.InkFaint;
                border = Color.FromArgb(255, 223, 237, 250);
            }
            else if (Primary)
            {
                fillA = _hover ? Draw.Mix(Palette.Sea, Color.White, 0.22f) : Palette.Sea;
                fillB = _hover ? Palette.SeaDeep : Draw.Mix(Palette.SeaDeep, Palette.Lagoon, 0.35f);
                fg = Color.White;
                border = Color.FromArgb(90, 255, 255, 255);
                Draw.Shadow(g, r, radius, _hover ? 70 : 44, 2f);
            }
            else
            {
                int a = _hover ? 255 : 224;
                fillA = Color.FromArgb(a, 255, 255, 255);
                fillB = Color.FromArgb(a, 246, 252, 255);
                fg = _hover ? Accent : Palette.Ink;
                border = Color.FromArgb(_hover ? 190 : 120, Accent);
            }

            if (_down) r.Offset(0f, 1f);
            using (GraphicsPath p = Draw.Round(r, radius))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new RectangleF(r.X, r.Y, r.Width, r.Height), fillA, fillB, 90f))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(border, 1.2f)) g.DrawPath(pen, p);
            }

            // 图标 + 文字整体居中
            SizeF textSize = g.MeasureString(Text, Font);
            float iconW = Icon == IconKind.None ? 0f : 15f;
            float gap = Icon == IconKind.None ? 0f : 7f;
            float total = iconW + gap + textSize.Width;
            float x = Math.Max(6f, (Width - total) / 2f);

            if (Icon != IconKind.None)
            {
                RectangleF ir = new RectangleF(x, (Height - 15f) / 2f - 1f, 15f, 15f);
                Draw.Icon(g, Icon, ir, fg, 0.13f);
                x += iconW + gap;
            }

            RectangleF tr = new RectangleF(x, 0f, Width - x - 4f, Height);
            using (StringFormat sf = new StringFormat())
            using (Brush tb = new SolidBrush(fg))
            {
                sf.Alignment = StringAlignment.Near;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                tr.Offset(0f, -0.5f);
                g.DrawString(Text, Font, tb, tr, sf);
            }
        }
    }

    // ----------------------------------------------------------------------
    // 组件状态卡片
    // ----------------------------------------------------------------------
    internal class StatusCard : GlassControl
    {
        public string Title = "";
        public string Detail = "";
        public bool Running;
        public Color Accent = Palette.Sea;
        public IconKind Icon = IconKind.Chat;
        public float Pulse;

        private bool _hover;

        public StatusCard()
        {
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            PaintBackdrop(g);

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color border = _hover ? Color.FromArgb(220, Accent) : Palette.CardBorder;
            Draw.Card(g, r, 18f, Color.FromArgb(_hover ? 255 : 246, 255, 255, 255), border, true);

            // 图标底板
            RectangleF ib = new RectangleF(14f, (Height - 46f) / 2f, 46f, 46f);
            using (GraphicsPath p = Draw.Round(ib, 15f))
            using (LinearGradientBrush b = new LinearGradientBrush(ib,
                Draw.Mix(Accent, Color.White, 0.28f), Accent, 55f))
            {
                g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(80, 255, 255, 255), 1f)) g.DrawPath(pen, p);
            }
            Draw.Icon(g, Icon, new RectangleF(ib.X + 13f, ib.Y + 13f, 20f, 20f), Color.White, 0.14f);

            // 文字
            float tx = 72f;
            Draw.String(g, Title, Draw.F(10.5f, FontStyle.Bold), Palette.Ink,
                new RectangleF(tx, 14f, Width - tx - 78f, 22f), ContentAlignment.MiddleLeft);
            Draw.String(g, Detail, Draw.F(8.5f, FontStyle.Regular), Palette.InkSoft,
                new RectangleF(tx, 38f, Width - tx - 12f, 34f), ContentAlignment.TopLeft);

            // 状态药丸
            RectangleF pill = new RectangleF(Width - 76f, 16f, 64f, 24f);
            Color pillBg = Running ? Color.FromArgb(255, 224, 248, 251) : Color.FromArgb(255, 234, 242, 250);
            Color pillFg = Running ? Color.FromArgb(255, 16, 126, 148) : Palette.InkFaint;
            using (GraphicsPath p = Draw.Round(pill, 12f))
            using (Brush b = new SolidBrush(pillBg))
                g.FillPath(b, p);

            float dotX = pill.X + 11f;
            float dotY = pill.Y + pill.Height / 2f;
            if (Running)
            {
                int a = (int)(70 + 60 * Math.Sin(Pulse));
                using (Brush b = new SolidBrush(Color.FromArgb(Math.Max(0, Math.Min(255, a)), Palette.Mint)))
                    g.FillEllipse(b, dotX - 6f, dotY - 6f, 12f, 12f);
            }
            using (Brush b = new SolidBrush(Running ? Palette.Mint : Palette.InkFaint))
                g.FillEllipse(b, dotX - 3f, dotY - 3f, 6f, 6f);

            Draw.String(g, Running ? "运行中" : "未运行", Draw.F(8f, FontStyle.Bold), pillFg,
                new RectangleF(pill.X + 17f, pill.Y, pill.Width - 19f, pill.Height), ContentAlignment.MiddleLeft);
        }
    }

    // ----------------------------------------------------------------------
    // 顶部标题栏（渐变 + 飘落花瓣 + 头像 + 窗口按钮）
    // ----------------------------------------------------------------------
    internal class HeaderControl : GlassControl
    {
        public string Title = "";
        public string Subtitle = "";
        public bool ShowMinimize = true;

        private class Bubble
        {
            public float X, Y, Size, Drift, Phase, Rise;
        }

        private readonly Bubble[] _bubbles;
        private readonly Random _rnd = new Random();
        private readonly Timer _timer;
        private int _tick;
        private int _hoverBtn = -1;

        public event EventHandler CloseClicked;
        public event EventHandler MinimizeClicked;
        public event EventHandler SettingsClicked;

        public HeaderControl()
        {
            _bubbles = new Bubble[16];
            for (int i = 0; i < _bubbles.Length; i++)
            {
                _bubbles[i] = new Bubble();
                ResetBubble(_bubbles[i], true);
            }
            _timer = new Timer();
            _timer.Interval = 60;
            _timer.Tick += delegate
            {
                _tick++;
                for (int i = 0; i < _bubbles.Length; i++)
                {
                    Bubble b = _bubbles[i];
                    // 气泡往上浮，和「大肥鱼在水里」的感觉一致
                    b.Y -= b.Rise;
                    b.X += (float)Math.Sin((_tick + b.Phase) * 0.028) * b.Drift;
                    if (b.Y < -12f) ResetBubble(b, false);
                }
                Invalidate();
            };
            _timer.Start();
        }

        private void ResetBubble(Bubble b, bool anywhere)
        {
            b.X = (float)_rnd.NextDouble() * Math.Max(Width, 400);
            b.Y = anywhere ? (float)_rnd.NextDouble() * Math.Max(Height, 80) : Height + 10f + (float)_rnd.NextDouble() * 30f;
            b.Size = 2.6f + (float)_rnd.NextDouble() * 4.6f;
            b.Rise = 0.30f + (float)_rnd.NextDouble() * 0.65f;
            b.Drift = 0.10f + (float)_rnd.NextDouble() * 0.35f;
            b.Phase = (float)_rnd.NextDouble() * 100f;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            RectangleF box = new RectangleF(0, 0, Width, Height);
            using (LinearGradientBrush b = new LinearGradientBrush(box,
                Color.FromArgb(255, 22, 62, 118), Color.FromArgb(255, 68, 148, 208), 12f))
                g.FillRectangle(b, box);

            // 右上角水面反光
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(new RectangleF(Width * 0.52f, -Height * 1.35f, Width * 0.75f, Height * 2.6f));
                using (PathGradientBrush pb = new PathGradientBrush(p))
                {
                    pb.CenterColor = Color.FromArgb(110, 210, 246, 255);
                    pb.SurroundColors = new Color[] { Color.FromArgb(0, 210, 246, 255) };
                    g.FillPath(pb, p);
                }
            }

            // 上浮的气泡
            for (int i = 0; i < _bubbles.Length; i++)
            {
                Bubble b = _bubbles[i];
                float r = b.Size;
                using (Brush fill = new SolidBrush(Color.FromArgb(46, 255, 255, 255)))
                    g.FillEllipse(fill, b.X - r, b.Y - r, r * 2f, r * 2f);
                using (Pen ring = new Pen(Color.FromArgb(150, 235, 252, 255), Math.Max(1f, r * 0.30f)))
                    g.DrawEllipse(ring, b.X - r, b.Y - r, r * 2f, r * 2f);
                using (Brush hl = new SolidBrush(Color.FromArgb(170, 255, 255, 255)))
                    g.FillEllipse(hl, b.X - r * 0.45f, b.Y - r * 0.55f, r * 0.55f, r * 0.55f);
            }

            // 头像
            RectangleF av = new RectangleF(22f, (Height - 58f) / 2f, 58f, 58f);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(RectangleF.Inflate(av, 3f, 3f));
                using (Brush b = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
                    g.FillPath(b, p);
            }
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(av);
                using (Brush b = new SolidBrush(Color.FromArgb(255, 246, 253, 255)))
                    g.FillPath(b, p);
            }
            GraphicsState stc = g.Save();
            g.SetClip(new RectangleF(av.X + 1f, av.Y + 1f, av.Width - 2f, av.Height - 2f));
            Mascot.Draw(g, RectangleF.Inflate(av, 2f, 4f), false);
            g.Restore(stc);

            // 标题
            float tx = av.Right + 16f;
            Draw.String(g, Title, Draw.F(17f, FontStyle.Bold), Color.White,
                new RectangleF(tx, Height / 2f - 24f, Width - tx - 120f, 28f), ContentAlignment.MiddleLeft);
            Draw.String(g, Subtitle, Draw.F(8.5f, FontStyle.Regular), Color.FromArgb(225, 255, 255, 255),
                new RectangleF(tx, Height / 2f + 2f, Width - tx - 120f, 22f), ContentAlignment.MiddleLeft);

            // 窗口按钮（自绘，命中区在 OnMouseMove/Down 里判断）
            DrawCircleButton(g, SettingsRect(), _hoverBtn == 2);
            DrawCircleButton(g, MinimizeRect(), _hoverBtn == 0);
            DrawCircleButton(g, CloseRect(), _hoverBtn == 1);
            RectangleF gr = SettingsRect();
            Draw.Icon(g, IconKind.Gear, new RectangleF(gr.X + 8f, gr.Y + 8f, 14f, 14f), Color.White, 0.16f);
            using (Pen pen = new Pen(Color.White, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                RectangleF mr = MinimizeRect();
                g.DrawLine(pen, mr.X + mr.Width * 0.32f, mr.Y + mr.Height * 0.54f, mr.X + mr.Width * 0.68f, mr.Y + mr.Height * 0.54f);
                RectangleF cr = CloseRect();
                g.DrawLine(pen, cr.X + cr.Width * 0.34f, cr.Y + cr.Height * 0.34f, cr.X + cr.Width * 0.66f, cr.Y + cr.Height * 0.66f);
                g.DrawLine(pen, cr.X + cr.Width * 0.66f, cr.Y + cr.Height * 0.34f, cr.X + cr.Width * 0.34f, cr.Y + cr.Height * 0.66f);
            }
        }

        private void DrawCircleButton(Graphics g, RectangleF r, bool hot)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(r);
                using (Brush b = new SolidBrush(Color.FromArgb(hot ? 110 : 55, 255, 255, 255)))
                    g.FillPath(b, p);
            }
        }

        private RectangleF SettingsRect()
        {
            return new RectangleF(Width - 118f, Height / 2f - 15f, 30f, 30f);
        }

        private RectangleF MinimizeRect()
        {
            return new RectangleF(Width - 82f, Height / 2f - 15f, 30f, 30f);
        }

        private RectangleF CloseRect()
        {
            return new RectangleF(Width - 46f, Height / 2f - 15f, 30f, 30f);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            if (SettingsRect().Contains(e.Location)) h = 2;
            else if (MinimizeRect().Contains(e.Location)) h = 0;
            else if (CloseRect().Contains(e.Location)) h = 1;
            if (h != _hoverBtn) { _hoverBtn = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverBtn = -1;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (SettingsRect().Contains(e.Location))
                {
                    if (SettingsClicked != null) SettingsClicked(this, EventArgs.Empty);
                    return;
                }
                if (MinimizeRect().Contains(e.Location))
                {
                    if (MinimizeClicked != null) MinimizeClicked(this, EventArgs.Empty);
                    return;
                }
                if (CloseRect().Contains(e.Location))
                {
                    if (CloseClicked != null) CloseClicked(this, EventArgs.Empty);
                    return;
                }
                // 拖动窗口
                Native.ReleaseCapture();
                Native.SendMessage(FindForm().Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
            }
            base.OnMouseDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    internal static class Native
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    }
}
