// SnowDream by øktaus — снег на рабочем столе с сугробами на окнах.
// Сборка: build.bat (встроенный csc из .NET Framework 4.x, C# 5).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DesktopSnow
{
    static class N
    {
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int W, H; public SIZE(int w, int h) { W = w; H = h; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte Op, Flags, Alpha, Format; }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight; public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }
        [StructLayout(LayoutKind.Sequential)] public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }

        public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
        public delegate bool MonitorEnumProc(IntPtr hm, IntPtr hdc, ref RECT r, IntPtr d);

        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool GetLayeredWindowAttributes(IntPtr h, out uint key, out byte alpha, out uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr d);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref POINT pt, ref SIZE sz, IntPtr src, ref POINT ptSrc, uint key, ref BLENDFUNCTION bf, uint flags);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern bool PeekMessage(out MSG m, IntPtr h, uint a, uint b, uint r);
        [DllImport("user32.dll")] public static extern uint MsgWaitForMultipleObjects(uint n, IntPtr handles, bool all, uint ms, uint mask);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int sz);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int sz);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int sz);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER bi, uint usage, out IntPtr bits, IntPtr sec, uint off);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr o);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool GdiFlush();
        [DllImport("gdi32.dll")] public static extern int GetDeviceCaps(IntPtr dc, int i);
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint p);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint p);
    }

    // Прозрачное, «некликабельное» окно поверх всего экрана.
    class Overlay : NativeWindow
    {
        public Overlay(int x, int y, int w, int h)
        {
            CreateParams cp = new CreateParams();
            cp.Caption = "SnowDreamOverlay";
            cp.X = x; cp.Y = y; cp.Width = w; cp.Height = h;
            cp.Style = unchecked((int)0x80000000);                       // WS_POPUP
            cp.ExStyle = 0x80000 | 0x20 | 0x80 | 0x8 | 0x08000000;       // LAYERED|TRANSPARENT|TOOLWINDOW|TOPMOST|NOACTIVATE
            CreateHandle(cp);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; }  // WM_NCHITTEST -> HTTRANSPARENT
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }   // WM_MOUSEACTIVATE -> NOACTIVATE
            base.WndProc(ref m);
        }
    }

    struct P
    {
        public float x, y, vx, vy, r, a, d, ph, fr, m, ignT, hv;
        public int ign;
        public bool clump;
    }

    class Surface
    {
        static int nextId = 1;
        public int Id = nextId++;
        public IntPtr Hwnd;
        public int Kind;            // 0 окно, 1 панель задач, 2 низ экрана
        public int L, T, R, B;
        public float[] H;           // высота сугроба по каждому столбцу пикселей
        public float[] Base;        // насколько кромка опущена в этом столбце (скруглённые углы)
        public int Rad;             // радиус скругления углов окна
        public float SpillL, SpillR, HMax;
        public bool Seen, Enabled;
        public double Vx, Vy;
        public int MoveUp;
        public List<int> Occ = new List<int>();

        public void ComputeBase()
        {
            int n = H.Length;
            Base = new float[n];
            if (Rad <= 0) return;
            for (int c = 0; c < Rad && c < n; c++)
            {
                float d = Rad - (c + 0.5f);
                float off = Rad - (float)Math.Sqrt(Math.Max(0f, Rad * Rad - d * d));
                Base[c] = Math.Max(Base[c], off);
                Base[n - 1 - c] = Math.Max(Base[n - 1 - c], off);
            }
        }
    }

    struct Entry { public IntPtr H; public N.RECT R; public bool Tray; }

    // Все настройки программы.
    class Config
    {
        public int Intensity = 1;      // 0 лёгкий, 1 снегопад, 2 метель
        public bool Snowing = true;
        public float Speed = 1f;       // множитель скорости падения
        public bool Front = true;      // снег поверх окон (иначе — только на рабочем столе)
        public bool Drifts = true;     // сугробы на окнах
        public bool Taskbar = true;    // сугробы на панели задач
        public int MaxMode = 1;        // развёрнутое окно: 0 обычно, 1 тихо, 2 скрыть
        public int Perf = 1;           // 0 эко, 1 баланс, 2 плавно
        public bool HideFs = true;     // прятать в играх/полноэкранных окнах
        public int Lang = DefaultLang(); // 0 русский, 1 английский

        static int DefaultLang()
        {
            string l = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return l == "ru" || l == "uk" || l == "be" || l == "kk" ? 0 : 1;
        }

        static string AppData { get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); } }
        public static string Dir { get { return Path.Combine(AppData, "SnowDream"); } }
        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        public void Load()
        {
            try
            {
                string f = FilePath;
                if (!File.Exists(f)) f = Path.Combine(Path.Combine(AppData, "DesktopSnow"), "settings.ini");   // настройки старой версии
                if (!File.Exists(f)) return;
                foreach (string line in File.ReadAllLines(f))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    int iv; float fv;
                    bool b = v == "1";
                    switch (k)
                    {
                        case "intensity": if (int.TryParse(v, out iv)) Intensity = Math.Max(0, Math.Min(2, iv)); break;
                        case "snowing": Snowing = b; break;
                        case "speed": if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out fv)) Speed = Math.Max(0.3f, Math.Min(2.5f, fv)); break;
                        case "front": Front = b; break;
                        case "drifts": Drifts = b; break;
                        case "taskbar": Taskbar = b; break;
                        case "maxMode": if (int.TryParse(v, out iv)) MaxMode = Math.Max(0, Math.Min(2, iv)); break;
                        case "calmMaximized": MaxMode = b ? 1 : 0; break;   // совместимость со старой версией
                        case "perf": if (int.TryParse(v, out iv)) Perf = Math.Max(0, Math.Min(2, iv)); break;
                        case "hideFullscreen": HideFs = b; break;
                        case "lang": if (int.TryParse(v, out iv)) Lang = iv == 1 ? 1 : 0; break;
                    }
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                StringBuilder sb = new StringBuilder();
                sb.Append("intensity=").Append(Intensity).Append("\r\n");
                sb.Append("snowing=").Append(Snowing ? 1 : 0).Append("\r\n");
                sb.Append("speed=").Append(Speed.ToString("0.0", CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("front=").Append(Front ? 1 : 0).Append("\r\n");
                sb.Append("drifts=").Append(Drifts ? 1 : 0).Append("\r\n");
                sb.Append("taskbar=").Append(Taskbar ? 1 : 0).Append("\r\n");
                sb.Append("maxMode=").Append(MaxMode).Append("\r\n");
                sb.Append("perf=").Append(Perf).Append("\r\n");
                sb.Append("hideFullscreen=").Append(HideFs ? 1 : 0).Append("\r\n");
                sb.Append("lang=").Append(Lang).Append("\r\n");
                File.WriteAllText(FilePath, sb.ToString());
            }
            catch { }
        }
    }

    // Локализация: L.T("по-русски", "in English").
    static class L
    {
        public static Config C;
        public static bool En { get { return C != null && C.Lang == 1; } }
        public static string T(string ru, string en) { return En ? en : ru; }
    }

    unsafe class SnowApp : ApplicationContext
    {
        // ---------- параметры ----------
        static readonly float[] Rates = { 30f, 75f, 190f };      // снежинок в секунду на 1000 px ширины
        static readonly float[] SpeedMul = { 0.9f, 1.0f, 1.55f };
        static readonly float[] WindBase = { 7f, 14f, 45f };
        static readonly float[] WindBias = { 0f, 4f, 70f };
        static readonly float[] PerfRate = { 0.7f, 1f, 1f };
        const float G = 2200f;          // гравитация для комков, px/s²
        const float MassScale = 0.2f;   // сколько «высоты» даёт одна снежинка
        const float GustDur = 5f;       // длительность порыва ветра «Сдуть снег», с
        const float GustPower = 1500f;  // сила порыва, px/s

        public readonly Config C = new Config();
        public float S = 1f;
        public float CpuPct, Fps;
        public int Particles { get { return pc; } }
        public bool Gusting { get { return gustT >= 0; } }
        public Icon AppIcon;

        Overlay ov;
        IntPtr screenDC, memDC, hBmp, oldBmp;
        uint* buf;
        int vL, vT, vW, vH;
        int refreshHz = 60;

        P[] ps = new P[40000]; int pc;
        Dictionary<IntPtr, Surface> surf = new Dictionary<IntPtr, Surface>();
        List<Surface> z = new List<Surface>();
        List<Surface> grounds = new List<Surface>();
        List<N.RECT> mons = new List<N.RECT>();
        List<Entry> entries = new List<Entry>();
        List<IntPtr> toRemove = new List<IntPtr>();
        N.EnumWindowsProc enumCb;
        N.MonitorEnumProc monCb;
        StringBuilder sb = new StringBuilder(256);
        int[] oT = new int[64], oB = new int[64];

        Random rng = new Random();
        Stopwatch sw = Stopwatch.StartNew();
        double lastTime, t, lastSlow, lastFs, nextFrame;
        float wind, spawnAcc, gustAcc;
        float calm, calmTarget;      // 0 — обычный снег, 1 — тихий режим (развёрнутое окно)
        float gustT = -1f, gust;     // порыв ветра: время с начала и текущая сила 0..1
        int frame, framesCount;
        bool running = true, hidden, hiddenDrawn, driftsWas = true;
        Process proc = Process.GetCurrentProcess();
        TimeSpan lastCpu;

        NotifyIcon tray;
        ToolStripMenuItem miSnowing, miSettings, miBlow, miQuit;
        SettingsForm settings;
        EventWaitHandle showEvt, blowEvt;

        public SnowApp(EventWaitHandle showEvent, EventWaitHandle blowEvent, bool openSettings)
        {
            showEvt = showEvent; blowEvt = blowEvent;
            C.Load();
            L.C = C;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                    if (k != null && k.GetValue("DesktopSnow") != null) SetAutostart(true);   // автозапуск старой версии → SnowDream
            }
            catch { }
            driftsWas = C.Drifts;
            N.timeBeginPeriod(1);
            try { S = N.GetDpiForSystem() / 96f; } catch { }
            if (S < 1f) S = 1f;
            try { AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            enumCb = OnEnum;
            monCb = OnMonitor;
            screenDC = N.GetDC(IntPtr.Zero);
            memDC = N.CreateCompatibleDC(screenDC);
            int hz = N.GetDeviceCaps(screenDC, 116);   // VREFRESH
            refreshHz = hz > 1 ? Math.Min(165, Math.Max(60, hz)) : 60;
            SetupScreen();
            ov = new Overlay(vL, vT, vW, vH);

            if (C.Snowing)
            {
                float avgV = 65f * S * SpeedMul[C.Intensity] * C.Speed;
                int pre = (int)(Rates[C.Intensity] * vW / 1000f * vH / avgV);
                for (int i = 0; i < pre; i++) Flake(1);
            }

            Clear(); Present();
            N.ShowWindow(ov.Handle, 4);
            BuildTray();
            lastTime = sw.Elapsed.TotalSeconds;
            nextFrame = lastTime;
            lastCpu = proc.TotalProcessorTime;
            Application.Idle += OnIdle;
            if (Diag) StartDiag();
            if (openSettings) ShowSettings();
        }

        float Rnd() { return (float)rng.NextDouble(); }
        static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }

        // ---------- экран ----------
        void SetupScreen()
        {
            vL = N.GetSystemMetrics(76); vT = N.GetSystemMetrics(77);
            vW = N.GetSystemMetrics(78); vH = N.GetSystemMetrics(79);
            if (hBmp != IntPtr.Zero) { N.SelectObject(memDC, oldBmp); N.DeleteObject(hBmp); }
            N.BITMAPINFOHEADER bi = new N.BITMAPINFOHEADER();
            bi.biSize = 40; bi.biWidth = vW; bi.biHeight = -vH; bi.biPlanes = 1; bi.biBitCount = 32;
            IntPtr bits;
            hBmp = N.CreateDIBSection(screenDC, ref bi, 0, out bits, IntPtr.Zero, 0);
            buf = (uint*)bits;
            oldBmp = N.SelectObject(memDC, hBmp);

            mons.Clear();
            N.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, monCb, IntPtr.Zero);
            grounds.Clear();
            foreach (N.RECT m in mons)
            {
                Surface g = new Surface();
                g.Kind = 2; g.L = m.L; g.R = m.R; g.T = m.B; g.B = m.B + 1;
                g.H = new float[Math.Max(1, m.R - m.L)];
                g.HMax = 24f * S; g.Enabled = true;
                g.ComputeBase();
                grounds.Add(g);
            }
        }

        bool OnMonitor(IntPtr hm, IntPtr hdc, ref N.RECT r, IntPtr d) { mons.Add(r); return true; }

        // ---------- главный цикл ----------
        // ---- диагностика (запуск с --diag): журнал раз в секунду из фонового потока ----
        public static bool Diag;
        int dIdle, dFrames, dUlwFail, dLastErr;
        System.Threading.Timer diagTimer;

        void StartDiag()
        {
            string f = Path.Combine(Config.Dir, "diag.log");
            try { Directory.CreateDirectory(Config.Dir); File.WriteAllText(f, ""); } catch { }
            diagTimer = new System.Threading.Timer(delegate
            {
                try
                {
                    IntPtr fg = N.GetForegroundWindow();
                    StringBuilder c = new StringBuilder(128); N.GetClassName(fg, c, 128);
                    File.AppendAllText(f, string.Format("{0:HH:mm:ss.fff} idle={1} frames={2} ulwFail={3} err={4} hidden={5} pc={6} settings={7} fg={8}\r\n",
                        DateTime.Now, dIdle, dFrames, dUlwFail, dLastErr, hidden, pc,
                        settings == null ? "null" : (settings.IsDisposed ? "disposed" : "open"), c));
                }
                catch { }
            }, null, 1000, 1000);
        }

        void OnIdle(object sender, EventArgs e)
        {
            N.MSG msg;
            dIdle++;
            while (running && !N.PeekMessage(out msg, IntPtr.Zero, 0, 0, 0))
            {
                Frame();
                dFrames++;
                double target;
                if (hidden) target = 0.2;
                else if (pc == 0 && !Gusting) target = 0.05;
                else target = C.Perf == 0 ? 1.0 / 30 : (C.Perf == 1 ? 1.0 / 60 : 1.0 / refreshHz);
                double now = sw.Elapsed.TotalSeconds;
                nextFrame += target;
                if (nextFrame < now) nextFrame = now;
                int ms = (int)((nextFrame - now) * 1000);
                if (ms > 0) N.MsgWaitForMultipleObjects(0, IntPtr.Zero, false, (uint)ms, 0x4FF);
            }
        }

        void Frame()
        {
            double now = sw.Elapsed.TotalSeconds;
            float dt = (float)(now - lastTime); lastTime = now;
            if (dt > 0.05f) dt = 0.05f; if (dt < 0.0005f) dt = 0.0005f;

            if (now - lastFs > 0.3)
            {
                lastFs = now;
                bool maxFg = IsMaximizedFg();
                hidden = (C.HideFs && IsFullscreenFg()) || (C.MaxMode == 2 && maxFg);
                calmTarget = C.MaxMode == 1 && maxFg ? 1f : 0f;
                if (showEvt != null && showEvt.WaitOne(0)) ShowSettings();
                if (blowEvt != null && blowEvt.WaitOne(0)) StartGust();
            }
            calm += (calmTarget - calm) * Math.Min(1f, dt * 1.5f);   // плавный переход ~2 с
            if (now - lastSlow > 1.0)
            {
                double el = now - lastSlow;
                lastSlow = now;
                N.SetWindowPos(ov.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x1 | 0x2 | 0x10 | 0x200);
                if (N.GetSystemMetrics(76) != vL || N.GetSystemMetrics(77) != vT ||
                    N.GetSystemMetrics(78) != vW || N.GetSystemMetrics(79) != vH) SetupScreen();
                proc.Refresh();
                TimeSpan cpu = proc.TotalProcessorTime;
                CpuPct = (float)((cpu - lastCpu).TotalSeconds / el / Environment.ProcessorCount * 100.0);
                lastCpu = cpu;
                Fps = (float)(framesCount / el);
                framesCount = 0;
            }
            if (C.Drifts != driftsWas) { if (!C.Drifts) ShakeAll(); driftsWas = C.Drifts; }
            if (hidden)
            {
                if (!hiddenDrawn) { Clear(); Present(); hiddenDrawn = true; }
                return;
            }
            hiddenDrawn = false;
            t += dt; frame++; framesCount++;

            // порыв ветра «Сдуть снег»: разгон 0.7 с, держится до 3.8 с, стихает к 5 с
            gust = 0f;
            if (gustT >= 0)
            {
                gustT += dt;
                if (gustT >= GustDur) gustT = -1f;
                else if (gustT < 0.7f) { float q = gustT / 0.7f; gust = q * q * (3 - 2 * q); }
                else if (gustT < 3.8f) gust = 1f;
                else { float q = 1f - (gustT - 3.8f) / (GustDur - 3.8f); gust = q * q * (3 - 2 * q); }
            }

            int I = C.Intensity;
            float wb = WindBase[I];
            wind = (wb * (0.55f * (float)Math.Sin(t * 0.05) + 0.3f * (float)Math.Sin(t * 0.17 + 1.3) + 0.15f * (float)Math.Sin(t * 0.61 + 2.1))
                   + WindBias[I] * (1f - gust) + GustPower * gust) * S;

            UpdateSurfaces(dt);
            if (Gusting) BlowSweep();
            if (C.Drifts && !Blowing) foreach (Surface s in z) if (s.Enabled) Slump(s);
            if (C.Snowing)
            {
                spawnAcc += Rates[I] * vW / 1000f * dt * (1f - 0.8f * calm) * PerfRate[C.Perf];
                while (spawnAcc >= 1f) { spawnAcc -= 1f; Flake(0); }
                if (gust > 0.15f)
                {
                    // во время порыва снег «заносит» слева, чтобы экран не пустел
                    gustAcc += Rates[I] * vH / 1000f * gust * 2.5f * dt;
                    while (gustAcc >= 1f) { gustAcc -= 1f; Flake(2); }
                }
            }
            UpdateParticles(dt);
            Render();
        }

        bool Blowing { get { return gustT >= 0 && gustT < 4.2f; } }

        public void StartGust()
        {
            if (Gusting) return;
            gustT = 0f;
        }

        // Фронт ветра идёт слева направо и поднимает в воздух весь снег, который накрыл.
        void BlowSweep()
        {
            float front = vL - 50f + (gustT / 1.1f) * (vW + 100f);
            foreach (Surface s in z) Blow(s, front);
        }

        void Blow(Surface s, float front)
        {
            float[] h = s.H; int n = h.Length;
            int lim = (int)Math.Min(n, front - s.L);
            for (int i = 0; i < lim; i += 2)
            {
                bool two = i + 1 < n;
                float a = h[i], b = two ? h[i + 1] : 0f, area = a + b;
                if (area < 0.05f) continue;
                h[i] = 0; if (two) h[i + 1] = 0;
                float col = area * 0.5f;
                int cnt = Math.Max(1, (int)Math.Ceiling(col / 3f));
                float bot = s.T + s.Base[i];
                for (int k = 0; k < cnt; k++)
                    Clump(s.L + i + 0.5f + Rnd(), bot - col + (k + 0.5f) * col / cnt,
                          (300f + Rnd() * 500f) * S, -(80f + Rnd() * 260f) * S, area / cnt, 0);
            }
            s.SpillL = 0; s.SpillR = 0;
        }

        // ---------- окна ----------
        bool OnEnum(IntPtr h, IntPtr l)
        {
            if (h == ov.Handle) return true;
            if (!N.IsWindowVisible(h) || N.IsIconic(h)) return true;
            int ex = N.GetWindowLong(h, -20);
            sb.Length = 0; N.GetClassName(h, sb, sb.Capacity);
            string cls = sb.ToString();
            bool tr = cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd";
            if (!tr)
            {
                if ((ex & 0x80) != 0 || (ex & 0x20) != 0) return true;   // toolwindow / прозрачные оверлеи
                if (cls == "Progman" || cls == "WorkerW") return true;
                if ((ex & 0x80000) != 0)
                {
                    uint key, fl; byte a;
                    if (N.GetLayeredWindowAttributes(h, out key, out a, out fl) && (fl & 2) != 0 && a < 40) return true;
                }
            }
            int cloaked;
            if (N.DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
            N.RECT r;
            if (N.DwmGetWindowAttribute(h, 9, out r, 16) != 0) N.GetWindowRect(h, out r);
            if (r.R - r.L < 60 || r.B - r.T < 30) return true;
            Entry e = new Entry(); e.H = h; e.R = r; e.Tray = tr;
            entries.Add(e);
            return true;
        }

        static readonly bool Win11 = DetectWin11();
        static bool DetectWin11()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    int b;
                    return k != null && int.TryParse(Convert.ToString(k.GetValue("CurrentBuildNumber")), out b) && b >= 22000;
                }
            }
            catch { return false; }
        }

        // Windows 11 скругляет углы окон: 8 px (или 4 px для «маленького» скругления), с учётом масштаба.
        int CornerRadius(IntPtr h)
        {
            if (!Win11) return 0;
            int pref;
            if (N.DwmGetWindowAttribute(h, 33, out pref, 4) == 0)
            {
                if (pref == 1) return 0;
                if (pref == 3) return (int)Math.Round(4 * S);
            }
            return (int)Math.Round(8 * S);
        }

        bool SurfaceAllowed(Surface s)
        {
            int cx = (s.L + s.R) / 2;
            foreach (N.RECT m in mons)
            {
                if (cx >= m.L && cx < m.R && s.T >= m.T - 1 && s.T < m.B)
                {
                    if (s.T <= m.T + 2) return false;                        // развёрнуто / упёрлось в верх
                    if (s.L <= m.L && s.R >= m.R && s.B >= m.B) return false;
                    return true;
                }
            }
            return false;
        }

        void UpdateSurfaces(float dt)
        {
            entries.Clear();
            N.EnumWindows(enumCb, IntPtr.Zero);
            foreach (Surface s in surf.Values) s.Seen = false;
            z.Clear();

            foreach (Entry e in entries)
            {
                Surface s;
                int nl = e.R.L, nt = e.R.T, nr = e.R.R, nb = e.R.B;
                if (!surf.TryGetValue(e.H, out s))
                {
                    s = new Surface();
                    s.Hwnd = e.H; s.Kind = e.Tray ? 1 : 0;
                    s.L = nl; s.T = nt; s.R = nr; s.B = nb;
                    s.H = new float[nr - nl];
                    s.HMax = (e.Tray ? 14f : 42f) * S;
                    s.Rad = e.Tray ? 0 : CornerRadius(e.H);
                    s.ComputeBase();
                    surf[e.H] = s;
                }
                else
                {
                    double ovx = s.Vx, ovy = s.Vy;
                    s.Vx += ((nl - s.L) / (double)dt - s.Vx) * 0.35;
                    s.Vy += ((nt - s.T) / (double)dt - s.Vy) * 0.35;
                    s.MoveUp = Math.Max(0, s.T - nt);
                    if (nr - nl != s.R - s.L) Remap(s, nl, nt, nr, nb);
                    else { s.L = nl; s.T = nt; s.R = nr; s.B = nb; }

                    if (s.Kind == 0 && s.Enabled && C.Drifts)
                    {
                        // инерция: при резком рывке снег слетает, при падении быстрее g — отрывается
                        double ax = (s.Vx - ovx) / dt, ay = (s.Vy - ovy) / dt;
                        double frac = 0, thx = 4 * G * S, gy = 1.2 * G * S;
                        if (Math.Abs(ax) > thx) frac = Math.Min(0.6, (Math.Abs(ax) - thx) / (25 * G * S));
                        if (ay > gy) frac = Math.Max(frac, Math.Min(1.0, (ay - gy) / (6 * G * S)));
                        float cap = 1500f * S;
                        if (frac > 0.01) Collapse(s, (float)frac, Clamp((float)ovx, -cap, cap), Clamp((float)ovy, -cap, cap), s.Id);
                    }
                }
                s.Seen = true;
                bool en = s.Kind == 1 ? C.Taskbar : SurfaceAllowed(s);
                if (s.Enabled && !en) Collapse(s, 1f, 0, 0, 0);
                s.Enabled = en;
                z.Add(s);
            }

            toRemove.Clear();
            foreach (KeyValuePair<IntPtr, Surface> kv in surf) if (!kv.Value.Seen) toRemove.Add(kv.Key);
            foreach (IntPtr h in toRemove)
            {
                Surface s = surf[h];
                if (s.Enabled) Collapse(s, 1f, 0, 0, 0);   // окно закрыли/свернули — сугроб падает
                surf.Remove(h);
            }
            foreach (Surface g in grounds) z.Add(g);

            for (int i = 0; i < z.Count; i++)
            {
                Surface s = z[i]; s.Occ.Clear();
                if (!s.Enabled) continue;
                int bt = s.T - (int)s.HMax - 2;
                for (int j = 0; j < i; j++)
                {
                    Surface o = z[j];
                    if (o.L < s.R && o.R > s.L && o.T < s.T && o.B > bt) s.Occ.Add(j);
                }
            }
        }

        void Remap(Surface s, int nl, int nt, int nr, int nb)
        {
            int nw = Math.Max(1, nr - nl);
            float[] nh = new float[nw];
            for (int i = 0; i < s.H.Length; i++)
            {
                float v = s.H[i];
                if (v <= 0.05f) continue;
                int j = s.L + i - nl;
                if (j >= 0 && j < nw) nh[j] = v;
                else
                {
                    int cnt = (int)Math.Ceiling(v / 2.5f);
                    float bot = s.T + s.Base[i];
                    for (int k = 0; k < cnt; k++)
                        Clump(s.L + i + 0.5f, bot - v + (k + 0.5f) * v / cnt, (Rnd() - 0.5f) * 10 * S, Rnd() * 15 * S, v / cnt, s.Id);
                }
            }
            s.H = nh; s.L = nl; s.T = nt; s.R = nr; s.B = nb;
            s.ComputeBase();
        }

        // Часть снега (frac) с поверхности превращается в падающие комки.
        void Collapse(Surface s, float frac, float vx, float vy, int ign)
        {
            float[] h = s.H; int n = h.Length;
            for (int i = 0; i < n; i += 2)
            {
                bool two = i + 1 < n;
                float h0 = h[i], h1 = two ? h[i + 1] : 0f;
                float a = h0 * frac, b = h1 * frac, area = a + b;
                h[i] = h0 - a; if (two) h[i + 1] = h1 - b;
                if (area < 0.3f) continue;
                float col = (h0 + h1) * 0.5f, hs = area * 0.5f;
                int cnt = Math.Max(1, (int)Math.Ceiling(hs / 2.5f));
                float top = s.T + s.Base[i] - col;
                for (int k = 0; k < cnt; k++)
                    Clump(s.L + i + 0.5f + Rnd(), top + (k + 0.5f) * hs / cnt,
                          vx + (Rnd() - 0.5f) * 14f * S, vy + Rnd() * 22f * S, area / cnt, ign);
            }
        }

        // Сравниваем абсолютный уровень снега (с учётом изгиба кромки), а не просто толщину.
        static void Rel(float[] h, float[] bs, int a, int b, float rep)
        {
            float d = (h[a] - bs[a]) - (h[b] - bs[b]);
            if (d > rep) { float q = Math.Min((d - rep) * 0.25f, h[a]); h[a] -= q; h[b] += q; }
            else if (d < -rep) { float q = Math.Min((-d - rep) * 0.25f, h[b]); h[b] -= q; h[a] += q; }
        }

        // Осыпание: склон не круче угла естественного откоса, с краёв снег сползает вниз.
        void Slump(Surface s)
        {
            float[] h = s.H; int n = h.Length;
            if (n < 2) return;
            const float rep = 0.9f;
            float[] bs = s.Base;
            if ((frame & 1) == 0) for (int i = 0; i < n - 1; i++) Rel(h, bs, i, i + 1, rep);
            else for (int i = n - 2; i >= 0; i--) Rel(h, bs, i, i + 1, rep);
            // лёгкая диффузия — сглаживает «пилу» от отдельных снежинок, массу сохраняет
            // (только на прямом участке кромки; на скруглениях работает Rel)
            int d0 = Math.Max(1, s.Rad), d1 = n - 1 - s.Rad;
            if (d1 > d0)
            {
                float prev = h[d0 - 1];
                for (int i = d0; i < d1; i++)
                {
                    float cur = h[i];
                    float lap = prev + h[i + 1] - 2f * cur;
                    prev = cur;
                    h[i] = cur + lap * 0.12f;
                }
            }
            float eh = 1.5f * S;
            if (h[0] > eh) { float ex = (h[0] - eh) * 0.3f; h[0] -= ex; s.SpillL += ex; }
            if (h[n - 1] > eh) { float ex = (h[n - 1] - eh) * 0.3f; h[n - 1] -= ex; s.SpillR += ex; }
            if (s.SpillL > 4f) { if (s.Kind != 2) Clump(s.L - 1f, s.T + bs[0] - 1.5f, -(8 + Rnd() * 25) * S, 0, s.SpillL, s.Id); s.SpillL = 0; }
            if (s.SpillR > 4f) { if (s.Kind != 2) Clump(s.R + 0.5f, s.T + bs[n - 1] - 1.5f, (8 + Rnd() * 25) * S, 0, s.SpillR, s.Id); s.SpillR = 0; }
        }

        void AddSnow(Surface s, int c, float m, float r)
        {
            int k = (int)(r * 2.5f) + 2;
            float[] h = s.H; int n = h.Length;
            float sum = (k + 1) * (k + 1);
            for (int o = -k; o <= k; o++)
            {
                int cc = c + o;
                if (cc < 0 || cc >= n) continue;
                float nv = h[cc] + m * (k + 1 - Math.Abs(o)) / sum;
                h[cc] = nv > s.HMax ? s.HMax : nv;
            }
        }

        // ---------- частицы ----------
        // where: 0 — сверху экрана, 1 — в любом месте (старт), 2 — с левого края (порыв ветра)
        void Flake(int where)
        {
            if (pc >= ps.Length) return;
            P p = new P();
            p.d = Rnd();
            p.r = (0.55f + 1.5f * (float)Math.Pow(p.d, 1.5) + Rnd() * 0.25f) * S;
            p.vy = (28f + 72f * p.d) * S * SpeedMul[C.Intensity] * (0.85f + 0.3f * Rnd());
            p.a = 0.35f + 0.6f * p.d;
            p.ph = Rnd() * 6.2832f;
            p.fr = 0.6f + 1.4f * Rnd();
            p.m = 3.14159f * p.r * p.r * MassScale;
            float margin = 150f * S + Math.Abs(WindBias[C.Intensity]) * 3f * S;
            if (where == 2)
            {
                p.x = vL - 10f - Rnd() * 60f * S;
                p.y = vT + Rnd() * vH;
            }
            else
            {
                p.x = vL - margin + Rnd() * (vW + 2 * margin);
                p.y = where == 1 ? vT + Rnd() * vH : vT - 6f - Rnd() * 20f;
            }
            ps[pc++] = p;
        }

        void Clump(float x, float y, float vx, float vy, float m, int ign)
        {
            if (pc >= ps.Length || m < 0.25f) return;
            P p = new P();
            p.x = x; p.y = y; p.vx = vx; p.vy = vy; p.m = m; p.clump = true;
            float r = (float)Math.Sqrt(m / 3.14159f);
            p.r = Clamp(r, 0.7f, 2.6f * S);
            p.a = 0.95f; p.d = 1f;
            p.ign = ign; p.ignT = ign != 0 ? 0.6f : 0f;
            ps[pc++] = p;
        }

        void UpdateParticles(float dt)
        {
            float tv = 1100f * S, gk = G * S * (1f - 0.85f * gust) * dt, k = Math.Min(1f, (0.8f + 4f * gust) * dt);
            float right = vL + vW + 250 * S, left = vL - 250 * S, bottom = vT + vH + 10;
            float spd = C.Speed;
            bool canLand = C.Drifts && !Blowing;
            for (int i = 0; i < pc; )
            {
                P p = ps[i];
                float oy = p.y;
                if (!p.clump)
                {
                    float sway = (float)Math.Sin(t * p.fr + p.ph) * (6f + 10f * p.d) * S;
                    p.hv = wind * (0.35f + 0.75f * p.d) + sway;
                    p.x += p.hv * dt;
                    p.y += p.vy * spd * dt;
                }
                else
                {
                    p.vy += gk; if (p.vy > tv) p.vy = tv;
                    p.vx += (wind * 0.5f - p.vx) * k;
                    p.hv = p.vx;
                    p.x += p.vx * dt; p.y += p.vy * dt;
                    if (p.ignT > 0) { p.ignT -= dt; if (p.ignT <= 0) p.ign = 0; }
                }
                bool dead = false;
                if (canLand && (p.clump || p.d > 0.22f) && Land(ref p, oy)) dead = true;   // дальние снежинки пролетают «за» окнами
                else if (p.y > bottom || p.x < left || p.x > right) dead = true;
                if (dead) { ps[i] = ps[--pc]; continue; }
                ps[i] = p; i++;
            }
        }

        bool Land(ref P p, float oy)
        {
            int xi = (int)Math.Floor(p.x);
            Surface best = null; int bc = 0; float by = float.MaxValue;
            for (int i = 0; i < z.Count; i++)
            {
                Surface s = z[i];
                if (!s.Enabled || xi < s.L || xi >= s.R || s.Id == p.ign) continue;
                int c = xi - s.L;
                float ys = s.T + s.Base[c] - s.H[c];
                if (p.y >= ys && oy <= ys + 1.5f + s.MoveUp && ys < by)
                {
                    bool vis = true; float yy = ys - 0.5f;
                    for (int j = 0; j < i; j++)
                    {
                        Surface o = z[j];
                        if (xi >= o.L && xi < o.R && yy >= o.T && yy < o.B) { vis = false; break; }
                    }
                    if (vis) { best = s; bc = c; by = ys; }
                }
            }
            if (best == null) return false;
            AddSnow(best, bc, p.m, p.r);
            return true;
        }

        // ---------- отрисовка ----------
        void Clear()
        {
            long n = (long)vW * vH;
            ulong* q = (ulong*)buf;
            long n2 = n / 2;
            for (long i = 0; i < n2; i++) q[i] = 0;
            if ((n & 1) != 0) buf[n - 1] = 0;
        }

        void Present()
        {
            N.GdiFlush();
            N.POINT pt = new N.POINT(vL, vT), src = new N.POINT(0, 0);
            N.SIZE sz = new N.SIZE(vW, vH);
            N.BLENDFUNCTION bf = new N.BLENDFUNCTION(); bf.Alpha = 255; bf.Format = 1;
            if (!N.UpdateLayeredWindow(ov.Handle, screenDC, ref pt, ref sz, memDC, ref src, 0, ref bf, 2))
            {
                dUlwFail++; dLastErr = Marshal.GetLastWin32Error();
            }
        }

        static void Blend(uint* px, int r, int g, int b, float a)
        {
            int A = (int)(a * 255f + 0.5f);
            if (A <= 0) return;
            if (A > 255) A = 255;
            uint d = *px;
            int ia = 255 - A;
            int or = (r * A + (int)((d >> 16) & 255) * ia) / 255;
            int og = (g * A + (int)((d >> 8) & 255) * ia) / 255;
            int ob = (b * A + (int)(d & 255) * ia) / 255;
            int oa = A + (int)(d >> 24) * ia / 255;
            *px = ((uint)oa << 24) | ((uint)or << 16) | ((uint)og << 8) | (uint)ob;
        }

        void Dot(float cx, float cy, float r, float alpha, int cr, int cg, int cb)
        {
            if (r < 0.6f) { alpha *= r / 0.6f; }
            float bx = cx - vL, by = cy - vT;
            int x0 = (int)Math.Floor(bx - r - 1), x1 = (int)Math.Ceiling(bx + r + 1);
            int y0 = (int)Math.Floor(by - r - 1), y1 = (int)Math.Ceiling(by + r + 1);
            if (x0 < 0) x0 = 0; if (y0 < 0) y0 = 0;
            if (x1 > vW) x1 = vW; if (y1 > vH) y1 = vH;
            for (int y = y0; y < y1; y++)
            {
                float dy = y + 0.5f - by;
                uint* row = buf + (long)y * vW;
                for (int x = x0; x < x1; x++)
                {
                    float dx = x + 0.5f - bx;
                    float cov = r + 0.5f - (float)Math.Sqrt(dx * dx + dy * dy);
                    if (cov <= 0) continue;
                    if (cov > 1) cov = 1;
                    Blend(row + x, cr, cg, cb, cov * alpha);
                }
            }
        }

        // Снежинка с коротким «хвостом», когда её несёт сильный ветер.
        void Particle(float x, float y, float r, float a, float hv, int cr, int cg, int cb)
        {
            float sp = Math.Abs(hv);
            if (sp > 250f * S)
            {
                float len = Math.Min(40f * S, sp * 0.03f), dir = hv > 0 ? -1f : 1f;
                for (int k = 4; k >= 1; k--)
                    Dot(x + dir * len * k / 4f, y, r * 0.85f, a * (1f - k / 5f) * 0.55f, cr, cg, cb);
            }
            Dot(x, y, r, a, cr, cg, cb);
        }

        bool BehindWindow(float x, float y)
        {
            int xi = (int)x, yi = (int)y;
            for (int i = 0; i < z.Count; i++)
            {
                Surface o = z[i];
                if (o.Kind == 2) continue;
                if (xi >= o.L && xi < o.R && yi >= o.T && yi < o.B) return true;
            }
            return false;
        }

        static uint Hash(int x)
        {
            uint h = (uint)x * 2654435761u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return h;
        }

        void DrawPile(Surface s)
        {
            float[] h = s.H, bs = s.Base; int n = h.Length, occN = s.Occ.Count;
            for (int c = 0; c < n; c++)
            {
                float hh = h[c];
                if (hh < 0.08f) continue;
                int x = s.L + c, bx = x - vL;
                if (bx < 0 || bx >= vW) continue;
                float bot = s.T + bs[c];          // кромка окна в этом столбце (с учётом скругления)
                float ytop = bot - hh;
                int by0 = (int)Math.Floor(ytop) - vT, by1 = (int)Math.Ceiling(bot) - vT;
                if (by0 < 0) by0 = 0; if (by1 > vH) by1 = vH;
                if (by0 >= by1) continue;

                int k = 0;
                for (int j = 0; j < occN && k < 64; j++)
                {
                    Surface o = z[s.Occ[j]];
                    if (x >= o.L && x < o.R) { oT[k] = o.T; oB[k] = o.B; k++; }
                }
                float hl = c > 0 ? h[c - 1] - bs[c - 1] : -bs[c], hr = c < n - 1 ? h[c + 1] - bs[c + 1] : -bs[c];
                float light = 1f + Clamp((hr - hl) * 0.5f, -2f, 2f) * 0.06f;
                bool sparkle = Hash(x) % 47 == 0 && Math.Sin(t * 2.3 + x * 0.37) > 0.88;

                for (int by = by0; by < by1; by++)
                {
                    int y = by + vT;
                    bool hid = false;
                    for (int q = 0; q < k; q++) if (y >= oT[q] && y < oB[q]) { hid = true; break; }
                    if (hid) continue;
                    float cov = Math.Min(y + 1f, bot) - Math.Max((float)y, ytop);
                    if (cov <= 0) continue;
                    if (cov > 1) cov = 1;
                    float dd = y - ytop; if (dd < 0) dd = 0;
                    float g = dd / 16f; if (g > 1) g = 1; g *= 0.8f;
                    float lm = dd < 2f ? light : 1f + (light - 1f) * 0.4f;
                    if (bot - y <= 1.5f) lm *= 0.92f;
                    int cr, cg, cb;
                    if (sparkle && dd < 1.2f) { cr = 255; cg = 255; cb = 255; }
                    else
                    {
                        cr = Math.Min(255, (int)((246f - 44f * g) * lm));
                        cg = Math.Min(255, (int)((249f - 32f * g) * lm));
                        cb = Math.Min(255, (int)((255f - 14f * g) * lm));
                    }
                    Blend(buf + (long)by * vW + bx, cr, cg, cb, cov * 0.97f);
                }
            }
        }

        void Render()
        {
            Clear();
            if (C.Drifts) for (int i = z.Count - 1; i >= 0; i--) if (z[i].Enabled) DrawPile(z[i]);
            float rMul = 1f - 0.35f * calm, aMul = 1f - 0.5f * calm;
            bool front = C.Front;
            for (int i = 0; i < pc; i++)
            {
                if (!front && BehindWindow(ps[i].x, ps[i].y)) continue;   // снег только на рабочем столе
                if (ps[i].clump) Particle(ps[i].x, ps[i].y, ps[i].r, 0.95f, ps[i].hv, 244, 248, 255);
                else
                {
                    float d = ps[i].d;
                    Particle(ps[i].x, ps[i].y, ps[i].r * rMul, ps[i].a * aMul, ps[i].hv, (int)(222 + 33 * d), (int)(232 + 23 * d), 255);
                }
            }
            Present();
        }

        bool IsMaximizedFg()
        {
            IntPtr fg = N.GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == ov.Handle) return false;
            return (N.GetWindowLong(fg, -16) & 0x01000000) != 0;   // WS_MAXIMIZE
        }

        bool IsFullscreenFg()
        {
            IntPtr fg = N.GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == ov.Handle) return false;
            sb.Length = 0; N.GetClassName(fg, sb, sb.Capacity);
            string cls = sb.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") return false;
            // системные полноэкранные «невидимки»: меню раскладок, Alt+Tab, представление задач, Пуск
            if (cls == "XamlExplorerHostIslandWindow" || cls == "MultitaskingViewFrame" || cls == "ForegroundStaging" ||
                cls == "Windows.UI.Core.CoreWindow" || cls == "TaskSwitcherWnd") return false;
            if ((N.GetWindowLong(fg, -20) & 0x80) != 0) return false;        // WS_EX_TOOLWINDOW — не игра
            int cloaked;
            if (N.DwmGetWindowAttribute(fg, 14, out cloaked, 4) == 0 && cloaked != 0) return false;
            if ((N.GetWindowLong(fg, -16) & 0x01000000) != 0) return false;   // WS_MAXIMIZE: обычное развёрнутое окно, не игра
            N.RECT r;
            if (!N.GetWindowRect(fg, out r)) return false;
            foreach (N.RECT m in mons)
                if (r.L <= m.L && r.T <= m.T && r.R >= m.R && r.B >= m.B) return true;
            return false;
        }

        // ---------- трей, окно настроек ----------
        public static void DrawFlake(Graphics g, float size)
        {
            DrawFlake(g, size, Color.FromArgb(255, 28, 78, 160), Color.White);
        }

        public static void DrawFlake(Graphics g, float size, Color outline, Color fill)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float c = size / 2f, R = size * 0.43f;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0 && outline.A == 0) continue;
                using (Pen pen = pass == 0 ? new Pen(outline, size * 0.17f) : new Pen(fill, size * 0.075f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    for (int k = 0; k < 6; k++)
                    {
                        double a = k * Math.PI / 3 - Math.PI / 2;
                        float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
                        g.DrawLine(pen, c, c, c + dx * R, c + dy * R);
                        float bx = c + dx * R * 0.58f, by = c + dy * R * 0.58f, bl = R * 0.3f;
                        for (int sgn = -1; sgn <= 1; sgn += 2)
                        {
                            double ba = a + sgn * Math.PI / 4;
                            g.DrawLine(pen, bx, by, bx + (float)Math.Cos(ba) * bl, by + (float)Math.Sin(ba) * bl);
                        }
                    }
                }
            }
        }

        public static void ExportIcon(string path)
        {
            using (Bitmap bmp = new Bitmap(256, 256, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); DrawFlake(g, 256); }
                using (MemoryStream png = new MemoryStream())
                {
                    bmp.Save(png, ImageFormat.Png);
                    byte[] data = png.ToArray();
                    using (BinaryWriter w = new BinaryWriter(File.Create(path)))
                    {
                        w.Write((short)0); w.Write((short)1); w.Write((short)1);
                        w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
                        w.Write((short)1); w.Write((short)32); w.Write(data.Length); w.Write(22);
                        w.Write(data);
                    }
                }
            }
        }

        Icon MakeTrayIcon()
        {
            int sz = Math.Max(16, (int)(16 * S));
            Bitmap bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); DrawFlake(g, sz); }
            return Icon.FromHandle(bmp.GetHicon());
        }

        void BuildTray()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            miSettings = new ToolStripMenuItem("", null, delegate { ShowSettings(); });
            miSettings.Font = new Font(miSettings.Font, FontStyle.Bold);
            menu.Items.Add(miSettings);
            menu.Items.Add(new ToolStripSeparator());
            miSnowing = new ToolStripMenuItem("", null, delegate { C.Snowing = !C.Snowing; Apply(); });
            menu.Items.Add(miSnowing);
            miBlow = new ToolStripMenuItem("", null, delegate { StartGust(); });
            menu.Items.Add(miBlow);
            menu.Items.Add(new ToolStripSeparator());
            miQuit = new ToolStripMenuItem("", null, delegate { ExitApp(); });
            menu.Items.Add(miQuit);

            tray = new NotifyIcon();
            tray.Icon = MakeTrayIcon();
            tray.Text = "SnowDream";
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowSettings(); };
            tray.Visible = true;
            Apply();
        }

        public void ShowSettings()
        {
            if (settings == null || settings.IsDisposed) settings = new SettingsForm(this);
            if (!settings.Visible) settings.Show();
            if (settings.WindowState == FormWindowState.Minimized) settings.WindowState = FormWindowState.Normal;
            // фоновому процессу Windows не даёт выйти на передний план — временно делаем окно «поверх всех»
            settings.TopMost = true;
            settings.Activate();
            settings.TopMost = false;
            settings.BringToFront();
        }

        // Вызывается после любого изменения настроек.
        public void Apply()
        {
            if (miSnowing != null)
            {
                miSnowing.Checked = C.Snowing;
                miSettings.Text = L.T("Настройки…", "Settings…");
                miSnowing.Text = L.T("Снегопад идёт", "Snow is falling");
                miBlow.Text = L.T("Сдуть весь снег", "Blow all snow away");
                miQuit.Text = L.T("Выход", "Quit");
            }
            C.Save();
            if (settings != null && !settings.IsDisposed) settings.Invalidate();
        }

        void ShakeAll()
        {
            foreach (Surface s in surf.Values) Collapse(s, 1f, 0, 0, 0);
            foreach (Surface g in grounds) Collapse(g, 1f, 0, 0, 0);
        }

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool GetAutostart()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && (k.GetValue("SnowDream") != null || k.GetValue("DesktopSnow") != null);
        }
        public static void SetAutostart(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                k.DeleteValue("DesktopSnow", false);   // запись старой версии
                if (on) k.SetValue("SnowDream", "\"" + Application.ExecutablePath + "\" --autostart");
                else k.DeleteValue("SnowDream", false);
            }
        }

        public void ExitApp()
        {
            if (!running) return;
            running = false;
            Application.Idle -= OnIdle;
            if (settings != null && !settings.IsDisposed) settings.Close();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            if (ov != null) ov.DestroyHandle();
            if (hBmp != IntPtr.Zero) { N.SelectObject(memDC, oldBmp); N.DeleteObject(hBmp); hBmp = IntPtr.Zero; }
            N.DeleteDC(memDC);
            N.ReleaseDC(IntPtr.Zero, screenDC);
            N.timeEndPeriod(1);
            ExitThread();
        }
    }

    static class Program
    {
        static SnowApp app;

        static void Log(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Config.Dir);
                File.AppendAllText(Path.Combine(Config.Dir, "error.log"), DateTime.Now + "\r\n" + ex + "\r\n\r\n");
            }
            catch { }
        }

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--export-icon") { SnowApp.ExportIcon(args[1]); return; }
            try { if (!N.SetProcessDpiAwarenessContext(new IntPtr(-4))) N.SetProcessDPIAware(); }
            catch { try { N.SetProcessDPIAware(); } catch { } }

            bool created;
            using (Mutex mx = new Mutex(true, "SnowDream_SingleInstance", out created))
            using (EventWaitHandle showEvt = new EventWaitHandle(false, EventResetMode.AutoReset, "SnowDream_ShowSettings"))
            using (EventWaitHandle blowEvt = new EventWaitHandle(false, EventResetMode.AutoReset, "SnowDream_Blow"))
            {
                bool blow = Array.IndexOf(args, "--blow") >= 0;
                if (!created)
                {
                    // программа уже запущена: «--blow» сдувает снег, иначе открываем настройки
                    if (blow) blowEvt.Set(); else showEvt.Set();
                    return;
                }
                bool autostart = Array.IndexOf(args, "--autostart") >= 0;
                SnowApp.Diag = Array.IndexOf(args, "--diag") >= 0;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
                {
                    Log(e.Exception);
                    if (app != null) app.ExitApp();
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
                {
                    Exception ex = e.ExceptionObject as Exception;
                    if (ex != null) Log(ex);
                };
                app = new SnowApp(showEvt, blowEvt, !autostart && !blow);
                if (blow) app.StartGust();
                Application.Run(app);
            }
        }
    }
}
