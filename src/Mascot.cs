// 看板娘头像：全部用 GDI+ 代码绘制，不依赖任何图片文件。
// 单独一个文件，方便 IconGen 工具复用。
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace QQBotLauncher
{
    internal static class Mascot
    {
        // 「蓝色大肥鱼」配色：发根深海军蓝，发梢渐变到天蓝，眼睛是海蓝，配白蕾丝
        static readonly Color HairHi = Color.FromArgb(255, 118, 166, 224);
        static readonly Color HairA = Color.FromArgb(255, 44, 62, 116);
        static readonly Color HairB = Color.FromArgb(255, 58, 94, 158);
        static readonly Color HairC = Color.FromArgb(255, 98, 202, 238);
        static readonly Color Line = Color.FromArgb(255, 16, 32, 62);
        static readonly Color Skin = Color.FromArgb(255, 255, 240, 232);
        static readonly Color SkinShade = Color.FromArgb(255, 255, 214, 203);
        static readonly Color BlushC = Color.FromArgb(110, 255, 138, 156);
        static readonly Color EarIn = Color.FromArgb(255, 152, 220, 246);
        static readonly Color EyeLine = Color.FromArgb(255, 14, 28, 54);
        static readonly Color IrisTop = Color.FromArgb(255, 26, 68, 138);
        static readonly Color IrisMid = Color.FromArgb(255, 58, 138, 218);
        static readonly Color IrisBot = Color.FromArgb(255, 126, 232, 246);
        static readonly Color PinFill = Color.FromArgb(255, 234, 249, 255);
        static readonly Color PinLine = Color.FromArgb(255, 28, 92, 148);
        static readonly Color LaceFill = Color.FromArgb(255, 255, 255, 255);
        static readonly Color LaceLine = Color.FromArgb(255, 186, 220, 242);
        static readonly Color White = Color.FromArgb(255, 255, 255, 255);

        // 未就绪 / 待机状态可以把眼睛里的小高光调暗
        public static void Draw(Graphics g, RectangleF box, bool simple)
        {
            // 有 ComfyUI 出的立绘就直接用；没有（比如单独的图标生成工具里）退回矢量绘制
            Image art = Avatar();
            if (art != null) { DrawArt(g, art, box); return; }

            SmoothingMode oldSm = g.SmoothingMode;
            PixelOffsetMode oldPo = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            try
            {
                float u = Math.Min(box.Width, box.Height);
                float lw = u * (simple ? 0.016f : 0.0115f);

                // ---- 后层头发（整体轮廓）
                using (Brush b = Grad(box, HairA, HairC))
                using (GraphicsPath p = BackHair(box))
                {
                    g.FillPath(b, p);
                    using (Pen pen = new Pen(Line, lw * 1.1f)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, p); }
                }

                // ---- 鲸鱼鳍耳
                DrawEar(g, box, lw, false);
                DrawEar(g, box, lw, true);

                // ---- 呆毛
                using (Brush b = Grad(box, HairHi, HairB))
                using (GraphicsPath p = Strand(box, 0.478f, 0.088f, 0.024f, 0.545f, 0.026f, 0.004f, 0.22f))
                {
                    g.FillPath(b, p);
                    using (Pen pen = new Pen(Line, lw * 0.9f)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, p); }
                }

                // ---- 脸
                RectangleF face = Rect(box, 0.268f, 0.352f, 0.464f, 0.490f);
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(face);
                    using (Brush b = new SolidBrush(Skin)) g.FillPath(b, p);
                    // 下巴/脸颊阴影
                    using (Brush sb = new SolidBrush(Color.FromArgb(70, 255, 190, 178)))
                    using (GraphicsPath sp = new GraphicsPath())
                    {
                        sp.AddEllipse(Rect(box, 0.310f, 0.730f, 0.380f, 0.110f));
                        g.FillPath(sb, sp);
                    }
                    using (Pen pen = new Pen(Line, lw)) g.DrawPath(pen, p);

                    // 刘海的投影，让脸有点立体感
                    Region oldClip = g.Clip;
                    g.SetClip(p, CombineMode.Intersect);
                    using (Brush hb = new SolidBrush(Color.FromArgb(24, 255, 186, 172)))
                        g.FillEllipse(hb, Rect(box, 0.240f, 0.330f, 0.520f, 0.220f));
                    g.Clip = oldClip;
                }

                // ---- 刘海（一整片，只在边缘描线，避免变成碎玻璃）
                DrawFringe(g, box, lw, simple);

                // ---- 白色蕾丝头饰（贴着发际线，简单模式下省略，免得糊成一团）
                if (!simple) DrawHeaddress(g, box, lw);

                // ---- 眼睛
                DrawEye(g, box, 0.366f, 0.618f, 0.156f, 0.200f, false, lw, simple);
                DrawEye(g, box, 0.634f, 0.618f, 0.156f, 0.200f, true, lw, simple);

                // ---- 腮红
                Color blush = simple ? Color.FromArgb(90, 255, 118, 156) : BlushC;
                using (Brush b = new SolidBrush(blush))
                {
                    g.FillEllipse(b, Rect(box, 0.300f, 0.712f, 0.115f, 0.058f));
                    g.FillEllipse(b, Rect(box, 0.585f, 0.712f, 0.115f, 0.058f));
                }

                // ---- 鼻子
                if (!simple)
                {
                    using (Pen pen = new Pen(Color.FromArgb(110, 214, 140, 130), lw * 0.8f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawArc(pen, Rect(box, 0.487f, 0.686f, 0.028f, 0.020f), 20f, 120f);
                    }
                }

                // ---- 猫嘴 ω
                using (Pen pen = new Pen(Line, lw * 1.05f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, Rect(box, 0.450f, 0.738f, 0.050f, 0.042f), 0f, 180f);
                    g.DrawArc(pen, Rect(box, 0.500f, 0.738f, 0.050f, 0.042f), 0f, 180f);
                }

                // ---- 发饰：小鲸鱼尾巴
                DrawWhalePin(g, box, 0.206f, 0.368f, 0.056f, lw);

                // ---- 发丝高光
                using (Pen pen = new Pen(Color.FromArgb(simple ? 100 : 130, White), u * (simple ? 0.020f : 0.016f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddBezier(P(box, 0.185f, 0.560f), P(box, 0.205f, 0.420f), P(box, 0.265f, 0.330f), P(box, 0.330f, 0.285f));
                        g.DrawPath(pen, p);
                        p.Reset();
                        p.AddBezier(P(box, 0.815f, 0.560f), P(box, 0.795f, 0.420f), P(box, 0.735f, 0.330f), P(box, 0.670f, 0.285f));
                        g.DrawPath(pen, p);
                    }
                }
            }
            finally
            {
                g.SmoothingMode = oldSm;
                g.PixelOffsetMode = oldPo;
            }
        }

        // ------------------------------------------------------------------
        // 应用图标：圆角底板 + 头像（小尺寸自动放大脸部，保证 16px 也认得出）
        // ------------------------------------------------------------------
        public static void DrawAppIcon(Graphics g, RectangleF box, bool simple)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                RectangleF plate = new RectangleF(box.X + box.Width * 0.015f, box.Y + box.Height * 0.015f,
                    box.Width * 0.97f, box.Height * 0.97f);
                using (GraphicsPath p = RoundPath(plate, box.Width * 0.235f))
                {
                    using (LinearGradientBrush bg = new LinearGradientBrush(
                        new RectangleF(plate.X, plate.Y, plate.Width, plate.Height),
                        Color.FromArgb(255, 236, 250, 255), Color.FromArgb(255, 152, 212, 246), 60f))
                        g.FillPath(bg, p);

                    // 左上角柔光
                    using (Brush gl = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
                        g.FillEllipse(gl, new RectangleF(plate.X - plate.Width * 0.15f, plate.Y - plate.Height * 0.30f,
                            plate.Width * 0.85f, plate.Height * 0.85f));

                    using (Pen pen = new Pen(Color.FromArgb(150, 255, 255, 255), Math.Max(1f, box.Width * 0.014f)))
                        g.DrawPath(pen, p);
                }

                float zoom = simple ? (box.Width <= 34f ? 1.42f : 1.24f) : 1.0f;
                RectangleF head = new RectangleF(
                    box.X + box.Width * (0.5f - 0.5f * zoom) + box.Width * 0.02f,
                    box.Y + box.Height * (0.5f - 0.5f * zoom) + box.Height * 0.02f,
                    box.Width * zoom, box.Height * zoom);
                Draw(g, head, simple);

                if (!simple)
                {
                    using (Brush b = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                    {
                        GraphicsPath s1 = SparkPath(box, 0.855f, 0.185f, 0.048f);
                        g.FillPath(b, s1);
                        s1.Dispose();
                        GraphicsPath s2 = SparkPath(box, 0.150f, 0.735f, 0.033f);
                        g.FillPath(b, s2);
                        s2.Dispose();
                    }
                }
            }
            finally { g.SmoothingMode = old; }
        }

        // ------------------------------------------------------------------
        // 内嵌立绘（build.ps1 用 /resource 把 WhaleAvatar.png 打进 exe）
        // ------------------------------------------------------------------
        static Image _avatar;
        static bool _avatarTried;

        static Image Avatar()
        {
            if (_avatarTried) return _avatar;
            _avatarTried = true;
            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (System.IO.Stream s = asm.GetManifestResourceStream("WhaleAvatar.png"))
                {
                    if (s != null)
                    {
                        using (Image tmp = Image.FromStream(s)) _avatar = new Bitmap(tmp);
                    }
                }
            }
            catch { _avatar = null; }
            return _avatar;
        }

        // 等比铺满，超出部分裁掉（图是方的，头像框也差不多是方的）
        static void DrawArt(Graphics g, Image art, RectangleF box)
        {
            float scale = Math.Max(box.Width / art.Width, box.Height / art.Height);
            float w = art.Width * scale;
            float h = art.Height * scale;
            float x = box.X + (box.Width - w) / 2f;
            float y = box.Y + (box.Height - h) / 2f;

            InterpolationMode oldIm = g.InterpolationMode;
            PixelOffsetMode oldPo = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(art, new RectangleF(x, y, w, h));
            g.InterpolationMode = oldIm;
            g.PixelOffsetMode = oldPo;
        }

        // ------------------------------------------------------------------
        // 基础工具
        // ------------------------------------------------------------------
        static PointF P(RectangleF b, float x, float y)
        {
            return new PointF(b.X + x * b.Width, b.Y + y * b.Height);
        }

        static RectangleF Rect(RectangleF b, float x, float y, float w, float h)
        {
            return new RectangleF(b.X + x * b.Width, b.Y + y * b.Height, w * b.Width, h * b.Height);
        }

        static LinearGradientBrush Grad(RectangleF b, Color top, Color bottom)
        {
            return new LinearGradientBrush(
                new RectangleF(b.X, b.Y - b.Height * 0.05f, b.Width, b.Height * 1.1f), top, bottom, 90f);
        }

        static void Quad(GraphicsPath p, PointF a, PointF c, PointF d)
        {
            PointF c1 = new PointF(a.X + (c.X - a.X) * 2f / 3f, a.Y + (c.Y - a.Y) * 2f / 3f);
            PointF c2 = new PointF(d.X + (c.X - d.X) * 2f / 3f, d.Y + (c.Y - d.Y) * 2f / 3f);
            p.AddBezier(a, c1, c2, d);
        }

        public static GraphicsPath RoundPath(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        // ------------------------------------------------------------------
        // 头发零件
        // ------------------------------------------------------------------
        static GraphicsPath BackHair(RectangleF b)
        {
            GraphicsPath p = new GraphicsPath();
            p.StartFigure();
            p.AddBezier(P(b, 0.500f, 0.085f), P(b, 0.245f, 0.085f), P(b, 0.115f, 0.255f), P(b, 0.115f, 0.520f));
            p.AddBezier(P(b, 0.115f, 0.520f), P(b, 0.115f, 0.700f), P(b, 0.160f, 0.860f), P(b, 0.250f, 0.905f));
            p.AddBezier(P(b, 0.250f, 0.905f), P(b, 0.330f, 0.945f), P(b, 0.670f, 0.945f), P(b, 0.750f, 0.905f));
            p.AddBezier(P(b, 0.750f, 0.905f), P(b, 0.840f, 0.860f), P(b, 0.885f, 0.700f), P(b, 0.885f, 0.520f));
            p.AddBezier(P(b, 0.885f, 0.520f), P(b, 0.885f, 0.255f), P(b, 0.755f, 0.085f), P(b, 0.500f, 0.085f));
            p.CloseFigure();
            return p;
        }

        static void DrawEar(Graphics g, RectangleF b, float lw, bool right)
        {
            // 大肥鱼的鳍耳：比猫耳更圆润，朝外上方翘
            using (GraphicsPath p = Fin(b,
                new PointF(0.250f, 0.372f), new PointF(right ? 0.796f : 0.204f, 0.088f), new PointF(0.470f, 0.258f), right))
            {
                using (Brush bg = Grad(b, HairB, HairC)) g.FillPath(bg, p);
                using (Pen pen = new Pen(Line, lw * 1.1f)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, p); }
            }
            using (GraphicsPath p = Fin(b,
                new PointF(0.286f, 0.330f), new PointF(right ? 0.742f : 0.258f, 0.174f), new PointF(0.418f, 0.266f), right))
            {
                using (Brush ib = new SolidBrush(EarIn)) g.FillPath(ib, p);
            }
        }

        // 鳍：根部在内侧，尖端朝外上，外侧那条边稍微外鼓
        static GraphicsPath Fin(RectangleF b, PointF root, PointF tip, PointF bottom, bool mirror)
        {
            PointF a = P(b, root.X, root.Y);
            PointF t = P(b, tip.X, tip.Y);
            PointF c = P(b, bottom.X, bottom.Y);
            float bulge = (mirror ? 1f : -1f) * b.Width * 0.042f;

            GraphicsPath p = new GraphicsPath();
            p.StartFigure();
            p.AddBezier(a,
                new PointF(a.X + (t.X - a.X) * 0.35f + bulge, a.Y + (t.Y - a.Y) * 0.45f),
                new PointF(t.X + bulge * 0.55f, t.Y + (c.Y - t.Y) * 0.06f),
                t);
            p.AddBezier(t,
                new PointF(t.X + (c.X - t.X) * 0.55f, t.Y + (c.Y - t.Y) * 0.42f),
                new PointF(c.X + (t.X - c.X) * 0.28f, c.Y + (t.Y - c.Y) * 0.16f),
                c);
            p.CloseFigure();
            return p;
        }

        // 一缕头发：底边中心 (bx,by)，尖端 (tx,ty)
        static GraphicsPath Strand(RectangleF b, float bx, float by, float halfW, float tx, float ty, float bowL, float bowR)
        {
            PointF baseC = P(b, bx, by);
            PointF tip = P(b, tx, ty);
            PointF left = P(b, bx - halfW, by);
            PointF right = P(b, bx + halfW, by);
            float u = b.Width;

            GraphicsPath p = new GraphicsPath();
            p.StartFigure();
            Quad(p, left, new PointF(
                left.X + (tip.X - left.X) * 0.40f - bowL * u,
                left.Y + (tip.Y - left.Y) * 0.55f), tip);
            Quad(p, tip, new PointF(
                right.X + (tip.X - right.X) * 0.40f + bowR * u,
                right.Y + (tip.Y - right.Y) * 0.55f), right);
            p.AddLine(right, baseC);
            p.AddLine(baseC, left);
            p.CloseFigure();
            return p;
        }

        // 刘海：一整片，底部带 5 个发尖
        static GraphicsPath Fringe(RectangleF b)
        {
            GraphicsPath p = new GraphicsPath();
            p.StartFigure();
            p.AddBezier(P(b, 0.176f, 0.560f), P(b, 0.168f, 0.470f), P(b, 0.170f, 0.390f), P(b, 0.200f, 0.322f));
            p.AddBezier(P(b, 0.200f, 0.322f), P(b, 0.272f, 0.140f), P(b, 0.352f, 0.098f), P(b, 0.500f, 0.098f));
            p.AddBezier(P(b, 0.500f, 0.098f), P(b, 0.648f, 0.098f), P(b, 0.728f, 0.140f), P(b, 0.800f, 0.322f));
            p.AddBezier(P(b, 0.800f, 0.322f), P(b, 0.830f, 0.390f), P(b, 0.832f, 0.470f), P(b, 0.824f, 0.560f));

            // 底边发梢：右 → 左
            float[] tipX = { 0.824f, 0.666f, 0.500f, 0.334f, 0.176f };
            float[] tipY = { 0.540f, 0.470f, 0.448f, 0.470f, 0.540f };
            float[] valX = { 0.745f, 0.583f, 0.417f, 0.255f };
            float[] valY = { 0.406f, 0.402f, 0.402f, 0.406f };

            PointF cur = new PointF(b.X + tipX[0] * b.Width, b.Y + tipY[0] * b.Height);
            for (int i = 0; i < valX.Length; i++)
            {
                // 尖 → 谷（略微下垂，显得发丝成束）
                PointF v = P(b, valX[i], valY[i]);
                PointF mid = new PointF((cur.X + v.X) / 2f, (cur.Y + v.Y) / 2f + b.Height * 0.012f);
                Quad(p, cur, mid, v);
                // 谷 → 尖
                PointF t = P(b, tipX[i + 1], tipY[i + 1]);
                PointF mid2 = new PointF((v.X + t.X) / 2f, (v.Y + t.Y) / 2f + b.Height * 0.006f);
                Quad(p, v, mid2, t);
                cur = t;
            }
            p.CloseFigure();
            return p;
        }

        static void DrawFringe(Graphics g, RectangleF b, float lw, bool simple)
        {
            using (GraphicsPath p = Fringe(b))
            {
                using (Brush bg = Grad(b, HairHi, HairB)) g.FillPath(bg, p);
                using (Pen pen = new Pen(Color.FromArgb(225, Line), lw * 0.95f))
                {
                    pen.LineJoin = LineJoin.Round;
                    g.DrawPath(pen, p);
                }
            }

            // 发丝分缕
            using (Pen pen = new Pen(Color.FromArgb(simple ? 55 : 75, Line), lw * 0.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float[] xs = { 0.666f, 0.500f, 0.334f };
                float[] ys = { 0.470f, 0.452f, 0.470f };
                for (int i = 0; i < xs.Length; i++)
                {
                    using (GraphicsPath sp = new GraphicsPath())
                    {
                        sp.AddBezier(P(b, xs[i], 0.180f), P(b, xs[i] * 0.985f, 0.300f), P(b, xs[i], 0.400f), P(b, xs[i], ys[i]));
                        g.DrawPath(pen, sp);
                    }
                }
            }
        }

        static void DrawSideLock(Graphics g, RectangleF b, float lw, bool right)
        {
            GraphicsState st = null;
            if (right)
            {
                st = g.Save();
                g.TranslateTransform(b.X + b.Width, 0f);
                g.ScaleTransform(-1f, 1f);
                g.TranslateTransform(-b.X, 0f);
            }
            try
            {
                using (GraphicsPath p = Strand(b, 0.262f, 0.430f, 0.042f, 0.238f, 0.840f, 0.020f, 0.004f))
                {
                    using (Brush bg = Grad(b, HairA, HairB)) g.FillPath(bg, p);
                    using (Pen pen = new Pen(Color.FromArgb(190, Line), lw * 0.9f))
                    {
                        pen.LineJoin = LineJoin.Round;
                        g.DrawPath(pen, p);
                    }
                }
            }
            finally
            {
                if (st != null) g.Restore(st);
            }
        }

        // 发饰：小鲸鱼尾巴（大肥鱼的标志）
        static void DrawWhalePin(Graphics g, RectangleF b, float cx, float cy, float r, float lw)
        {
            PointF c = P(b, cx, cy);
            float rr = r * b.Width;

            using (GraphicsPath p = new GraphicsPath())
            {
                p.StartFigure();
                // 左尾鳍
                p.AddBezier(new PointF(c.X - rr * 0.05f, c.Y + rr * 0.20f),
                    new PointF(c.X - rr * 0.38f, c.Y - rr * 0.16f),
                    new PointF(c.X - rr * 0.94f, c.Y - rr * 0.26f),
                    new PointF(c.X - rr * 0.96f, c.Y - rr * 0.92f));
                p.AddBezier(new PointF(c.X - rr * 0.96f, c.Y - rr * 0.92f),
                    new PointF(c.X - rr * 0.52f, c.Y - rr * 0.74f),
                    new PointF(c.X - rr * 0.18f, c.Y - rr * 0.42f),
                    new PointF(c.X - rr * 0.02f, c.Y - rr * 0.06f));
                // 右尾鳍（镜像）
                p.AddBezier(new PointF(c.X + rr * 0.02f, c.Y - rr * 0.06f),
                    new PointF(c.X + rr * 0.18f, c.Y - rr * 0.42f),
                    new PointF(c.X + rr * 0.52f, c.Y - rr * 0.74f),
                    new PointF(c.X + rr * 0.96f, c.Y - rr * 0.92f));
                p.AddBezier(new PointF(c.X + rr * 0.96f, c.Y - rr * 0.92f),
                    new PointF(c.X + rr * 0.94f, c.Y - rr * 0.26f),
                    new PointF(c.X + rr * 0.38f, c.Y - rr * 0.16f),
                    new PointF(c.X + rr * 0.05f, c.Y + rr * 0.20f));
                p.CloseFigure();

                using (Brush bg = new SolidBrush(PinFill)) g.FillPath(bg, p);
                using (Pen pen = new Pen(PinLine, Math.Max(1f, lw * 0.9f)))
                {
                    pen.LineJoin = LineJoin.Round;
                    g.DrawPath(pen, p);
                }
            }
        }

        // 白色蕾丝头饰：贴着发际线的一条花边，下沿一排小蕾丝球
        static void DrawHeaddress(Graphics g, RectangleF b, float lw)
        {
            RectangleF arcBox = Rect(b, 0.186f, 0.146f, 0.628f, 0.330f);
            float cx = arcBox.X + arcBox.Width / 2f;
            float cy = arcBox.Y + arcBox.Height / 2f;
            float rx = arcBox.Width / 2f;
            float ry = arcBox.Height / 2f;

            using (Pen band = new Pen(LaceFill, b.Width * 0.050f))
            {
                band.StartCap = LineCap.Round;
                band.EndCap = LineCap.Round;
                g.DrawArc(band, arcBox, 202f, 136f);
            }
            using (Pen edge = new Pen(LaceLine, Math.Max(1f, b.Width * 0.008f)))
            {
                edge.StartCap = LineCap.Round;
                edge.EndCap = LineCap.Round;
                g.DrawArc(edge, arcBox, 202f, 136f);
            }

            const int n = 9;
            float bead = b.Width * 0.021f;
            for (int i = 0; i < n; i++)
            {
                double ang = (202.0 + 136.0 * i / (n - 1)) * Math.PI / 180.0;
                float x = cx + (float)Math.Cos(ang) * rx * 1.012f;
                float y = cy + (float)Math.Sin(ang) * ry * 1.012f;
                using (Brush bb = new SolidBrush(LaceFill))
                    g.FillEllipse(bb, x - bead, y - bead, bead * 2f, bead * 2f);
                using (Pen pp = new Pen(LaceLine, Math.Max(1f, b.Width * 0.006f)))
                    g.DrawEllipse(pp, x - bead, y - bead, bead * 2f, bead * 2f);
            }
        }

        static GraphicsPath SparkPath(RectangleF b, float cx, float cy, float r)
        {
            PointF c = P(b, cx, cy);
            float rr = r * b.Width;
            float k = rr * 0.26f;
            GraphicsPath p = new GraphicsPath();
            p.StartFigure();
            p.AddBezier(new PointF(c.X, c.Y - rr), new PointF(c.X + k, c.Y - k), new PointF(c.X + k, c.Y - k), new PointF(c.X + rr, c.Y));
            p.AddBezier(new PointF(c.X + rr, c.Y), new PointF(c.X + k, c.Y + k), new PointF(c.X + k, c.Y + k), new PointF(c.X, c.Y + rr));
            p.AddBezier(new PointF(c.X, c.Y + rr), new PointF(c.X - k, c.Y + k), new PointF(c.X - k, c.Y + k), new PointF(c.X - rr, c.Y));
            p.AddBezier(new PointF(c.X - rr, c.Y), new PointF(c.X - k, c.Y - k), new PointF(c.X - k, c.Y - k), new PointF(c.X, c.Y - rr));
            p.CloseFigure();
            return p;
        }

        // ------------------------------------------------------------------
        // 眼睛
        // ------------------------------------------------------------------
        static GraphicsPath EyeShape(RectangleF e)
        {
            PointF inner = new PointF(e.Left, e.Top + e.Height * 0.52f);
            PointF outer = new PointF(e.Right, e.Top + e.Height * 0.40f);
            PointF top = new PointF(e.Left + e.Width * 0.54f, e.Top);
            PointF bottom = new PointF(e.Left + e.Width * 0.46f, e.Bottom);

            GraphicsPath p = new GraphicsPath();
            p.StartFigure();
            Quad(p, inner, new PointF(e.Left + e.Width * 0.18f, e.Top - e.Height * 0.02f), top);
            Quad(p, top, new PointF(e.Left + e.Width * 0.82f, e.Top - e.Height * 0.01f), outer);
            Quad(p, outer, new PointF(e.Left + e.Width * 0.80f, e.Top + e.Height * 0.78f), bottom);
            Quad(p, bottom, new PointF(e.Left + e.Width * 0.20f, e.Top + e.Height * 0.86f), inner);
            p.CloseFigure();
            return p;
        }

        static GraphicsPath EyeTopArc(RectangleF e)
        {
            PointF inner = new PointF(e.Left, e.Top + e.Height * 0.52f);
            PointF outer = new PointF(e.Right, e.Top + e.Height * 0.40f);
            PointF top = new PointF(e.Left + e.Width * 0.54f, e.Top);

            GraphicsPath p = new GraphicsPath();
            p.StartFigure();
            Quad(p, inner, new PointF(e.Left + e.Width * 0.18f, e.Top - e.Height * 0.02f), top);
            Quad(p, top, new PointF(e.Left + e.Width * 0.82f, e.Top - e.Height * 0.01f), outer);
            return p;
        }

        static void DrawEye(Graphics g, RectangleF b, float cx, float cy, float w, float h, bool mirror, float lw, bool simple)
        {
            RectangleF e = new RectangleF(
                b.X + (cx - w / 2f) * b.Width, b.Y + (cy - h / 2f) * b.Height,
                w * b.Width, h * b.Height);

            using (GraphicsPath shape = EyeShape(e))
            {
                Region oldClip = g.Clip;
                g.SetClip(shape, CombineMode.Intersect);

                using (Brush wb = new SolidBrush(Color.FromArgb(255, 253, 254, 255)))
                    g.FillPath(wb, shape);

                // 虹膜（比眼睛大，被眼型裁掉，形成上浓下淡的二次元眼睛）
                RectangleF iris = new RectangleF(
                    e.Left + e.Width * 0.06f, e.Top - e.Height * 0.06f,
                    e.Width * 0.88f, e.Height * 1.16f);
                using (Brush ib = new LinearGradientBrush(
                    new RectangleF(iris.X, iris.Y, iris.Width, iris.Height), IrisTop, IrisBot, 90f))
                    g.FillEllipse(ib, iris);
                using (Brush mb = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                    g.FillEllipse(mb, new RectangleF(
                        iris.X + iris.Width * 0.10f, iris.Y + iris.Height * 0.46f,
                        iris.Width * 0.80f, iris.Height * 0.46f));

                // 瞳孔
                using (Brush pb = new SolidBrush(Color.FromArgb(240, 12, 30, 64)))
                    g.FillEllipse(pb, new RectangleF(
                        e.Left + e.Width * 0.38f, e.Top + e.Height * 0.26f,
                        e.Width * 0.24f, e.Height * 0.50f));

                // 高光
                using (Brush hb = new SolidBrush(White))
                {
                    g.FillEllipse(hb, new RectangleF(
                        e.Left + e.Width * 0.20f, e.Top + e.Height * 0.17f,
                        e.Width * 0.30f, e.Height * 0.26f));
                    if (!simple)
                    {
                        g.FillEllipse(hb, new RectangleF(
                            e.Left + e.Width * 0.62f, e.Top + e.Height * 0.62f,
                            e.Width * 0.17f, e.Height * 0.15f));
                    }
                }

                g.Clip = oldClip;
            }

            // 上眼线：上缘贴着眼的弧线，下缘向下鼓，形成中间粗两头细的睫毛
            using (GraphicsPath lash = LashShape(e))
            {
                using (Brush lb = new SolidBrush(EyeLine)) g.FillPath(lb, lash);
            }
            using (GraphicsPath shape = EyeShape(e))
            {
                using (Pen pen = new Pen(Color.FromArgb(simple ? 0 : 130, 96, 62, 104), Math.Max(1f, lw * 0.75f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, RectangleF.Inflate(e, -e.Width * 0.06f, -e.Height * 0.06f), 30f, 120f);
                }
            }

            // 外眼角的小睫毛
            using (Brush lb = new SolidBrush(EyeLine))
            {
                float dir = mirror ? -1f : 1f;
                float ox = mirror ? e.Left : e.Right;
                float oy = e.Top + e.Height * 0.36f;
                g.FillPolygon(lb, new PointF[] {
                    new PointF(ox, oy - e.Height * 0.04f),
                    new PointF(ox + dir * e.Width * 0.30f, oy - e.Height * 0.30f),
                    new PointF(ox + dir * e.Width * 0.18f, oy + e.Height * 0.06f) });
            }
        }

        // 睫毛：上缘沿眼型，下缘向下鼓
        static GraphicsPath LashShape(RectangleF e)
        {
            PointF inner = new PointF(e.Left, e.Top + e.Height * 0.52f);
            PointF outer = new PointF(e.Right, e.Top + e.Height * 0.40f);
            PointF top = new PointF(e.Left + e.Width * 0.54f, e.Top);

            GraphicsPath p = new GraphicsPath();
            p.StartFigure();
            Quad(p, inner, new PointF(e.Left + e.Width * 0.18f, e.Top - e.Height * 0.02f), top);
            Quad(p, top, new PointF(e.Left + e.Width * 0.82f, e.Top - e.Height * 0.01f), outer);
            p.AddBezier(outer,
                new PointF(e.Left + e.Width * 0.84f, e.Top + e.Height * 0.19f),
                new PointF(e.Left + e.Width * 0.30f, e.Top + e.Height * 0.17f),
                inner);
            p.CloseFigure();
            return p;
        }
    }
}
