// 大肥鱼启动器（桌面版）
// 单文件 WinForms，.NET Framework 4.x 自带组件，无需安装运行时。
// 编译：csc.exe /target:winexe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll Launcher.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QQBotLauncher
{
    internal static class Program
    {
        public static string AppDir;

        [STAThread]
        private static void Main(string[] args)
        {
            AppDir = Path.GetDirectoryName(Application.ExecutablePath);
            foreach (string a in args)
            {
                if (string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase))
                {
                    SelfTest();
                    return;
                }
                if (a.StartsWith("--cmd=", StringComparison.OrdinalIgnoreCase))
                {
                    RunCommand(a.Substring(6).ToLowerInvariant());
                    return;
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e) { LogCrash(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { LogCrash(e.ExceptionObject as Exception); };
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                LogCrash(ex);
                throw;
            }
        }

        // 界面初始化时的异常写一份到 %TEMP%，方便排查（正式使用时基本不会触发）
        private static void LogCrash(Exception ex)
        {
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "qqbot-crash.txt"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + ex, new UTF8Encoding(false));
            }
            catch { }
        }

        // 无窗口跑一次操作，把过程写到 %TEMP%\qqbot-cmd.txt（调试和自检用）
        private static void RunCommand(string cmd)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                BotEnvironment env = new BotEnvironment(AppDir, delegate(string m) { sb.AppendLine(m); });
                env.Detect(false);
                if (cmd == "start") env.StartAll(delegate(string m) { sb.AppendLine(m); }, null);
                else if (cmd == "stop") env.StopAll(delegate(string m) { sb.AppendLine(m); }, false);
                else if (cmd == "stopqq") env.StopAll(delegate(string m) { sb.AppendLine(m); }, true);
                else if (cmd == "restart")
                {
                    env.StopAll(delegate(string m) { sb.AppendLine(m); }, false);
                    System.Threading.Thread.Sleep(2000);
                    env.StartAll(delegate(string m) { sb.AppendLine(m); }, null);
                }
                foreach (BotEnvironment.Row r in env.CollectStatus())
                    sb.AppendLine(string.Format("[{0}] {1} {2}", r.Running ? "运行中" : "未运行", r.Name, r.Ports));
            }
            catch (Exception ex)
            {
                sb.AppendLine("ERROR: " + ex);
            }
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "qqbot-cmd.txt"), sb.ToString(), new UTF8Encoding(false));
        }

        private static void SelfTest()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                BotEnvironment env = new BotEnvironment(AppDir, delegate(string m) { sb.AppendLine(m); });
                env.Detect(false);
                sb.AppendLine("QQ      = " + (env.QQExe == null ? "(not found)" : env.QQExe));
                sb.AppendLine("AstrBot = " + (env.AstrDir ?? "(not found)"));
                sb.AppendLine("SnowLuma= " + (env.SnowDir ?? "(not found)"));
                sb.AppendLine("wb2api  = " + (env.WbDir ?? "(not found)"));
                sb.AppendLine("WebPort = " + env.GetSnowLumaWebPort());
                sb.AppendLine("UIN     = " + env.GetLoginUin());
                sb.AppendLine("--- status ---");
                foreach (BotEnvironment.Row r in env.CollectStatus())
                {
                    sb.AppendLine(string.Format("{0,-28} {1,-6} {2,-16} {3}", r.Name, r.Running ? "运行中" : "未运行", r.Owner, r.Ports));
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("ERROR: " + ex);
            }
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "qqbot-selftest.txt"), sb.ToString(), new UTF8Encoding(false));
        }
    }

    // ------------------------------------------------------------------ 组件 / 进程信息
    internal class Comp
    {
        public string Key;
        public string Name;
        public string Kind;          // Port | Process
        public string Dir;
        public string Exe;
        public string[] Args = new string[0];
        public string ProcessName;
        public string CmdLike;       // 命令行需匹配（SnowLuma 用它区分父子）
        public bool Dedupe;
        public int ReadyPort;
        public int[] Ports = new int[0];
        public string Url;
        public bool Optional;
        public int TimeoutSec = 60;
    }

    internal class ProcInfo
    {
        public int Pid;
        public int ParentPid;
        public string Name;
        public string Path;
        public string CmdLine;
        public DateTime Created;
    }

    // ------------------------------------------------------------------ 核心逻辑（GUI 和自检共用）
    internal class BotEnvironment
    {
        private readonly string _appDir;
        private readonly Action<string> _log;

        public string QQExe;
        public string QQDir;
        public string AstrDir;
        public string SnowDir;
        public string WbDir;
        public List<Comp> Components = new List<Comp>();
        public string ConfigPath;

        public string QQPathSetting = "";
        public string AstrSetting = "";
        public string SnowSetting = "";
        public string WbSetting = "";

        private string _logDir;
        private readonly object _sync = new object();

        public BotEnvironment(string appDir, Action<string> log)
        {
            _appDir = appDir;
            _log = log;
            _logDir = Path.Combine(appDir, "logs");
            try { if (!Directory.Exists(_logDir)) Directory.CreateDirectory(_logDir); }
            catch { }
            ConfigPath = Path.Combine(appDir, "config.ini");
            EnsureConfig();
            LoadConfig();
        }

        private const string ConfigHeader =
            "# 大肥鱼启动器：留空 = 自动查找\n" +
            "# 也可以写死路径（绝对路径，或相对本程序的相对路径），例如：\n" +
            "# QQPath=D:\\QQ\\QQ.exe";

        // ---------------- 配置 ----------------
        // 第一次运行时放一份带注释的模板，路径不对时直接改这个文件
        private void EnsureConfig()
        {
            try
            {
                if (File.Exists(ConfigPath)) return;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(ConfigHeader);
                sb.AppendLine("QQPath=");
                sb.AppendLine("AstrBotDir=");
                sb.AppendLine("SnowLumaDir=");
                sb.AppendLine("Wb2apiDir=");
                File.WriteAllText(ConfigPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;
                foreach (string line in File.ReadAllLines(ConfigPath, Encoding.UTF8))
                {
                    string s = line.Trim();
                    if (s.Length == 0 || s.StartsWith("#") || s.StartsWith(";")) continue;
                    int i = s.IndexOf('=');
                    if (i <= 0) continue;
                    string k = s.Substring(0, i).Trim().ToLowerInvariant();
                    string v = s.Substring(i + 1).Trim();
                    if (k == "qqpath") QQPathSetting = v;
                    else if (k == "astrbotdir") AstrSetting = v;
                    else if (k == "snowlumadir") SnowSetting = v;
                    else if (k == "wb2apidir") WbSetting = v;
                }
            }
            catch (Exception ex) { _log("config.ini 读取失败：" + ex.Message); }
        }

        // 设置窗口点「保存」时写回文件；没填的项留空 = 下次启动自动查找
        public void SaveConfig()
        {
            SaveConfig(QQPathSetting, AstrSetting, SnowSetting, WbSetting);
        }

        // 先写临时文件再替换，写到一半断电也不会把配置写坏
        public void SaveConfig(string qq, string astr, string snow, string wb)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(ConfigHeader);
            sb.AppendLine("QQPath=" + (qq ?? ""));
            sb.AppendLine("AstrBotDir=" + (astr ?? ""));
            sb.AppendLine("SnowLumaDir=" + (snow ?? ""));
            sb.AppendLine("Wb2apiDir=" + (wb ?? ""));
            string tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
            if (File.Exists(ConfigPath)) File.Replace(tmp, ConfigPath, null);
            else File.Move(tmp, ConfigPath);
        }

        private static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }
        private string ResolvePath(string p)
        {
            if (string.IsNullOrEmpty(p)) return null;
            string full = Path.IsPathRooted(p) ? p : Path.Combine(_appDir, p);
            try { if (File.Exists(full) || Directory.Exists(full)) return Path.GetFullPath(full); }
            catch { }
            return null;
        }

        // ---------------- 目录识别 ----------------
        private static bool HasMarkers(string dir, string[] files, string[] dirs)
        {
            try
            {
                foreach (string f in files) if (!File.Exists(Path.Combine(dir, f))) return false;
                foreach (string d in dirs) if (!Directory.Exists(Path.Combine(dir, d))) return false;
                return true;
            }
            catch { return false; }
        }

        private string FindDir(string what, string[] files, string[] dirs, string configured)
        {
            if (!string.IsNullOrEmpty(configured))
            {
                string p = ResolvePath(configured);
                if (p != null && HasMarkers(p, files, dirs)) return p;
                _log("config.ini 里写的 " + what + " 路径不可用（" + configured + "），改成自动查找");
            }

            List<string> roots = new List<string>();
            roots.Add(_appDir);
            string a = Path.GetDirectoryName(_appDir);
            if (a != null) roots.Add(a);
            if (a != null) { string b = Path.GetDirectoryName(a); if (b != null) roots.Add(b); }

            List<string> hits = new List<string>();
            foreach (string root in roots.Distinct())
            {
                if (!Directory.Exists(root)) continue;
                if (HasMarkers(root, files, dirs)) hits.Add(root);
                string[] subs;
                try { subs = Directory.GetDirectories(root); }
                catch { continue; }
                foreach (string sub in subs)
                {
                    if (HasMarkers(sub, files, dirs)) hits.Add(sub);
                }
            }
            if (hits.Count == 0) return null;
            hits.Sort(delegate(string x, string y)
            {
                DateTime tx = Directory.GetLastWriteTime(x);
                DateTime ty = Directory.GetLastWriteTime(y);
                return ty.CompareTo(tx);
            });
            if (hits.Count > 1) _log("找到 " + hits.Count + " 个可能的 " + what + "，用最新的：" + Path.GetFileName(hits[0]));
            return hits[0];
        }

        // ---------------- QQ 定位 ----------------
        private string QQFromUninstall()
        {
            string[] keys = new string[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            foreach (RegistryKey hive in new RegistryKey[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (string keyPath in keys)
                {
                    try
                    {
                        using (RegistryKey baseKey = hive.OpenSubKey(keyPath))
                        {
                            if (baseKey == null) continue;
                            foreach (string sub in baseKey.GetSubKeyNames())
                            {
                                using (RegistryKey k = baseKey.OpenSubKey(sub))
                                {
                                    if (k == null) continue;
                                    string display = k.GetValue("DisplayName") as string;
                                    if (display == null || !display.StartsWith("QQ", StringComparison.OrdinalIgnoreCase)) continue;

                                    string icon = k.GetValue("DisplayIcon") as string;
                                    if (!string.IsNullOrEmpty(icon))
                                    {
                                        string path = icon.Split(',')[0].Trim().Trim('"');
                                        if (path.EndsWith("QQ.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path)) return path;
                                    }
                                    string loc = k.GetValue("InstallLocation") as string;
                                    if (!string.IsNullOrEmpty(loc))
                                    {
                                        foreach (string rel in new string[] { "QQ.exe", @"..\QQ.exe", @"Bin\QQ.exe" })
                                        {
                                            try
                                            {
                                                string p = Path.GetFullPath(Path.Combine(loc.Trim().Trim('"'), rel));
                                                if (File.Exists(p) && string.Equals(Path.GetFileName(p), "QQ.exe", StringComparison.OrdinalIgnoreCase)) return p;
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            return null;
        }

        private string QQFromMuiCache()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache"))
                {
                    if (k == null) return null;
                    foreach (string name in k.GetValueNames())
                    {
                        if (!name.EndsWith("QQ.exe.FriendlyAppName", StringComparison.OrdinalIgnoreCase)) continue;
                        string candidate = name.Substring(0, name.Length - ".FriendlyAppName".Length);
                        if (File.Exists(candidate) && string.Equals(Path.GetFileName(candidate), "QQ.exe", StringComparison.OrdinalIgnoreCase))
                            return candidate;
                    }
                }
            }
            catch { }
            return null;
        }

        private string QQFromCommonPaths()
        {
            List<string> list = new List<string>();
            list.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Tencent\QQNT\QQ.exe"));
            list.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Tencent\QQNT\QQ.exe"));
            list.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Tencent\QQNT\QQ.exe"));
            list.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Tencent\QQNT\QQ.exe"));
            list.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Tencent\QQNT\QQ.exe"));
            string[] rels = new string[] { @"QQ\QQ.exe", @"Tencent\QQNT\QQ.exe", @"Program Files\Tencent\QQNT\QQ.exe", @"Program Files (x86)\Tencent\QQNT\QQ.exe", @"QQNT\QQ.exe", @"软件\QQ\QQ.exe" };
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType != DriveType.Fixed) continue;
                    foreach (string rel in rels) list.Add(Path.Combine(d.RootDirectory.FullName, rel));
                }
                catch { }
            }
            foreach (string p in list)
            {
                try { if (File.Exists(p)) return Path.GetFullPath(p); }
                catch { }
            }
            return null;
        }

        private string QQFromShallowScan()
        {
            DateTime deadline = DateTime.Now.AddSeconds(10);
            string[] skip = new string[] { "Windows", "Windows.old", "ProgramData", "$Recycle.Bin", "System Volume Information", "AppData" };
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try { if (d.DriveType != DriveType.Fixed) continue; }
                catch { continue; }
                string[] lv1;
                try { lv1 = Directory.GetDirectories(d.RootDirectory.FullName); }
                catch { continue; }
                foreach (string d1 in lv1)
                {
                    if (DateTime.Now > deadline) return null;
                    if (skip.Contains(Path.GetFileName(d1))) continue;
                    string hit = Path.Combine(d1, "QQ.exe");
                    if (File.Exists(hit)) return hit;
                    string[] lv2;
                    try { lv2 = Directory.GetDirectories(d1); }
                    catch { continue; }
                    if (lv2.Length > 300) continue;
                    foreach (string d2 in lv2)
                    {
                        if (DateTime.Now > deadline) return null;
                        string hit2 = Path.Combine(d2, "QQ.exe");
                        if (File.Exists(hit2)) return hit2;
                    }
                }
            }
            return null;
        }

        private string FindQQ()
        {
            if (!string.IsNullOrEmpty(QQPathSetting))
            {
                string p = ResolvePath(QQPathSetting);
                if (p != null && string.Equals(Path.GetFileName(p), "QQ.exe", StringComparison.OrdinalIgnoreCase)) return p;
                _log("config.ini 里写的 QQ 路径不可用（" + QQPathSetting + "），改成自动查找");
            }
            string found = QQFromUninstall();
            if (found == null) found = QQFromMuiCache();
            if (found == null) found = QQFromCommonPaths();
            if (found == null) found = QQFromShallowScan();
            return found;
        }

        // ---------------- 检测 ----------------
        public void Detect(bool writeBack = true)
        {
            AstrDir = FindDir("AstrBot", new string[] { "AstrBot.exe" }, new string[0], AstrSetting);
            SnowDir = FindDir("SnowLuma", new string[] { "index.mjs", "node.exe" }, new string[] { "native" }, SnowSetting);
            WbDir = FindDir("wb2api", new string[] { "wb2api.exe" }, new string[0], WbSetting);
            QQExe = FindQQ();
            QQDir = QQExe == null ? null : Path.GetDirectoryName(QQExe);
            lock (_sync) { BuildComponents(); }
            if (writeBack) SaveDetected();
        }

        // 识别到什么就记到什么：下次直接按记下的路径用，程序挪了位置会自动退回自动查找
        private void SaveDetected()
        {
            bool changed = false;
            if (QQExe != null && !SamePath(QQPathSetting, QQExe)) { QQPathSetting = QQExe; changed = true; }
            if (AstrDir != null && !SamePath(AstrSetting, AstrDir)) { AstrSetting = AstrDir; changed = true; }
            if (SnowDir != null && !SamePath(SnowSetting, SnowDir)) { SnowSetting = SnowDir; changed = true; }
            if (WbDir != null && !SamePath(WbSetting, WbDir)) { WbSetting = WbDir; changed = true; }
            if (!changed) return;
            try { SaveConfig(); } catch { }
        }

        private void BuildComponents()
        {
            Components.Clear();

            Comp qq = new Comp();
            qq.Key = "qq"; qq.Name = "QQ 客户端（登录用）"; qq.Kind = "Process";
            qq.Dir = QQDir; qq.Exe = QQExe; qq.ProcessName = "QQ.exe"; qq.Optional = true;
            qq.TimeoutSec = 40;
            Components.Add(qq);

            Comp wb = new Comp();
            wb.Key = "wb2api"; wb.Name = "wb2api（AI 接口网关）"; wb.Kind = "Port";
            wb.Dir = WbDir; wb.Exe = WbDir == null ? null : Path.Combine(WbDir, "wb2api.exe");
            wb.ProcessName = "wb2api.exe"; wb.Dedupe = true; wb.ReadyPort = 7863;
            wb.Ports = new int[] { 7863 }; wb.Url = "http://127.0.0.1:7863/"; wb.Optional = true;
            wb.TimeoutSec = 40;
            Components.Add(wb);

            Comp snow = new Comp();
            snow.Key = "snowluma"; snow.Name = "SnowLuma（QQ 登录 / OneBot）"; snow.Kind = "Port";
            snow.Dir = SnowDir; snow.Exe = SnowDir == null ? null : Path.Combine(SnowDir, "node.exe");
            snow.Args = new string[] { "index.mjs" };
            snow.ProcessName = "node.exe"; snow.CmdLike = "index.mjs"; snow.Dedupe = true;
            snow.ReadyPort = 5099; snow.Ports = new int[] { 5099, 3000, 3001 };
            snow.Url = "http://127.0.0.1:5099/"; snow.Optional = false;
            snow.TimeoutSec = 60;
            Components.Add(snow);

            Comp astr = new Comp();
            astr.Key = "astrbot"; astr.Name = "AstrBot（机器人本体）"; astr.Kind = "Port";
            astr.Dir = AstrDir; astr.Exe = AstrDir == null ? null : Path.Combine(AstrDir, "AstrBot.exe");
            astr.ProcessName = "AstrBot.exe"; astr.Dedupe = true; astr.ReadyPort = 6185;
            astr.Ports = new int[] { 6185, 6199 }; astr.Url = "http://127.0.0.1:6185/"; astr.Optional = false;
            astr.TimeoutSec = 120;
            Components.Add(astr);
        }

        // ---------------- 端口 / 进程 ----------------
        public static bool TestPort(int port)
        {
            if (port <= 0) return false;
            using (TcpClient c = new TcpClient())
            {
                try
                {
                    IAsyncResult ar = c.BeginConnect("127.0.0.1", port, null, null);
                    bool ready;
                    using (System.Threading.WaitHandle wh = ar.AsyncWaitHandle) ready = wh.WaitOne(400);
                    if (!ready) return false;
                    c.EndConnect(ar);
                    return true;
                }
                catch { return false; }
                finally { try { c.Close(); } catch { } }
            }
        }

        public List<ProcInfo> ListProcesses()
        {
            List<ProcInfo> list = new List<ProcInfo>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId,ParentProcessId,Name,ExecutablePath,CommandLine,CreationDate FROM Win32_Process"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        try
                        {
                            ProcInfo p = new ProcInfo();
                            p.Pid = Convert.ToInt32(mo["ProcessId"]);
                            p.ParentPid = mo["ParentProcessId"] == null ? 0 : Convert.ToInt32(mo["ParentProcessId"]);
                            p.Name = mo["Name"] as string;
                            p.Path = mo["ExecutablePath"] as string;
                            p.CmdLine = mo["CommandLine"] as string;
                            p.Created = ParseWmiDate(mo["CreationDate"] as string);
                            list.Add(p);
                        }
                        catch { }
                        finally { mo.Dispose(); }
                    }
                }
            }
            catch (Exception ex) { _log("枚举进程失败：" + ex.Message); }
            return list;
        }

        private static DateTime ParseWmiDate(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 8) return DateTime.MinValue;
            try
            {
                return ManagementDateTimeConverter.ToDateTime(s);
            }
            catch { return DateTime.MinValue; }
        }

        private static bool Matches(Comp c, ProcInfo p)
        {
            if (c.ProcessName == null) return false;
            if (!string.Equals(p.Name, c.ProcessName, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrEmpty(c.Dir) && !string.IsNullOrEmpty(p.Path))
            {
                string prefix = c.Dir.TrimEnd('\\') + "\\";
                if (!p.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            }
            if (!string.IsNullOrEmpty(c.CmdLike))
            {
                if (p.CmdLine == null || p.CmdLine.IndexOf(c.CmdLike, StringComparison.OrdinalIgnoreCase) < 0) return false;
            }
            return true;
        }

        public List<ProcInfo> ProcessesOf(Comp c, List<ProcInfo> all)
        {
            List<ProcInfo> result = new List<ProcInfo>();
            foreach (ProcInfo p in all) if (Matches(c, p)) result.Add(p);
            return result;
        }

        public bool IsReady(Comp c, List<ProcInfo> all)
        {
            if (c.Kind == "Process") return ProcessesOf(c, all).Count > 0;
            return TestPort(c.ReadyPort);
        }

        public int GetSnowLumaWebPort()
        {
            if (SnowDir != null)
            {
                string logFile = Path.Combine(SnowDir, @"logs\snowluma-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                if (File.Exists(logFile))
                {
                    try
                    {
                        string[] tail = TailLines(logFile, 400);
                        for (int i = tail.Length - 1; i >= 0; i--)
                        {
                            Match m = Regex.Match(tail[i], @"listening http://127\.0\.0\.1:(\d+)");
                            if (m.Success)
                            {
                                int port = int.Parse(m.Groups[1].Value);
                                if (TestPort(port)) return port;
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }
            foreach (int candidate in new int[] { 5099, 5100, 5101, 5102 })
                if (TestPort(candidate)) return candidate;
            return 5099;
        }

        private static string[] TailLines(string path, int count)
        {
            // 只从文件末尾读 256KB：SnowLuma 刷日志时单个文件能有几十 MB，整读会卡住刷新
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    const int window = 256 * 1024;
                    long start = Math.Max(0, fs.Length - window);
                    fs.Seek(start, SeekOrigin.Begin);
                    byte[] buf = new byte[fs.Length - start];
                    int read = fs.Read(buf, 0, buf.Length);
                    string text = Encoding.UTF8.GetString(buf, 0, read);
                    string[] lines = text.Split((char)10);
                    List<string> list = new List<string>();
                    int from = start > 0 ? 1 : 0;   // 首行可能是被截断的半行
                    for (int i = from; i < lines.Length; i++)
                    {
                        string s = lines[i];
                        if (s.Length > 0 && s[s.Length - 1] == (char)13) s = s.Substring(0, s.Length - 1);
                        list.Add(s);
                    }
                    if (list.Count <= count) return list.ToArray();
                    return list.GetRange(list.Count - count, count).ToArray();
                }
            }
            catch { return new string[0]; }
        }

        public string GetLoginUin()
        {
            if (SnowDir == null) return "";
            try
            {
                List<string> uins = new List<string>();
                foreach (string f in Directory.GetFiles(Path.Combine(SnowDir, "config"), "onebot_*.json"))
                {
                    Match m = Regex.Match(Path.GetFileName(f), @"^onebot_(\d+)\.json$");
                    if (m.Success && !uins.Contains(m.Groups[1].Value)) uins.Add(m.Groups[1].Value);
                }
                return string.Join(" / ", uins.ToArray());
            }
            catch { return ""; }
        }

        // ---------------- 状态汇总 ----------------
        public class Row
        {
            public string Key;
            public string Name;
            public bool Running;
            public string Owner = "-";
            public string Ports = "-";
            public string Url;
        }

        public List<Row> CollectStatus()
        {
            List<ProcInfo> all = ListProcesses();
            List<Row> rows = new List<Row>();
            int snowPort = 0;
            Comp[] comps;
            lock (_sync) { comps = Components.ToArray(); }
            foreach (Comp c in comps)
            {
                Row r = new Row();
                r.Key = c.Key; r.Name = c.Name; r.Url = c.Url;
                List<ProcInfo> procs = ProcessesOf(c, all);
                bool running;
                if (c.Key == "snowluma")
                {
                    snowPort = GetSnowLumaWebPort();
                    running = TestPort(snowPort);
                }
                else running = c.Kind == "Process" ? procs.Count > 0 : TestPort(c.ReadyPort);

                r.Running = running;
                if (procs.Count > 0)
                {
                    r.Owner = Path.GetFileNameWithoutExtension(procs[0].Name) + "/" + procs[0].Pid;
                    int roots = CountRoots(procs, all);
                    if (roots > 1) r.Owner += " +" + (roots - 1);
                }

                List<string> portText = new List<string>();
                if (c.Key == "snowluma" && snowPort != 0 && snowPort != 5099 && TestPort(snowPort))
                    portText.Add(snowPort + " 开(5099被占)");
                foreach (int p in c.Ports)
                    portText.Add(p + (TestPort(p) ? " 开" : " 关"));
                if (portText.Count > 0) r.Ports = string.Join("  ", portText.ToArray());
                rows.Add(r);
            }
            return rows;
        }

        private static int CountRoots(List<ProcInfo> procs, List<ProcInfo> all)
        {
            HashSet<int> ids = new HashSet<int>();
            foreach (ProcInfo p in procs) ids.Add(p.Pid);
            int roots = 0;
            foreach (ProcInfo p in procs) if (!ids.Contains(p.ParentPid)) roots++;
            return roots;
        }

        // ---------------- 启动 / 停止 ----------------
        public bool StartAll(Action<string> log, Func<bool> cancelled)
        {
            List<ProcInfo> all = ListProcesses();
            bool ok = true;
            Comp[] comps;
            lock (_sync) { comps = Components.ToArray(); }
            foreach (Comp c in comps)
            {
                if (cancelled != null && cancelled()) break;
                if (!StartOne(c, log, ref all)) ok = false;
            }
            return ok;
        }

        private bool StartOne(Comp c, Action<string> log, ref List<ProcInfo> all)
        {
            if (c.Exe == null || !File.Exists(c.Exe))
            {
                if (c.Optional) { log("没找到 " + c.Name + "，跳过"); return true; }
                log("没找到 " + c.Name + " 的安装目录，无法启动");
                return false;
            }

            List<ProcInfo> procs = ProcessesOf(c, all);
            if (c.Dedupe && procs.Count > 1) procs = RemoveExtras(c, procs, log);

            if (procs.Count > 0)
            {
                if (IsReady(c, all)) { log(c.Name + " 已在运行（PID " + procs[0].Pid + "），跳过"); return true; }
                log(c.Name + " 的进程已在（PID " + procs[0].Pid + "），等它就绪…");
                DateTime w = DateTime.Now.AddSeconds(15);
                while (DateTime.Now < w)
                {
                    if (IsReady(c, ListProcesses())) { log(c.Name + " 已就绪"); return true; }
                    System.Threading.Thread.Sleep(700);
                }
                log(c.Name + " 在跑但一直没就绪，重开一份");
                foreach (ProcInfo p in procs) { try { using (Process x = Process.GetProcessById(p.Pid)) x.Kill(); } catch { } }
                System.Threading.Thread.Sleep(2000);
                all = ListProcesses();
            }

            log("正在启动 " + c.Name + " …");
            DateTime startedAt = DateTime.Now;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = c.Exe;
                psi.WorkingDirectory = c.Dir;
                bool interactive = (c.Key == "qq" || c.Key == "astrbot");
                psi.UseShellExecute = interactive;
                if (interactive)
                {
                    psi.WindowStyle = c.Key == "astrbot" ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal;
                }
                else
                {
                    // 不要用管道接管子进程的输出：本程序一关，管道就断，
                    // SnowLuma 会陷入 EPIPE 死循环（实测能刷出 1GB 日志），
                    // 接着 OneBot 发消息全部超时，机器人就不回话了。
                    // 改成让 cmd 把输出直接重定向到 logs\<组件>.log 文件。
                    string logDir = _logDir;
                    try { Directory.CreateDirectory(logDir); } catch { }
                    string logFile = Path.Combine(logDir, c.Key + ".log");
                    string cmdLine = "\"" + c.Exe + "\"";
                    if (c.Args.Length > 0) cmdLine += " " + string.Join(" ", c.Args);

                    psi.FileName = "cmd.exe";
                    psi.Arguments = "/c \"" + cmdLine + " > \"" + logFile + "\" 2>&1\"";
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    if (c.Key == "snowluma") psi.EnvironmentVariables["SNOWLUMA_HOOK_AUTOLOAD"] = "1";
                    log("  输出写入 logs\\" + c.Key + ".log");
                }

                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.Start();
                }
            }
            catch (Exception ex)
            {
                log("启动 " + c.Name + " 失败：" + ex.Message);
                return false;
            }

            DateTime deadline = DateTime.Now.AddSeconds(c.TimeoutSec);
            while (DateTime.Now < deadline)
            {
                if (IsReady(c, ListProcesses()))
                {
                    log(c.Name + " 已就绪（" + (int)(DateTime.Now - startedAt).TotalSeconds + " 秒）");
                    return true;
                }
                System.Threading.Thread.Sleep(800);
            }
            log(c.Name + " 等了 " + c.TimeoutSec + " 秒还没就绪，请稍后刷新看看");
            return false;
        }

        private List<ProcInfo> RemoveExtras(Comp c, List<ProcInfo> procs, Action<string> log)
        {
            HashSet<int> ids = new HashSet<int>();
            foreach (ProcInfo p in procs) ids.Add(p.Pid);
            List<ProcInfo> roots = new List<ProcInfo>();
            foreach (ProcInfo p in procs) if (!ids.Contains(p.ParentPid)) roots.Add(p);
            if (roots.Count <= 1) return procs;

            roots.Sort(delegate(ProcInfo x, ProcInfo y) { return x.Created.CompareTo(y.Created); });
            HashSet<int> keep = new HashSet<int>();
            keep.Add(roots[0].Pid);
            for (int i = 0; i < 5; i++)
                foreach (ProcInfo p in procs)
                    if (keep.Contains(p.ParentPid)) keep.Add(p.Pid);

            List<ProcInfo> kept = new List<ProcInfo>();
            foreach (ProcInfo p in procs)
            {
                if (keep.Contains(p.Pid)) { kept.Add(p); continue; }
                try
                {
                    using (Process x = Process.GetProcessById(p.Pid)) x.Kill();
                    log("发现重复的 " + c.Name + "（PID " + p.Pid + "），已关掉多余的");
                }
                catch { }
            }
            return kept;
        }

        public void StopAll(Action<string> log, bool withQQ)
        {
            List<string> prefixes = new List<string>();
            foreach (string d in new string[] { AstrDir, SnowDir, WbDir })
                if (!string.IsNullOrEmpty(d)) prefixes.Add(d.TrimEnd('\\') + "\\");

            List<ProcInfo> procs = new List<ProcInfo>();
            foreach (ProcInfo p in ListProcesses())
            {
                if (string.IsNullOrEmpty(p.Path)) continue;
                foreach (string prefix in prefixes)
                {
                    if (p.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { procs.Add(p); break; }
                }
            }

            procs.Sort(delegate(ProcInfo x, ProcInfo y) { return Rank(x).CompareTo(Rank(y)); });
            foreach (ProcInfo p in procs)
            {
                string label = p.Name + " (PID " + p.Pid + ")";
                try
                {
                    using (Process proc = Process.GetProcessById(p.Pid))
                    {
                        try { if (proc.MainWindowHandle != IntPtr.Zero && proc.CloseMainWindow()) proc.WaitForExit(5000); }
                        catch { }
                        if (!proc.HasExited) { proc.Kill(); }
                    }
                    log("已结束 " + label);
                }
                catch (Exception ex)
                {
                    if (!(ex is ArgumentException)) log("结束 " + label + " 时出错：" + ex.Message);
                }
                System.Threading.Thread.Sleep(300);
            }

            System.Threading.Thread.Sleep(1500);
            foreach (ProcInfo p in ListProcesses())
            {
                bool inStack = false;
                foreach (string prefix in prefixes)
                    if (p.Path != null && p.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) inStack = true;
                if (!inStack) continue;
                try { using (Process x = Process.GetProcessById(p.Pid)) x.Kill(); log("补杀残留 " + p.Name + " (PID " + p.Pid + ")"); }
                catch { }
            }

            if (withQQ) StopQQ(log);
            else log("QQ 保持打开（避免把号踢下线）");
        }

        private int Rank(ProcInfo p)
        {
            if (AstrDir != null && p.Path != null && p.Path.StartsWith(AstrDir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return 0;
            if (SnowDir != null && p.Path != null && p.Path.StartsWith(SnowDir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return 2;
            if (WbDir != null && p.Path != null && p.Path.StartsWith(WbDir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return 4;
            return 3;
        }

        public void StopQQ(Action<string> log)
        {
            if (QQDir == null) return;
            string prefix = QQDir.TrimEnd('\\') + "\\";
            List<ProcInfo> qq = new List<ProcInfo>();
            foreach (ProcInfo p in ListProcesses())
                if (string.Equals(p.Name, "QQ.exe", StringComparison.OrdinalIgnoreCase) && p.Path != null &&
                    p.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) qq.Add(p);

            if (qq.Count == 0) { log("QQ 没有在运行"); return; }
            qq.Sort(delegate(ProcInfo x, ProcInfo y) { return y.Created.CompareTo(x.Created); });
            foreach (ProcInfo p in qq)
            {
                try { using (Process x = Process.GetProcessById(p.Pid)) x.Kill(); log("已结束 QQ (PID " + p.Pid + ")"); }
                catch { }
            }
        }
    }
}
