// 主窗口：二次元风格自绘界面（渐变标题栏 + 看板娘 + 状态卡片 + 日志面板）
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QQBotLauncher
{
    internal class MainForm : Form
    {
        private readonly BotEnvironment _env;
        private HeaderControl _header;
        private LoginBanner _banner;
        private readonly Dictionary<string, StatusCard> _cards = new Dictionary<string, StatusCard>();
        private TextBox _logBox;
        private AnimeButton _btnStart, _btnStop, _btnRestart, _btnPanel, _btnLogs;
        private Timer _statusTimer, _pulseTimer;
        private bool _busy;
        private bool _refreshing;
        private float _pulse;
        private string _hint = "双击卡片可以直接打开对应面板";
        private readonly List<string> _earlyLog = new List<string>();

        private const int W = 980;
        private const int H = 700;

        public MainForm()
        {
            _env = new BotEnvironment(Program.AppDir, AppendLog);
            _env.Detect();
            BuildUi();
            foreach (string s in _earlyLog) AppendLog(s);
            _earlyLog.Clear();
            AppendLog("自动识别完成：QQ=" + (_env.QQExe ?? "未找到") + "，SnowLuma=" + (_env.SnowDir ?? "未找到"));
            if (_env.SnowDir == null || _env.AstrDir == null)
                AppendLog("有程序没找到：可以在 config.ini 里填路径，或者把它们解压到本程序旁边。");

            _statusTimer = new Timer();
            _statusTimer.Interval = 2500;
            _statusTimer.Tick += delegate { RefreshStatusAsync(); };
            _statusTimer.Start();

            _pulseTimer = new Timer();
            _pulseTimer.Interval = 90;
            _pulseTimer.Tick += delegate
            {
                _pulse += 0.28f;
                foreach (StatusCard c in _cards.Values)
                {
                    if (c.Running) { c.Pulse = _pulse; c.Invalidate(); }
                }
            };
            _pulseTimer.Start();

            Shown += delegate { RefreshStatusAsync(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return cp;
            }
        }

        // ------------------------------------------------------------------ 布局
        private void BuildUi()
        {
            Text = "大肥鱼启动器";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(W, H);
            MinimumSize = new Size(W, H);
            MaximumSize = new Size(W, H);
            DoubleBuffered = true;
            Font = Draw.F(9f, FontStyle.Regular);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            _header = new HeaderControl();
            _header.SetBounds(0, 0, W, 92);
            _header.Title = "大肥鱼启动器";
            _header.Subtitle = "SnowLuma · wb2api · AstrBot　·　蓝色大肥鱼主题";
            _header.CloseClicked += delegate { Close(); };
            _header.MinimizeClicked += delegate { WindowState = FormWindowState.Minimized; };
            _header.SettingsClicked += delegate { OpenSettings(); };
            Controls.Add(_header);

            _banner = new LoginBanner();
            _banner.SetBounds(24, 104, W - 48, 52);
            Controls.Add(_banner);

            int y = 200;
            IconKind[] fallbackIcons = new IconKind[] { IconKind.Chat, IconKind.Globe, IconKind.Sparkle, IconKind.Gear };
            Color[] fallbackAccents = new Color[] { Palette.Sky, Palette.Lagoon, Palette.Sea, Palette.Mint };
            int idx = 0;
            foreach (Comp c in _env.Components)
            {
                StatusCard card = new StatusCard();
                card.SetBounds(24, y, 372, 84);
                card.Title = ShortName(c.Name);
                card.Icon = IconOf(c.Key, fallbackIcons[Math.Min(idx, 3)]);
                card.Accent = AccentOf(c.Key, fallbackAccents[Math.Min(idx, 3)]);
                card.Detail = "等待检测…";
                string url = c.Url;
                card.DoubleClick += delegate
                {
                    if (!string.IsNullOrEmpty(url))
                    {
                        try { Process.Start(url); } catch { }
                    }
                };
                Controls.Add(card);
                _cards[c.Key] = card;
                y += 96;
                idx++;
            }

            _logBox = new TextBox();
            _logBox.Multiline = true;
            _logBox.ReadOnly = true;
            _logBox.ScrollBars = ScrollBars.Vertical;
            _logBox.WordWrap = true;
            _logBox.BorderStyle = BorderStyle.None;
            _logBox.BackColor = Palette.LogBg;
            _logBox.ForeColor = Palette.LogText;
            _logBox.Font = new Font("Consolas", 9f);
            _logBox.SetBounds(428, 214, 512, 380);
            _logBox.TabStop = false;
            Controls.Add(_logBox);

            _btnStart = MakeButton("启动", IconKind.Play, 24, 200, true, Palette.Sea, delegate { StartAll(); });
            _btnStop = MakeButton("停止", IconKind.Stop, 238, 150, false, Palette.Coral, delegate { StopAll(); });
            _btnRestart = MakeButton("重启", IconKind.Restart, 402, 150, false, Palette.Amber, delegate { RestartAll(); });
            _btnPanel = MakeButton("打开面板", IconKind.Globe, 566, 190, false, Palette.Lagoon, delegate { OpenPanel(); });
            _btnLogs = MakeButton("日志", IconKind.Folder, 770, 186, false, Palette.Sky, delegate { OpenFolder(Path.Combine(Program.AppDir, "logs")); });
        }

        private AnimeButton MakeButton(string text, IconKind icon, int x, int w, bool primary, Color accent, EventHandler click)
        {
            AnimeButton b = new AnimeButton();
            b.Text = text;
            b.Icon = icon;
            b.Primary = primary;
            b.Accent = accent;
            b.SetBounds(x, 628, w, 44);
            b.Click += click;
            Controls.Add(b);
            return b;
        }

        // 名字太长时去掉括号里的说明，避免被截断
        private static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            int cut = name.IndexOf('（');
            if (cut > 0 && name.Length > 10) return name.Substring(0, cut);
            return name;
        }

        private static IconKind IconOf(string key, IconKind fallback)
        {
            if (key == "qq") return IconKind.Chat;
            if (key == "snowluma") return IconKind.Sparkle;
            if (key == "wb2api") return IconKind.Globe;
            if (key == "astrbot") return IconKind.Gear;
            return fallback;
        }

        private static Color AccentOf(string key, Color fallback)
        {
            if (key == "qq") return Palette.Sky;
            if (key == "snowluma") return Palette.Sea;
            if (key == "wb2api") return Palette.Lagoon;
            if (key == "astrbot") return Palette.Mint;
            return fallback;
        }

        // ------------------------------------------------------------------ 背景
        public void PaintBackdrop(Graphics g)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF box = new RectangleF(0, 0, Width, Height);
            using (LinearGradientBrush b = new LinearGradientBrush(
                new RectangleF(0, 90, Width, Height - 90),
                Color.FromArgb(255, 246, 252, 255), Color.FromArgb(255, 226, 243, 253), 90f))
                g.FillRectangle(b, new RectangleF(0, 90, Width, Height - 90));

            // 柔和的装饰色块（浅水蓝）
            using (Brush b = new SolidBrush(Color.FromArgb(26, 118, 208, 246)))
                g.FillEllipse(b, new RectangleF(Width - 260, 120, 300, 300));
            using (Brush b = new SolidBrush(Color.FromArgb(22, 88, 136, 210)))
                g.FillEllipse(b, new RectangleF(-120, 420, 320, 320));
            using (Brush b = new SolidBrush(Color.FromArgb(20, 148, 224, 250)))
                g.FillEllipse(b, new RectangleF(Width - 190, 560, 220, 220));

            // 小气泡
            Random rnd = new Random(7);
            for (int i = 0; i < 26; i++)
            {
                float x = (float)rnd.NextDouble() * Width;
                float y = 110 + (float)rnd.NextDouble() * (Height - 130);
                float s = 1.5f + (float)rnd.NextDouble() * 2.5f;
                using (Brush b = new SolidBrush(Color.FromArgb(46, 120, 198, 240)))
                    g.FillEllipse(b, x, y, s, s);
            }
            g.SmoothingMode = old;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            PaintBackdrop(g);

            // 日志卡片
            RectangleF logCard = new RectangleF(412, 168, 544, 444);
            Draw.Card(g, logCard, 20f, Palette.LogBg, Color.FromArgb(80, 255, 255, 255), true);
            using (Brush b = new SolidBrush(Color.FromArgb(255, 255, 255, 255)))
                g.FillEllipse(b, 436f, 190f, 8f, 8f);
            Draw.String(g, "运行日志", Draw.F(10.5f, FontStyle.Bold), Color.FromArgb(255, 232, 248, 255),
                new RectangleF(452, 178f, 200f, 26f), ContentAlignment.MiddleLeft);
            Draw.String(g, "双击下面的卡片可以直接打开对应面板",
                Draw.F(8f, FontStyle.Regular), Color.FromArgb(150, 175, 210, 240),
                new RectangleF(452, 182f, 480f, 20f), ContentAlignment.MiddleRight);

            // 左边分区标题
            Draw.Icon(g, IconKind.Sparkle, new RectangleF(24, 176, 16, 16), Palette.Sea, 0.2f);
            Draw.String(g, "组件状态", Draw.F(11f, FontStyle.Bold), Palette.Ink,
                new RectangleF(46, 166, 200, 28), ContentAlignment.MiddleLeft);

            Draw.String(g, _hint, Draw.F(8f, FontStyle.Regular), Palette.InkSoft,
                new RectangleF(24, 582, 372, 22), ContentAlignment.MiddleLeft);

            // 外边框
            using (Pen pen = new Pen(Color.FromArgb(255, 204, 233, 250), 1.4f))
            using (GraphicsPath p = Draw.Round(new RectangleF(0.7f, 0.7f, Width - 1.4f, Height - 1.4f), 22f))
                g.DrawPath(pen, p);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            try
            {
                using (GraphicsPath p = Draw.Round(new RectangleF(0, 0, Width, Height), 22f))
                    Region = new Region(p);
            }
            catch { }
        }

        // ------------------------------------------------------------------ 日志
        public void AppendLog(string line)
        {
            if (line == null) return;
            if (InvokeRequired) { try { BeginInvoke(new Action<string>(AppendLog), line); } catch { } return; }
            if (_logBox == null) { _earlyLog.Add(line); return; }
            string text = DateTime.Now.ToString("HH:mm:ss") + "  " + line;
            _logBox.AppendText(text + Environment.NewLine);
            if (_logBox.Lines.Length > 1500)
            {
                _logBox.Text = string.Join(Environment.NewLine, _logBox.Lines.Skip(_logBox.Lines.Length - 800).ToArray());
                _logBox.SelectionStart = _logBox.TextLength;
                _logBox.ScrollToCaret();
            }
        }

        // ------------------------------------------------------------------ 状态
        private void RefreshStatusAsync()
        {
            if (_refreshing) return;
            _refreshing = true;
            Task.Factory.StartNew(delegate
            {
                List<BotEnvironment.Row> rows = null;
                string uin = "";
                string err = null;
                try
                {
                    rows = _env.CollectStatus();
                    uin = _env.GetLoginUin();
                }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        _refreshing = false;
                        if (err != null) { AppendLog("刷新状态失败：" + err); return; }
                        ApplyStatus(rows, uin);
                    }));
                }
                catch { _refreshing = false; }
            });
        }

        private void ApplyStatus(List<BotEnvironment.Row> rows, string uin)
        {
            bool onebotUp = false;
            bool qqRunning = false;
            foreach (BotEnvironment.Row r in rows)
            {
                StatusCard card;
                if (_cards.TryGetValue(r.Key, out card))
                {
                    card.Running = r.Running;
                    string detail = r.Owner;
                    if (detail == "-") detail = "没有进程";
                    if (r.Ports != "-") detail += "　·　" + r.Ports;
                    card.Detail = detail;
                    card.Invalidate();
                }
                if (r.Key == "snowluma" && (r.Ports.Contains("3001 开") || r.Ports.Contains("3000 开"))) onebotUp = true;
                if (r.Key == "qq" && r.Running) qqRunning = true;
            }

            if (onebotUp)
            {
                _banner.State = 1;
                _banner.Message = uin.Length > 0 ? "QQ 已登录（" + uin + "），OneBot 已上线，机器人正在工作" : "QQ 已登录，OneBot 已上线，机器人正在工作";
            }
            else if (qqRunning)
            {
                _banner.State = 2;
                _banner.Message = "QQ 窗口开着，还没登录 —— 扫码/输密码后会自动挂上";
            }
            else
            {
                _banner.State = 2;
                _banner.Message = "QQ 没开 —— 点「启动」会把 QQ、SnowLuma、wb2api、AstrBot 依次拉起来";
            }
            _banner.Invalidate();
        }

        // ------------------------------------------------------------------ 操作
        private void SetBusy(bool busy)
        {
            _busy = busy;
            _btnStart.Enabled = !busy;
            _btnStop.Enabled = !busy;
            _btnRestart.Enabled = !busy;
            _hint = busy ? "正在处理，请稍等…" : "双击卡片可以直接打开对应面板";
            Invalidate();
        }

        private void StartAll()
        {
            if (_busy) return;
            SetBusy(true);
            AppendLog("=== 启动 ===");
            Task.Factory.StartNew(delegate
            {
                try { _env.StartAll(AppendLog, null); }
                catch (Exception ex) { AppendLog("启动出错：" + ex.Message); }
                finally
                {
                    try { BeginInvoke(new Action(delegate { SetBusy(false); RefreshStatusAsync(); })); }
                    catch { SetBusy(false); }
                }
            });
        }

        private void StopAll()
        {
            if (_busy) return;
            if (MessageBox.Show(this, "停止 AstrBot、SnowLuma、wb2api。\r\n\r\nQQ 默认不关（关了你的号会掉线）。要继续吗？",
                "停止机器人", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            SetBusy(true);
            AppendLog("=== 停止 ===");
            Task.Factory.StartNew(delegate
            {
                try { _env.StopAll(AppendLog, false); }
                catch (Exception ex) { AppendLog("停止出错：" + ex.Message); }
                finally
                {
                    try { BeginInvoke(new Action(delegate { SetBusy(false); RefreshStatusAsync(); })); }
                    catch { SetBusy(false); }
                }
            });
        }

        private void RestartAll()
        {
            if (_busy) return;
            if (MessageBox.Show(this, "先停后起。QQ 不动。继续吗？", "重启机器人",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            SetBusy(true);
            AppendLog("=== 重启 ===");
            Task.Factory.StartNew(delegate
            {
                try
                {
                    _env.StopAll(AppendLog, false);
                    System.Threading.Thread.Sleep(2000);
                    _env.StartAll(AppendLog, null);
                }
                catch (Exception ex) { AppendLog("重启出错：" + ex.Message); }
                finally
                {
                    try { BeginInvoke(new Action(delegate { SetBusy(false); RefreshStatusAsync(); })); }
                    catch { SetBusy(false); }
                }
            });
        }

        private void OpenPanel()
        {
            try
            {
                int port = _env.GetSnowLumaWebPort();
                Process.Start("http://127.0.0.1:" + port + "/");
            }
            catch (Exception ex) { AppendLog("打不开面板：" + ex.Message); }
        }

        private void OpenFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch { }
        }

        // 设置窗口：只用来改 config.ini 里的四个路径
        private void OpenSettings()
        {
            bool saved;
            try
            {
                using (SettingsForm f = new SettingsForm(_env))
                    saved = f.ShowDialog(this) == DialogResult.OK;
            }
            finally
            {
                // 关掉设置后别让焦点停在按钮上，否则再按一下回车又会弹一次
                try { ActiveControl = null; } catch { }
            }
            if (!saved) return;

            // 重新识别可能要扫盘，放到后台跑，别把窗口卡住
            SetBusy(true);
            AppendLog("设置已保存，正在重新识别…");
            Task.Factory.StartNew(delegate
            {
                try { _env.Detect(); }
                catch (Exception ex) { AppendLog("重新识别出错：" + ex.Message); }
                finally
                {
                    try
                    {
                        BeginInvoke(new Action(delegate
                        {
                            SetBusy(false);
                            AppendLog("重新识别完成。");
                            RefreshStatusAsync();
                        }));
                    }
                    catch { try { SetBusy(false); } catch { } }
                }
            });
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_statusTimer != null) _statusTimer.Stop();
            if (_pulseTimer != null) _pulseTimer.Stop();
            base.OnFormClosing(e);
        }
    }

    // ----------------------------------------------------------------------
    // QQ 登录状态横幅
    // ----------------------------------------------------------------------
    internal class LoginBanner : GlassControl
    {
        public string Message = "正在检查 QQ 登录状态…";
        private int _state;

        // 0 未知 / 1 正常 / 2 提醒
        public int State
        {
            get { return _state; }
            set { _state = value; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            PaintBackdrop(g);

            Color accent = _state == 1 ? Palette.Mint : (_state == 2 ? Palette.Amber : Palette.Sky);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Draw.Card(g, r, 16f, Color.FromArgb(250, 255, 255, 255), Palette.CardBorder, true);

            // 左侧色条
            using (GraphicsPath p = Draw.Round(new RectangleF(10f, 11f, 5f, Height - 22f), 3f))
            using (Brush b = new SolidBrush(accent))
                g.FillPath(b, p);

            // 图标
            RectangleF ib = new RectangleF(26f, (Height - 28f) / 2f, 28f, 28f);
            using (GraphicsPath p = Draw.Round(ib, 9f))
            using (Brush b = new SolidBrush(Color.FromArgb(36, accent)))
                g.FillPath(b, p);
            Draw.Icon(g, IconKind.Chat, new RectangleF(ib.X + 6f, ib.Y + 6f, 16f, 16f), accent, 0.16f);

            Draw.String(g, Message, Draw.F(9.5f, FontStyle.Bold), Palette.Ink,
                new RectangleF(68f, 0f, Width - 88f, Height), ContentAlignment.MiddleLeft);
        }
    }
}
