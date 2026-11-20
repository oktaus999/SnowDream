// Окно настроек: тёмное, минималистичное, все контролы нарисованы вручную.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace DesktopSnow
{
    static partial class Th
    {
        public static Color Bg = Color.FromArgb(21, 23, 28);
        public static Color Card = Color.FromArgb(30, 33, 40);
        public static Color Field = Color.FromArgb(40, 44, 53);
        public static Color Line = Color.FromArgb(48, 53, 64);
        public static Color Text = Color.FromArgb(234, 237, 243);
        public static Color Sub = Color.FromArgb(138, 146, 162);
        public static Color Accent = Color.FromArgb(140, 203, 255);
        public static Color AccentText = Color.FromArgb(10, 24, 38);
        public static Color Track = Color.FromArgb(55, 60, 72);
        public static Color Knob = Color.FromArgb(160, 168, 182);

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, RectangleF r, float rad, Color c)
        {
            using (GraphicsPath p = Round(r, rad)) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static void Text2(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h)
        {
            using (StringFormat sf = new StringFormat(StringFormatFlags.NoWrap))
            using (SolidBrush b = new SolidBrush(c))
            {
                sf.Alignment = h; sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(s, f, b, r, sf);
            }
        }

        public static float Approach(float cur, float target)
        {
            float d = target - cur;
            if (Math.Abs(d) < 0.004f) return target;
            return cur + d * 0.3f;
        }
    }

    abstract class Widget
    {
        public RectangleF B;
        public bool Hover, Down;
        public SettingsForm F;
        public virtual bool Clickable { get { return true; } }
        public abstract void Paint(Graphics g);
        public virtual void OnDown(PointF p) { }
        public virtual void OnMove(PointF p) { }
        public virtual void OnUp(PointF p, bool inside) { }
        public virtual bool Animate() { return false; }
        protected float s { get { return F.Sc; } }
    }

    class Toggle : Widget
    {
        string label, sub; Func<bool> get; Action<bool> set; float anim;
        public Toggle(string label, string sub, Func<bool> get, Action<bool> set)
        { this.label = label; this.sub = sub; this.get = get; this.set = set; anim = get() ? 1 : 0; }

        public override void Paint(Graphics g)
        {
            float sw = 40 * s, sh = 22 * s;
            RectangleF tr = new RectangleF(B.X, B.Y, B.Width - sw - 12 * s, B.Height);
            if (sub == null) Th.Text2(g, label, F.FText, Th.Text, tr, StringAlignment.Near);
            else
            {
                float mid = B.Y + B.Height / 2;
                Th.Text2(g, label, F.FText, Th.Text, new RectangleF(tr.X, mid - 19 * s, tr.Width, 20 * s), StringAlignment.Near);
                Th.Text2(g, sub, F.FSub, Th.Sub, new RectangleF(tr.X, mid + 1 * s, tr.Width, 18 * s), StringAlignment.Near);
            }
            RectangleF r = new RectangleF(B.Right - sw, B.Y + (B.Height - sh) / 2, sw, sh);
            Color bg = Th.Mix(Th.Track, Th.Accent, anim);
            if (Hover && anim < 0.5f) bg = Th.Mix(bg, Th.Text, 0.08f);
            Th.Fill(g, r, sh / 2, bg);
            float kd = 16 * s, kx = r.X + 3 * s + anim * (r.Width - kd - 6 * s);
            using (SolidBrush b = new SolidBrush(Th.Mix(Th.Knob, Color.White, anim)))
                g.FillEllipse(b, kx, r.Y + (sh - kd) / 2, kd, kd);
        }

        public override void OnUp(PointF p, bool inside) { if (inside) { set(!get()); F.Changed(); } }
        public override bool Animate() { float n = Th.Approach(anim, get() ? 1 : 0); bool ch = n != anim; anim = n; return ch; }
    }

    class Segmented : Widget
    {
        string[] opts; Func<int> get; Action<int> set; float anim; int hoverIdx = -1;
        public Segmented(string[] opts, Func<int> get, Action<int> set)
        { this.opts = opts; this.get = get; this.set = set; anim = get(); }

        int IndexAt(PointF p)
        {
            int i = (int)((p.X - B.X) / (B.Width / opts.Length));
            return Math.Max(0, Math.Min(opts.Length - 1, i));
        }

        public override void Paint(Graphics g)
        {
            Th.Fill(g, B, 10 * s, Th.Field);
            float segW = B.Width / opts.Length, pad = 3 * s;
            RectangleF pill = new RectangleF(B.X + pad + anim * segW, B.Y + pad, segW - 2 * pad, B.Height - 2 * pad);
            Th.Fill(g, pill, 8 * s, Th.Accent);
            for (int i = 0; i < opts.Length; i++)
            {
                float sel = Math.Max(0f, 1f - Math.Abs(i - anim));
                Color idle = (Hover && hoverIdx == i) ? Th.Text : Th.Sub;
                RectangleF r = new RectangleF(B.X + i * segW, B.Y, segW, B.Height);
                Th.Text2(g, opts[i], sel > 0.5f ? F.FTextB : F.FText, Th.Mix(idle, Th.AccentText, sel), r, StringAlignment.Center);
            }
        }

        public override void OnMove(PointF p) { int i = IndexAt(p); if (i != hoverIdx) { hoverIdx = i; F.Invalidate(); } }
        public override void OnUp(PointF p, bool inside) { if (inside) { set(IndexAt(p)); F.Changed(); } }
        public override bool Animate() { float n = Th.Approach(anim, get()); bool ch = n != anim; anim = n; return ch; }
    }

    class Slider : Widget
    {
        float min, max, step; Func<float> get; Action<float> set; bool drag;
        public Slider(float min, float max, float step, Func<float> get, Action<float> set)
        { this.min = min; this.max = max; this.step = step; this.get = get; this.set = set; }

        float Pad { get { return 10 * s; } }

        public override void Paint(Graphics g)
        {
            float cy = B.Y + B.Height / 2, x0 = B.X + Pad, x1 = B.Right - Pad;
            float tt = (get() - min) / (max - min), kx = x0 + tt * (x1 - x0);
            Th.Fill(g, new RectangleF(x0, cy - 2 * s, x1 - x0, 4 * s), 2 * s, Th.Track);
            Th.Fill(g, new RectangleF(x0, cy - 2 * s, kx - x0, 4 * s), 2 * s, Th.Accent);
            // отметка «1.0×» — обычная скорость
            float x1x = x0 + (1f - min) / (max - min) * (x1 - x0);
            using (SolidBrush b = new SolidBrush(Th.Sub)) g.FillEllipse(b, x1x - 2 * s, cy + 7 * s, 4 * s, 4 * s);
            if (Hover || drag)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(50, Th.Accent))) g.FillEllipse(b, kx - 13 * s, cy - 13 * s, 26 * s, 26 * s);
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, kx - 8 * s, cy - 8 * s, 16 * s, 16 * s);
        }

        void SetFrom(PointF p)
        {
            float tt = (p.X - (B.X + Pad)) / (B.Width - 2 * Pad);
            if (tt < 0) tt = 0; if (tt > 1) tt = 1;
            float v = min + tt * (max - min);
            v = (float)Math.Round(v / step) * step;
            set(Math.Max(min, Math.Min(max, v)));
            F.Invalidate();
        }

        public override void OnDown(PointF p) { drag = true; SetFrom(p); }
        public override void OnMove(PointF p) { if (drag) SetFrom(p); }
        public override void OnUp(PointF p, bool inside) { drag = false; F.Changed(); }
    }

    class Caption : Widget
    {
        Func<string> left, right; bool small;
        public Caption(Func<string> left, Func<string> right, bool small)
        { this.left = left; this.right = right; this.small = small; }
        public override bool Clickable { get { return false; } }
        public override void Paint(Graphics g)
        {
            Th.Text2(g, left(), small ? F.FSub : F.FText, small ? Th.Sub : Th.Text, B, StringAlignment.Near);
            if (right != null) Th.Text2(g, right(), F.FTextB, Th.Accent, B, StringAlignment.Far);
        }
    }

    class Btn : Widget
    {
        Func<string> text; Action act; bool primary; Func<bool> enabled;
        public Btn(Func<string> text, Action act, bool primary, Func<bool> enabled)
        { this.text = text; this.act = act; this.primary = primary; this.enabled = enabled; }

        bool En { get { return enabled == null || enabled(); } }

        public bool Secondary;   // спокойная кнопка внутри карточки

        public override void Paint(Graphics g)
        {
            if (Secondary)
            {
                Color c = Th.Field;
                if (!En) c = Th.Card;
                else if (Down) c = Th.Mix(Th.Field, Color.Black, 0.15f);
                else if (Hover) c = Th.Mix(Th.Field, Th.Text, 0.08f);
                Th.Fill(g, B, 10 * s, c);
                Th.Text2(g, text(), F.FTextB, En ? Th.Text : Th.Sub, B, StringAlignment.Center);
            }
            else if (primary)
            {
                // у тем бывает градиент или радуга на главной кнопке
                Color toward = !En ? Th.Card : (Down ? Color.Black : (Hover ? Color.White : Th.Accent));
                float amt = !En ? 0.55f : (Down ? 0.12f : (Hover ? 0.18f : 0f));
                Th.FillAccent(g, B, 12 * s, toward, amt);
                string tx = text();
                SizeF ts = g.MeasureString(tx, F.FBtn);
                float iw = 22 * s, gap = 10 * s, total = iw + gap + ts.Width;
                float x = B.X + (B.Width - total) / 2, cy = B.Y + B.Height / 2;
                DrawWind(g, x, cy, iw, Th.AccentText);
                Th.Text2(g, tx, F.FBtn, Th.AccentText, new RectangleF(x + iw + gap, B.Y, ts.Width + 4, B.Height), StringAlignment.Near);
            }
            else
            {
                if (Hover) Th.Fill(g, B, 8 * s, Th.Field);
                Th.Text2(g, text(), F.FSub, Hover ? Th.Text : Th.Sub, B, StringAlignment.Center);
            }
        }

        void DrawWind(Graphics g, float x, float cy, float w, Color c)
        {
            using (Pen p = new Pen(c, 2.2f * s))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                float r = 3.5f * s;
                // верхняя линия с завитком
                g.DrawLine(p, x, cy - 5 * s, x + w * 0.62f, cy - 5 * s);
                g.DrawArc(p, x + w * 0.62f - r, cy - 5 * s - 2 * r, 2 * r, 2 * r, 90, -270);
                // средняя
                g.DrawLine(p, x + 2 * s, cy + 1 * s, x + w * 0.95f, cy + 1 * s);
                // нижняя с завитком вниз
                g.DrawLine(p, x, cy + 7 * s, x + w * 0.5f, cy + 7 * s);
                g.DrawArc(p, x + w * 0.5f - r, cy + 7 * s, 2 * r, 2 * r, 270, 270);
            }
        }

        public override void OnUp(PointF p, bool inside) { if (inside && En) { act(); F.Invalidate(); } }
    }

    // «Плашка» в шапке с живым отсчётом до Нового года, прижата к правому краю B.
    class Chip : Widget
    {
        Func<string> text;
        public Chip(Func<string> text) { this.text = text; }
        public override bool Clickable { get { return false; } }
        public override void Paint(Graphics g)
        {
            string tx = text();
            SizeF ts = g.MeasureString(tx, F.FTextB);
            float w = ts.Width + 26 * s;
            RectangleF r = new RectangleF(B.Right - w, B.Y, w, B.Height);
            Th.Fill(g, r, B.Height / 2, Th.Field);
            using (SolidBrush b = new SolidBrush(Th.Accent)) g.FillEllipse(b, r.X + 11 * s, r.Y + B.Height / 2 - 3 * s, 6 * s, 6 * s);
            Th.Text2(g, tx, F.FTextB, Th.Text, new RectangleF(r.X + 22 * s, r.Y, ts.Width + 4, r.Height), StringAlignment.Near);
        }
    }

    class CloseBtn : Widget
    {
        public override void Paint(Graphics g)
        {
            if (Hover) Th.Fill(g, B, 8 * s, Th.Field);
            float cx = B.X + B.Width / 2, cy = B.Y + B.Height / 2, d = 5 * s;
            using (Pen p = new Pen(Hover ? Th.Text : Th.Sub, 1.6f * s))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - d, cy - d, cx + d, cy + d);
                g.DrawLine(p, cx - d, cy + d, cx + d, cy - d);
            }
        }
        public override void OnUp(PointF p, bool inside) { if (inside) F.Close(); }
    }

    class SettingsForm : Form
    {
        public readonly SnowApp App;
        public readonly float Sc;
        public Font FTitle, FText, FTextB, FSub, FCap, FBtn;

        class CardInfo { public RectangleF R; public string Title; public bool Compact; }

        List<Widget> ws = new List<Widget>();
        List<CardInfo> cards = new List<CardInfo>();
        Widget hot, pressed;
        Timer tm;
        float headerH;
        int tick;

        // построитель раскладки
        float cx, cy, cw, cardY; string cardTitle;

        public SettingsForm(SnowApp app)
        {
            App = app; Sc = app.S;
            Text = "SnowDream";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            Th.Apply(app.C.Theme);
            BackColor = Th.Bg;
            DoubleBuffered = true;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            if (app.AppIcon != null) Icon = app.AppIcon;

            FTitle = new Font("Segoe UI Semibold", 17 * Sc, FontStyle.Regular, GraphicsUnit.Pixel);
            FText = new Font("Segoe UI", 13.5f * Sc, FontStyle.Regular, GraphicsUnit.Pixel);
            FTextB = new Font("Segoe UI Semibold", 13.5f * Sc, FontStyle.Regular, GraphicsUnit.Pixel);
            FSub = new Font("Segoe UI", 12 * Sc, FontStyle.Regular, GraphicsUnit.Pixel);
            FCap = new Font("Segoe UI Semibold", 11 * Sc, FontStyle.Regular, GraphicsUnit.Pixel);
            FBtn = new Font("Segoe UI Semibold", 14.5f * Sc, FontStyle.Regular, GraphicsUnit.Pixel);

            Build();

            tm = new Timer();
            tm.Interval = 16;
            tm.Tick += delegate
            {
                bool any = false;
                foreach (Widget w in ws) if (w.Animate()) any = true;
                if (++tick % 30 == 0) any = true;   // раз в ~0.5 с обновляем статистику
                if (Th.Decor == 3 && tick % 3 == 0) any = true;   // мерцание звёзд и радуги
                if (any) Invalidate();
            };
            tm.Start();
        }

        float P(float v) { return v * Sc; }

        const string DonateUrl = "https://www.donationalerts.com/r/oktau7";

        // Открывает страницу поддержки автора в браузере по умолчанию.
        static void OpenDonate()
        {
            try { System.Diagnostics.Process.Start(DonateUrl); } catch { }
        }

        // «До Нового года 48 дн 07:12:33»
        static string Countdown()
        {
            DateTime now = DateTime.Now;
            if (now.Month == 1 && now.Day == 1) return L.T("С Новым годом!", "Happy New Year!");
            TimeSpan d = new DateTime(now.Year + 1, 1, 1) - now;
            return string.Format(L.T("До Нового года {0} дн {1:00}:{2:00}:{3:00}", "New Year in {0}d {1:00}:{2:00}:{3:00}"),
                                 d.Days, d.Hours, d.Minutes, d.Seconds);
        }

        // десятичный разделитель: «0,6» по-русски, «0.6» по-английски
        static IFormatProvider Num
        {
            get { return L.En ? System.Globalization.CultureInfo.InvariantCulture : new System.Globalization.CultureInfo("ru-RU"); }
        }

        void Add(Widget w, float x, float y, float wd, float h)
        {
            w.F = this; w.B = new RectangleF(x, y, wd, h); ws.Add(w);
        }

        void BeginCard(float x, float y, float w, string title)
        {
            cx = x; cardY = y; cw = w; cardTitle = title;
            cy = y + P(44);
        }
        float EndCard()
        {
            float bottom = cy + P(8);
            CardInfo c = new CardInfo();
            c.R = new RectangleF(cx, cardY, cw, bottom - cardY); c.Title = cardTitle;
            cards.Add(c);
            return bottom;
        }
        void RowToggle(string label, string sub, Func<bool> get, Action<bool> set)
        {
            float h = P(sub == null ? 36 : 46);
            Add(new Toggle(label, sub, get, set), cx + P(18), cy, cw - P(36), h);
            cy += h + P(6);
        }
        void RowCaption(Func<string> left, Func<string> right, bool small)
        {
            Add(new Caption(left, right, small), cx + P(18), cy, cw - P(36), P(22));
            cy += P(28);
        }
        void RowSegmented(string[] opts, Func<int> get, Action<int> set)
        {
            Add(new Segmented(opts, get, set), cx + P(18), cy, cw - P(36), P(36));
            cy += P(36 + 14);
        }
        void RowSlider(float min, float max, float step, Func<float> get, Action<float> set)
        {
            Add(new Slider(min, max, step, get, set), cx + P(10), cy, cw - P(20), P(26));
            cy += P(26 + 12);
        }

        // Пересобрать окно (например, после смены языка).
        void Rebuild()
        {
            ws.Clear(); cards.Clear();
            hot = null; pressed = null;
            Cursor = Cursors.Default;
            Build();
            Invalidate();
        }

        void Build()
        {
            Config C = App.C;
            float pad = P(20), gap = P(14), colW = P(322), W = 3 * colW + 2 * gap + 2 * pad;
            headerH = P(74);
            Add(new CloseBtn(), W - pad - P(34), P(20), P(34), P(34));

            // кружки выбора темы — посередине шапки
            Add(new ThemeDots(), pad + P(300), P(21), P(4 * 29 + 130), P(32));

            // язык — компактная плашка в шапке, рядом отсчёт до Нового года
            float langW = P(116), langX = W - pad - P(34) - P(12) - langW;
            Add(new Segmented(new[] { "RU", "EN" }, delegate { return C.Lang; },
                              delegate(int v) { if (C.Lang != v) { C.Lang = v; BeginInvoke(new MethodInvoker(Rebuild)); } }),
                langX, P(22), langW, P(30));
            Add(new Chip(Countdown), langX - P(12) - P(330), P(23), P(330), P(28));

            // ---- колонка 1 ----
            float x1 = pad, x2 = pad + colW + gap, x3 = pad + 2 * (colW + gap), y = headerH;
            BeginCard(x1, y, colW, L.T("СНЕГОПАД", "SNOWFALL"));
            RowToggle(L.T("Снегопад идёт", "Snow is falling"), null, delegate { return C.Snowing; }, delegate(bool v) { C.Snowing = v; });
            RowCaption(delegate { return L.T("Сила", "Intensity"); }, null, false);
            RowSegmented(L.En ? new[] { "Light", "Snowfall", "Blizzard" } : new[] { "Лёгкий", "Снегопад", "Метель" },
                         delegate { return C.Intensity; }, delegate(int v) { C.Intensity = v; });
            RowCaption(delegate { return L.T("Вид снежинок", "Flake style"); }, null, false);
            RowSegmented(L.En ? new[] { "Dots", "Stars", "Blocks", "Pixels" } : new[] { "Точки", "Звёзды", "Блоки", "Пиксели" },
                         delegate { return C.Style; }, delegate(int v) { C.Style = v; });
            RowCaption(delegate { return L.T("Скорость падения", "Fall speed"); }, delegate { return C.Speed.ToString("0.0", Num) + "×"; }, false);
            RowSlider(0.3f, 2.5f, 0.1f, delegate { return C.Speed; }, delegate(float v) { C.Speed = v; });
            RowCaption(delegate { return L.T("Размер снежинок", "Flake size"); }, delegate { return C.Size.ToString("0.0", Num) + "×"; }, false);
            RowSlider(0.5f, 2f, 0.1f, delegate { return C.Size; }, delegate(float v) { C.Size = v; });
            RowCaption(delegate { return L.T("Ветер", "Wind"); }, null, false);
            RowSegmented(L.En ? new[] { "← Left", "Auto", "Right →" } : new[] { "← Влево", "Авто", "Вправо →" },
                         delegate { return C.WindDir; }, delegate(int v) { C.WindDir = v; });
            RowCaption(delegate { return L.T("Сила ветра", "Wind strength"); }, delegate { return C.WindPower.ToString("0.0", Num) + "×"; }, false);
            RowSlider(0f, 3f, 0.1f, delegate { return C.WindPower; }, delegate(float v) { C.WindPower = v; });
            float b1 = EndCard(); int last1 = cards.Count - 1;

            // ---- колонка 2 ----
            y = headerH;
            BeginCard(x2, y, colW, L.T("КУДА ПАДАЕТ СНЕГ", "WHERE SNOW FALLS"));
            RowToggle(L.T("Поверх окон", "Over windows"), L.T("Выкл — только на рабочем столе", "Off — desktop only"),
                      delegate { return C.Front; }, delegate(bool v) { C.Front = v; });
            RowToggle(L.T("На окна", "On windows"), L.T("Сугробы на верхних краях окон", "Drifts on top of windows"),
                      delegate { return C.Drifts; }, delegate(bool v) { C.Drifts = v; });
            RowToggle(L.T("На панель задач", "On the taskbar"), L.T("Сугроб на панели внизу", "A drift on the bottom bar"),
                      delegate { return C.Taskbar; }, delegate(bool v) { C.Taskbar = v; });
            y = EndCard() + gap;

            BeginCard(x2, y, colW, L.T("СОСУЛЬКИ", "ICICLES"));
            RowToggle(L.T("Сосульки", "Icicles"), L.T("Растут на верхнем краю экрана", "Grow on the top edge of the screen"),
                      delegate { return C.Icicles; }, delegate(bool v) { C.Icicles = v; });
            RowCaption(delegate { return L.T("Скорость роста", "Growth speed"); }, delegate { return C.IceSpeed.ToString("0.0", Num) + "×"; }, false);
            RowSlider(0.2f, 3f, 0.1f, delegate { return C.IceSpeed; }, delegate(float v) { C.IceSpeed = v; });
            y = EndCard() + gap;

            BeginCard(x2, y, colW, L.T("РАЗВЁРНУТОЕ ОКНО", "MAXIMIZED WINDOW"));
            RowCaption(delegate { return L.T("Когда окно открыто на весь экран", "When a window fills the screen"); }, null, true);
            RowSegmented(L.En ? new[] { "Normal", "Quiet", "Hide" } : new[] { "Как обычно", "Тихо", "Скрыть" },
                         delegate { return C.MaxMode; }, delegate(int v) { C.MaxMode = v; });
            float b2 = EndCard(); int last2 = cards.Count - 1;

            // ---- колонка 3 ----
            y = headerH;
            BeginCard(x3, y, colW, L.T("НОВЫЙ ГОД", "NEW YEAR"));
            RowToggle(L.T("Салют в полночь", "Fireworks at midnight"), L.T("1 января в 00:00", "January 1st, 00:00"),
                      delegate { return C.Fireworks; }, delegate(bool v) { C.Fireworks = v; });
            Btn fwb = new Btn(delegate { return L.T("Запустить салют сейчас", "Launch fireworks now"); }, delegate { App.StartFireworks(); }, false, null);
            fwb.Secondary = true;
            Add(fwb, cx + P(18), cy, cw - P(36), P(38));
            cy += P(38 + 8);
            y = EndCard() + gap;

            BeginCard(x3, y, colW, L.T("НАГРУЗКА", "PERFORMANCE"));
            RowCaption(delegate { return L.T("Частота кадров и количество снега", "Frame rate and amount of snow"); }, null, true);
            RowSegmented(L.En ? new[] { "Eco", "Balanced", "Smooth" } : new[] { "Эко", "Баланс", "Плавно" },
                         delegate { return C.Perf; }, delegate(int v) { C.Perf = v; });
            RowCaption(delegate
            {
                return string.Format(Num, L.T("ЦП {0:0.0}%   ·   {1:0} кадр/с   ·   {2} снежинок", "CPU {0:0.0}%   ·   {1:0} fps   ·   {2} flakes"),
                                     App.CpuPct, App.Fps, App.Particles);
            }, null, true);
            y = EndCard() + gap;

            BeginCard(x3, y, colW, L.T("ПРОЧЕЕ", "OTHER"));
            RowToggle(L.T("Курсор разгоняет снег", "Cursor scatters snow"), L.T("Снежинки облетают мышку", "Flakes swirl around the mouse"),
                      delegate { return C.Cursor; }, delegate(bool v) { C.Cursor = v; });
            RowToggle(L.T("Прятать в играх", "Hide in games"), L.T("И в полноэкранном видео", "And in fullscreen video"),
                      delegate { return C.HideFs; }, delegate(bool v) { C.HideFs = v; });
            RowToggle(L.T("Запускать с Windows", "Start with Windows"), null,
                      delegate { return SnowApp.GetAutostart(); }, delegate(bool v) { SnowApp.SetAutostart(v); });
            float b3 = EndCard(); int last3 = cards.Count - 1;

            // выравниваем низ колонок: последнюю карточку каждой колонки вытягиваем до общей линии
            float maxB = Math.Max(b1, Math.Max(b2, b3));
            cards[last1].R.Height += maxB - b1;
            cards[last2].R.Height += maxB - b2;
            cards[last3].R.Height += maxB - b3;

            float by = maxB + P(16);
            Add(new Btn(delegate { return App.Gusting ? L.T("Ветер дует…", "Wind is blowing…") : L.T("Сдуть весь снег", "Blow all snow away"); },
                        delegate { App.StartGust(); }, true, delegate { return !App.Gusting; }),
                pad, by, W - 2 * pad, P(50));
            by += P(50 + 8);
            Add(new Btn(delegate { return L.T("Выйти из программы", "Quit SnowDream"); }, delegate { App.ExitApp(); }, false, null),
                W - pad - P(170), by, P(170), P(32));
            Add(new Btn(delegate { return L.T("♥ Поддержать автора", "♥ Support the author"); }, OpenDonate, false, null),
                W - pad - P(170) - P(8) - P(190), by, P(190), P(32));
            Add(new Caption(delegate { return L.T("Клик по снежинке в трее открывает это окно", "Click the snowflake in the tray to open this window"); }, null, true),
                pad + P(4), by, P(440), P(32));
            by += P(32 + 14);

            ClientSize = new Size((int)W, (int)by);
        }

        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; }   // тень окна
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2; N.DwmSetWindowAttribute(Handle, 33, ref round, 4);   // скруглённые углы (Win11)
                int dark = 1; N.DwmSetWindowAttribute(Handle, 20, ref dark, 4);     // тёмная рамка
            }
            catch { }
        }

        public void Changed() { App.Apply(); Invalidate(); }

        public void SetTheme(int theme)
        {
            App.C.Theme = theme;
            Th.Apply(theme);
            BackColor = Th.Bg;
            Changed();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Th.Bg);
            Th.DrawDecor(g, ClientSize.Width, ClientSize.Height, headerH, Sc, tick);

            // шапка
            float isz = P(34), ix = P(22), iy = P(20);
            Th.Fill(g, new RectangleF(ix, iy, isz, isz), P(10), Th.Field);
            GraphicsState st = g.Save();
            g.TranslateTransform(ix + P(5), iy + P(5));
            SnowApp.DrawFlake(g, isz - P(10), Color.Transparent, Th.Accent);
            g.Restore(st);
            Th.Text2(g, "SnowDream", FTitle, Th.Text, new RectangleF(ix + isz + P(12), iy - P(2), P(400), P(22)), StringAlignment.Near);
            Th.Text2(g, "by øktaus", FSub, Th.Sub, new RectangleF(ix + isz + P(12), iy + P(19), P(400), P(18)), StringAlignment.Near);

            foreach (CardInfo c in cards)
            {
                Th.Fill(g, c.R, P(14), Th.Card);
                RectangleF tr = c.Compact
                    ? new RectangleF(c.R.X + P(18), c.R.Y, c.R.Width / 2, c.R.Height)
                    : new RectangleF(c.R.X + P(18), c.R.Y + P(12), c.R.Width - P(36), P(22));
                Th.Text2(g, c.Title, FCap, Th.Sub, tr, StringAlignment.Near);
            }
            foreach (Widget w in ws) w.Paint(g);
        }

        Widget HitTest(Point p)
        {
            for (int i = ws.Count - 1; i >= 0; i--)
                if (ws[i].Clickable && ws[i].B.Contains(p)) return ws[i];
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (pressed != null) { pressed.OnMove(e.Location); return; }
            Widget h = HitTest(e.Location);
            if (h != hot)
            {
                if (hot != null) hot.Hover = false;
                hot = h;
                if (hot != null) hot.Hover = true;
                Cursor = hot != null ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            if (hot != null) hot.OnMove(e.Location);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (hot != null)
            {
                pressed = hot; pressed.Down = true;
                pressed.OnDown(e.Location);
                Invalidate();
            }
            else if (e.Y < headerH)
            {
                N.ReleaseCapture();                                    // перетаскивание за шапку
                N.SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (pressed == null) return;
            Widget w = pressed; pressed = null;
            w.Down = false;
            w.OnUp(e.Location, w.B.Contains(e.Location));
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != null && pressed == null) { hot.Hover = false; hot = null; Cursor = Cursors.Default; Invalidate(); }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            tm.Stop(); tm.Dispose();
            base.OnFormClosed(e);
        }
    }
}
