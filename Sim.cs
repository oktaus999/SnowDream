// SnowDream: экран, окна и физика сугробов (работает в потоке рендера).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DesktopSnow
{
    unsafe partial class SnowApp
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

        // ---------- состояние потока рендера ----------
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
        double lastTime, t, lastSlow, lastFs;
        float wind, spawnAcc, gustAcc;
        float calm, calmTarget;      // 0 — обычный снег, 1 — тихий режим (развёрнутое окно)
        float gustT = -1f, gust;     // порыв ветра: время с начала и текущая сила 0..1
        float gustSgn = 1f;          // куда дует порыв: +1 вправо, -1 влево
        float windBias;              // постоянная составляющая ветра (для запаса появления снежинок)
        int frame, framesCount;
        bool hidden, hiddenDrawn, driftsWas = true;
        Process proc = Process.GetCurrentProcess();
        TimeSpan lastCpu;

        float Rnd() { return (float)rng.NextDouble(); }
        static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }

        void InitRender()
        {
            driftsWas = C.Drifts;
            Surface.Scale = S;
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
            lastTime = sw.Elapsed.TotalSeconds;
            lastCpu = proc.TotalProcessorTime;
        }

        void ShutdownRender()
        {
            if (ov != null) { ov.DestroyHandle(); ov = null; }
            if (hBmp != IntPtr.Zero) { N.SelectObject(memDC, oldBmp); N.DeleteObject(hBmp); hBmp = IntPtr.Zero; }
            if (memDC != IntPtr.Zero) { N.DeleteDC(memDC); memDC = IntPtr.Zero; }
            if (screenDC != IntPtr.Zero) { N.ReleaseDC(IntPtr.Zero, screenDC); screenDC = IntPtr.Zero; }
        }

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
            dAn = 0; dBn = 0;   // новая поверхность уже чистая

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
            BuildIcicles();
        }

        bool OnMonitor(IntPtr hm, IntPtr hdc, ref N.RECT r, IntPtr d) { mons.Add(r); return true; }

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

        // Точка (x, y) закрыта каким-то окном выше поверхности с индексом idx?
        bool Covered(int idx, float x, float y)
        {
            for (int j = 0; j < idx; j++)
            {
                Surface o = z[j];
                if (x >= o.L && x < o.R && y >= o.T && y < o.B) return true;
            }
            return false;
        }

        void Remap(Surface s, int nl, int nt, int nr, int nb)
        {
            int nw = Math.Max(1, nr - nl);
            // снег, оказавшийся за новыми краями окна, отваливается глыбами
            CollapseRange(s, 0, nl - s.L, 1f, 0, 0, s.Id);
            CollapseRange(s, nr - s.L, s.H.Length, 1f, 0, 0, s.Id);
            float[] nh = new float[nw];
            for (int i = 0; i < s.H.Length; i++)
            {
                int j = s.L + i - nl;
                if (j >= 0 && j < nw) nh[j] = s.H[i];
            }
            s.H = nh; s.L = nl; s.T = nt; s.R = nr; s.B = nb;
            s.ComputeBase();
        }

        // Часть снега (frac) со всей поверхности отваливается глыбами (см. Chunks.cs).
        void Collapse(Surface s, float frac, float vx, float vy, int ign) { CollapseRange(s, 0, s.H.Length, frac, vx, vy, ign); }

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
            float[] h = s.H, cap = s.Cap; int n = h.Length;
            m *= s.Catch[c];   // в «ложбинах» рельефа снег задерживается охотнее
            float sum = (k + 1) * (k + 1);
            for (int o = -k; o <= k; o++)
            {
                int cc = c + o;
                if (cc < 0 || cc >= n) continue;
                float nv = h[cc] + m * (k + 1 - Math.Abs(o)) / sum;
                if (nv > cap[cc]) nv = Math.Max(h[cc], cap[cc]);
                h[cc] = nv;
            }
        }

        bool Blowing { get { return gustT >= 0 && gustT < 4.2f; } }

        // Фронт ветра идёт слева направо и поднимает в воздух весь снег, который накрыл.
        void BlowSweep()
        {
            // фронт ветра идёт в сторону порыва: слева направо или справа налево
            float prog = (gustT / 1.1f) * (vW + 100f);
            float front = gustSgn > 0 ? vL - 50f + prog : vL + vW + 50f - prog;
            foreach (Surface s in z) Blow(s, front);
            BlowIcicles(front);
        }

        void Blow(Surface s, float front)
        {
            float[] h = s.H; int n = h.Length;
            int i0, i1;   // какие столбцы уже накрыл фронт
            if (gustSgn > 0) { i0 = 0; i1 = (int)Math.Min(n, front - s.L); }
            else { i0 = Math.Max(0, (int)(front - s.L)); i0 -= i0 & 1; i1 = n; }
            for (int i = i0; i < i1; i += 2)
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
                          gustSgn * (300f + Rnd() * 500f) * S, -(80f + Rnd() * 260f) * S, area / cnt, 0);
            }
            s.SpillL = 0; s.SpillR = 0;
        }

        void ShakeAll()
        {
            foreach (Surface s in surf.Values) Collapse(s, 1f, 0, 0, 0);
            foreach (Surface g in grounds) Collapse(g, 1f, 0, 0, 0);
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
    }
}
