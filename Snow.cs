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
        [StructLayout(LayoutKind.Sequential)] public struct PPTS { public uint Version, ControlMask, StateMask; }
        [DllImport("kernel32.dll")] public static extern bool SetProcessInformation(IntPtr p, int cls, ref PPTS info, int size);
        [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr CreateWaitableTimerExW(IntPtr attr, string name, uint flags, uint access);
        [DllImport("kernel32.dll")] public static extern bool SetWaitableTimer(IntPtr h, ref long due, int period, IntPtr cb, IntPtr arg, bool resume);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("user32.dll")] public static extern uint MsgWaitForMultipleObjects(uint n, IntPtr[] handles, bool all, uint ms, uint mask);
        [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG m);
        [DllImport("user32.dll")] public static extern IntPtr DispatchMessage(ref MSG m);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
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
        public float x, y, vx, vy, r, a, d, ph, fr, m, ignT, hv, ux, uy;
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

        // Рельеф: Catch — где снег копится охотнее, Cap — до какой высоты может вырасти сугроб.
        // Это плавные «волны» длиной в сотни пикселей, поэтому сугроб получается холмистым, но без зубцов.
        public float[] Catch, Cap;
        public static float Scale = 1f;
        static Random srnd = new Random();
        float[] ph;    // случайные фазы и длины волн этой поверхности

        void ComputeShape()
        {
            if (ph == null)
            {
                ph = new float[8];
                for (int i = 0; i < 5; i++) ph[i] = (float)(srnd.NextDouble() * 6.2832);
                ph[5] = (220f + (float)srnd.NextDouble() * 220f) * Scale;   // длинная волна: большие холмы
                ph[6] = (90f + (float)srnd.NextDouble() * 70f) * Scale;     // средняя
                ph[7] = (38f + (float)srnd.NextDouble() * 24f) * Scale;     // короткая, совсем слабая
            }
            int n = H.Length;
            Catch = new float[n]; Cap = new float[n];
            const float TAU = 6.2832f;
            for (int c = 0; c < n; c++)
            {
                float f = 0.5f + 0.27f * (float)Math.Sin(TAU * c / ph[5] + ph[0])
                               + 0.15f * (float)Math.Sin(TAU * c / ph[6] + ph[1])
                               + 0.07f * (float)Math.Sin(TAU * c / ph[7] + ph[2]);
                float g = 0.5f + 0.3f * (float)Math.Sin(TAU * c / (ph[5] * 1.37f) + ph[3])
                               + 0.2f * (float)Math.Sin(TAU * c / (ph[6] * 0.83f) + ph[4]);
                if (f < 0) f = 0; if (f > 1) f = 1;
                if (g < 0) g = 0; if (g > 1) g = 1;
                Catch[c] = 0.4f + 1.2f * f;
                Cap[c] = HMax * (0.45f + 0.55f * g);
            }
        }

        public void ComputeBase()
        {
            int n = H.Length;
            Base = new float[n];
            ComputeShape();
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
        public bool Cursor = true;     // курсор разгоняет снег и сбивает сугробы
        public bool Fireworks = true;  // салют 1 января в 00:00
        public float Size = 1f;        // множитель размера снежинок
        public bool Icicles = true;    // сосульки на верхнем краю экрана
        public float IceSpeed = 1f;    // скорость роста сосулек
        public int WindDir = 1;        // ветер: 0 влево, 1 авто, 2 вправо
        public float WindPower = 1f;   // сила ветра, 0 — снег падает строго вниз
        public int Theme = 0;          // тема окна настроек: 0 ночь, 1 ёлка, 2 мандарин, 3 радуга
        public int Style = 0;          // вид снежинок: 0 точки, 1 звёзды-кристаллы, 2 блоки, 3 пиксельные звёздочки

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
                        case "cursor": Cursor = b; break;
                        case "icicles": Icicles = b; break;
                        case "windDir": if (int.TryParse(v, out iv)) WindDir = Math.Max(0, Math.Min(2, iv)); break;
                        case "windPower": if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out fv)) WindPower = Math.Max(0f, Math.Min(3f, fv)); break;
                        case "theme": if (int.TryParse(v, out iv)) Theme = Math.Max(0, Math.Min(3, iv)); break;
                        case "style": if (int.TryParse(v, out iv)) Style = Math.Max(0, Math.Min(3, iv)); break;
                        case "iceSpeed": if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out fv)) IceSpeed = Math.Max(0.2f, Math.Min(3f, fv)); break;
                        case "fireworks": Fireworks = b; break;
                        case "size": if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out fv)) Size = Math.Max(0.5f, Math.Min(2f, fv)); break;
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
                sb.Append("cursor=").Append(Cursor ? 1 : 0).Append("\r\n");
                sb.Append("icicles=").Append(Icicles ? 1 : 0).Append("\r\n");
                sb.Append("windDir=").Append(WindDir).Append("\r\n");
                sb.Append("windPower=").Append(WindPower.ToString("0.0", CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("theme=").Append(Theme).Append("\r\n");
                sb.Append("style=").Append(Style).Append("\r\n");
                sb.Append("iceSpeed=").Append(IceSpeed.ToString("0.0", CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("fireworks=").Append(Fireworks ? 1 : 0).Append("\r\n");
                sb.Append("size=").Append(Size.ToString("0.0", CultureInfo.InvariantCulture)).Append("\r\n");
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

    static class Program
    {
        static SnowApp app;

        public static void Log(Exception ex)
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
            using (EventWaitHandle fwEvt = new EventWaitHandle(false, EventResetMode.AutoReset, "SnowDream_Fireworks"))
            {
                bool blow = Array.IndexOf(args, "--blow") >= 0;
                bool fireworks = Array.IndexOf(args, "--fireworks") >= 0;
                if (!created)
                {
                    // программа уже запущена: «--blow» сдувает снег, «--fireworks» запускает салют, иначе открываем настройки
                    if (blow) blowEvt.Set(); else if (fireworks) fwEvt.Set(); else showEvt.Set();
                    return;
                }
                bool autostart = Array.IndexOf(args, "--autostart") >= 0;
                SnowApp.Diag = Array.IndexOf(args, "--diag") >= 0;
                int si = Array.IndexOf(args, "--snapshot");
                if (si >= 0 && si + 1 < args.Length) SnowApp.SnapPath = args[si + 1];
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
                app = new SnowApp(showEvt, blowEvt, fwEvt, !autostart && !blow && !fireworks);
                if (blow) app.StartGust();
                if (fireworks) app.StartFireworks();
                Application.Run(app);
            }
        }
    }
}
