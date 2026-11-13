// SnowDream: управление приложением, трей и поток рендера.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DesktopSnow
{
    unsafe partial class SnowApp : ApplicationContext
    {
        public readonly Config C = new Config();
        public float S = 1f;
        public volatile float CpuPct, Fps;
        public int Particles { get { return pc; } }
        public bool Gusting { get { return gustT >= 0 || gustRequest; } }
        public Icon AppIcon;
        public static bool Diag;
        public static string SnapPath;   // --snapshot: сохранить кадр слоя снега в PNG (для отладки)
        bool snapDone;

        // запросы из UI-потока в поток рендера
        volatile bool gustRequest, fwRequest, running = true, renderFailed;

        Thread renderThread;
        NotifyIcon tray;
        ToolStripMenuItem miSnowing, miSettings, miBlow, miQuit;
        SettingsForm settings;
        EventWaitHandle showEvt, blowEvt, fwEvt;
        System.Windows.Forms.Timer uiTimer;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public SnowApp(EventWaitHandle showEvent, EventWaitHandle blowEvent, EventWaitHandle fwEvent, bool openSettings)
        {
            showEvt = showEvent; blowEvt = blowEvent; fwEvt = fwEvent;
            C.Load();
            L.C = C;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                    if (k != null && k.GetValue("DesktopSnow") != null) SetAutostart(true);   // автозапуск старой версии → SnowDream
            }
            catch { }
            try { S = N.GetDpiForSystem() / 96f; } catch { }
            if (S < 1f) S = 1f;
            try { AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // Windows 11 любит «экономить» на фоновых программах: просим не замедлять нас и не игнорировать точный таймер
            try
            {
                N.PPTS st = new N.PPTS(); st.Version = 1; st.ControlMask = 0x1 | 0x4; st.StateMask = 0;
                N.SetProcessInformation(N.GetCurrentProcess(), 4, ref st, 12);
            }
            catch { }

            BuildTray();

            renderThread = new Thread(RenderMain);
            renderThread.IsBackground = true;
            renderThread.Name = "SnowDream render";
            renderThread.Priority = ThreadPriority.AboveNormal;
            renderThread.Start();

            // UI-поток только слушает команды от других экземпляров и следит за потоком рендера
            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 200;
            uiTimer.Tick += delegate
            {
                if (showEvt != null && showEvt.WaitOne(0)) ShowSettings();
                if (blowEvt != null && blowEvt.WaitOne(0)) StartGust();
                if (fwEvt != null && fwEvt.WaitOne(0)) StartFireworks();
                if (renderFailed) ExitApp();
            };
            uiTimer.Start();

            if (openSettings) ShowSettings();
        }

        public void StartGust() { if (!Gusting) gustRequest = true; }
        public void StartFireworks() { fwRequest = true; }

        // ---------- поток рендера ----------
        void RenderMain()
        {
            IntPtr hTimer = IntPtr.Zero;
            try
            {
                N.timeBeginPeriod(1);
                try { hTimer = N.CreateWaitableTimerExW(IntPtr.Zero, null, 0x2, 0x1F0003); } catch { }   // высокоточный таймер (Win10 1803+)
                if (hTimer == IntPtr.Zero) try { hTimer = N.CreateWaitableTimerExW(IntPtr.Zero, null, 0, 0x1F0003); } catch { }
                IntPtr[] handles = { hTimer };

                InitRender();
                if (Diag) StartDiag();

                double nextFrame = sw.Elapsed.TotalSeconds;
                N.MSG msg;
                while (running)
                {
                    // окно оверлея живёт в этом потоке — честно разбираем его сообщения
                    while (N.PeekMessage(out msg, IntPtr.Zero, 0, 0, 1))
                    {
                        N.TranslateMessage(ref msg);
                        N.DispatchMessage(ref msg);
                    }
                    if (!running) break;

                    Frame();
                    dFrames++;

                    double target;
                    if (hidden) target = 0.2;
                    else if (pc == 0 && fc == 0 && !Gusting) target = 0.05;
                    else target = C.Perf == 0 ? 1.0 / 30 : (C.Perf == 1 ? 1.0 / 60 : 1.0 / refreshHz);
                    double now = sw.Elapsed.TotalSeconds;
                    nextFrame += target;
                    if (nextFrame < now) nextFrame = now;
                    double wait = nextFrame - now;
                    if (wait > 0.0005)
                    {
                        if (hTimer != IntPtr.Zero)
                        {
                            long due = -(long)(wait * 1e7);
                            N.SetWaitableTimer(hTimer, ref due, 0, IntPtr.Zero, IntPtr.Zero, false);
                            N.MsgWaitForMultipleObjects(1, handles, false, 1000, 0x4FF);
                        }
                        else N.MsgWaitForMultipleObjects(0, IntPtr.Zero, false, (uint)Math.Max(1, wait * 1000), 0x4FF);
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                renderFailed = true;
            }
            finally
            {
                try { ShutdownRender(); } catch { }
                if (hTimer != IntPtr.Zero) N.CloseHandle(hTimer);
                N.timeEndPeriod(1);
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
                CheckMidnight();
                if (SnapPath != null) hidden = false;
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
            if (gustRequest) { gustRequest = false; if (gustT < 0) { gustT = 0f; gustSgn = C.WindDir == 0 ? -1f : 1f; } }
            if (fwRequest) { fwRequest = false; fwShow = Math.Max(fwShow, 14f); }

            if (hidden)
            {
                if (!hiddenDrawn) { Clear(); dAn = 0; Present(); hiddenDrawn = true; }
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
            // «Авто» — ветер сам гуляет как раньше; «Влево»/«Вправо» — постоянный ветер в выбранную сторону
            float noise = wb * (0.55f * (float)Math.Sin(t * 0.05) + 0.3f * (float)Math.Sin(t * 0.17 + 1.3) + 0.15f * (float)Math.Sin(t * 0.61 + 2.1));
            float bias = C.WindDir == 1 ? WindBias[I] : (C.WindDir == 0 ? -1f : 1f) * (35f + WindBias[I]);
            if (C.WindDir != 1) noise *= 0.5f;
            windBias = bias * C.WindPower * S;
            wind = ((noise + bias * (1f - gust)) * C.WindPower + gustSgn * GustPower * gust) * S;

            UpdateCursor(dt);
            UpdateSurfaces(dt);
            UpdateIcicles(dt);
            if (gustT >= 0) BlowSweep();
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
            UpdateFireworks(dt);
            Render();
            if (SnapPath != null && !snapDone && t > 30) { snapDone = true; SaveSnapshot(SnapPath); }
        }

        // ---------- диагностика (запуск с --diag) ----------
        int dFrames, dUlwFail, dLastErr;
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
                    File.AppendAllText(f, string.Format("{0:HH:mm:ss.fff} frames={1} fps={2:0} ulwFail={3} err={4} hidden={5} calm={6:0.00} pc={7} fw={8} cpu={9:0.0}% fg={10}\r\n",
                        DateTime.Now, dFrames, Fps, dUlwFail, dLastErr, hidden, calm, pc, fc, CpuPct, c));
                }
                catch { }
            }, null, 1000, 1000);
        }

        // ---------- трей, окно настроек ----------
        public static void DrawFlake(Graphics g, float size)
        {
            DrawFlake(g, size, Color.FromArgb(255, 28, 78, 160), Color.White);
        }

        public static void DrawFlake(Graphics g, float size, Color outline, Color fill)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float c = size / 2f, R = size * 0.43f;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0 && outline.A == 0) continue;
                using (Pen pen = pass == 0 ? new Pen(outline, size * 0.17f) : new Pen(fill, size * 0.075f))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round; pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
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
            if (!running) return;
            if (settings == null || settings.IsDisposed) settings = new SettingsForm(this);
            if (!settings.Visible) settings.Show();
            if (settings.WindowState == FormWindowState.Minimized) settings.WindowState = FormWindowState.Normal;
            // фоновому процессу Windows не даёт выйти на передний план — временно делаем окно «поверх всех»
            settings.TopMost = true;
            settings.Activate();
            settings.TopMost = false;
            settings.BringToFront();
        }

        // Вызывается после любого изменения настроек (в UI-потоке).
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
            if (!running && renderThread == null) return;
            running = false;
            if (uiTimer != null) uiTimer.Stop();
            if (renderThread != null) { renderThread.Join(3000); renderThread = null; }
            if (diagTimer != null) diagTimer.Dispose();
            if (settings != null && !settings.IsDisposed) settings.Close();
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            ExitThread();
        }
    }
}
