// SnowDream: темы окна настроек, «своя» тема с фото и палитрой, кружки выбора темы.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace DesktopSnow
{
    static partial class Th
    {
        public static Color Accent2 = Color.FromArgb(140, 203, 255);   // второй цвет градиента главной кнопки
        public static int Decor;                                        // 0 нет, 1 полоска-леденец, 2 праздничная кайма и золотое свечение
        public static Image Photo;                                      // фото фона «своей» темы
        public static int PhotoDim = 55;                                // затемнение фото, %
        static string photoPath;

        public static readonly string[] NamesRu = { "Ночь", "Ёлка", "Праздник", "Своя тема" };
        public static readonly string[] NamesEn = { "Night", "Fir tree", "Festive", "Custom" };

        // Цвета кружков-переключателей: фон и акцент каждой готовой темы.
        public static readonly Color[,] Swatch = {
            { Color.FromArgb(21, 23, 28),  Color.FromArgb(140, 203, 255) },
            { Color.FromArgb(18, 52, 36),  Color.FromArgb(226, 62, 62) },
            { Color.FromArgb(110, 10, 16), Color.FromArgb(226, 184, 90) } };

        // Палитра для «своей» темы: цвета с праздничной картинки и несколько базовых.
        public static readonly Color[] Palette = {
            Color.FromArgb(123, 10, 16), Color.FromArgb(250, 82, 72), Color.FromArgb(226, 184, 90),
            Color.FromArgb(249, 234, 150), Color.FromArgb(64, 92, 30), Color.FromArgb(18, 52, 36),
            Color.FromArgb(21, 23, 28), Color.FromArgb(20, 44, 84), Color.FromArgb(140, 203, 255),
            Color.FromArgb(170, 130, 255), Color.FromArgb(255, 140, 40), Color.FromArgb(245, 245, 245) };

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
        public static float Lum(Color c) { return (0.299f * c.R + 0.587f * c.G + 0.114f * c.B) / 255f; }

        public static void Apply(Config cfg)
        {
            Photo = null;
            switch (cfg.Theme)
            {
                case 1:   // Ёлка: хвойный фон, красный акцент, белый текст
                    Bg = C(13, 30, 22); Card = C(20, 44, 32); Field = C(30, 60, 44); Line = C(40, 74, 54);
                    Text = C(246, 248, 242); Sub = C(160, 186, 166);
                    Accent = C(226, 62, 62); Accent2 = C(196, 34, 44); AccentText = C(255, 255, 255);
                    Track = C(44, 80, 60); Knob = C(196, 210, 198); Decor = 1;
                    break;
                case 2:   // Праздник: бордо, коралл, золото, светло-жёлтый и хвоя — палитра с бубенцами
                    Bg = C(42, 6, 10); Card = C(64, 12, 18); Field = C(86, 20, 26); Line = C(104, 28, 34);
                    Text = C(250, 238, 190); Sub = C(214, 172, 136);
                    Accent = C(226, 184, 90); Accent2 = C(250, 82, 72); AccentText = C(52, 8, 12);
                    Track = C(104, 32, 36); Knob = C(232, 214, 172); Decor = 2;
                    break;
                case 3:   // своя тема: всё выводится из трёх выбранных цветов
                    Custom(cfg);
                    break;
                default:  // Ночь
                    Bg = C(21, 23, 28); Card = C(30, 33, 40); Field = C(40, 44, 53); Line = C(48, 53, 64);
                    Text = C(234, 237, 243); Sub = C(138, 146, 162);
                    Accent = C(140, 203, 255); Accent2 = C(140, 203, 255); AccentText = C(10, 24, 38);
                    Track = C(55, 60, 72); Knob = C(160, 168, 182); Decor = 0;
                    break;
            }
        }

        static void Custom(Config cfg)
        {
            Color bg = Color.FromArgb(255, Color.FromArgb(cfg.CustomBg));
            Color ac = Color.FromArgb(255, Color.FromArgb(cfg.CustomAccent));
            Color ac2 = Color.FromArgb(255, Color.FromArgb(cfg.CustomAccent2));
            bool light = Lum(bg) > 0.55f;
            Text = light ? C(26, 28, 34) : C(245, 247, 250);
            Bg = bg;
            Card = Mix(bg, Text, 0.07f); Field = Mix(bg, Text, 0.13f); Line = Mix(bg, Text, 0.18f);
            Sub = Mix(Text, bg, 0.42f); Track = Mix(bg, Text, 0.22f); Knob = Mix(Text, bg, 0.3f);
            Accent = ac; Accent2 = ac2;
            AccentText = Lum(ac) > 0.6f ? C(20, 20, 24) : C(255, 255, 255);
            Decor = 0;
            PhotoDim = cfg.CustomDim;
            Photo = LoadPhoto(cfg.CustomPhoto);
        }

        static Image cachedPhoto;
        // Фото грузим в память целиком, чтобы файл не блокировался.
        static Image LoadPhoto(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string path = Path.Combine(Config.Dir, name);
            if (path == photoPath && cachedPhoto != null) return cachedPhoto;
            try
            {
                using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(path)))
                using (Image img = Image.FromStream(ms))
                {
                    if (cachedPhoto != null) cachedPhoto.Dispose();
                    cachedPhoto = new Bitmap(img);
                    photoPath = path;
                    return cachedPhoto;
                }
            }
            catch { return null; }
        }

        // Заливка главной кнопки: однотонная или градиент. toward/amt — подсветка при наведении/нажатии.
        public static void FillAccent(Graphics g, RectangleF r, float rad, Color toward, float amt)
        {
            using (GraphicsPath p = Round(r, rad))
            {
                if (Accent2 != Accent)
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(r, Mix(Accent, toward, amt), Mix(Accent2, toward, amt), LinearGradientMode.Horizontal))
                        g.FillPath(b, p);
                }
                else using (SolidBrush b = new SolidBrush(Mix(Accent, toward, amt))) g.FillPath(b, p);
            }
        }

        // Украшения поверх фона окна (рисуются до карточек).
        public static void DrawDecor(Graphics g, float w, float h, float headerH, float s, int tick)
        {
            switch (Decor)
            {
                case 1:   // полоска-леденец по верхнему краю
                {
                    float sh = 6 * s, step = 14 * s;
                    GraphicsState st = g.Save();
                    g.SetClip(new RectangleF(0, 0, w, sh));
                    using (SolidBrush white = new SolidBrush(C(250, 250, 246))) g.FillRectangle(white, 0, 0, w, sh);
                    using (SolidBrush red = new SolidBrush(C(214, 42, 48)))
                        for (float x = -sh; x < w + sh; x += step)
                        {
                            PointF[] q = { new PointF(x, sh), new PointF(x + sh, 0), new PointF(x + sh + step / 2, 0), new PointF(x + step / 2, sh) };
                            g.FillPolygon(red, q);
                        }
                    g.Restore(st);
                    break;
                }
                case 2:   // праздник: мягкое золотое свечение и кайма «хвоя + золото» сверху
                {
                    RectangleF e = new RectangleF(w * 0.35f, -headerH * 3.4f, w * 0.9f, headerH * 6.2f);
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddEllipse(e);
                        using (PathGradientBrush b = new PathGradientBrush(gp))
                        {
                            b.CenterColor = Color.FromArgb(60, 226, 184, 90);
                            b.SurroundColors = new[] { Color.FromArgb(0, 226, 184, 90) };
                            g.FillPath(b, gp);
                        }
                    }
                    using (SolidBrush green = new SolidBrush(C(64, 92, 30))) g.FillRectangle(green, 0, 0, w, 4 * s);
                    using (SolidBrush gold = new SolidBrush(C(226, 184, 90))) g.FillRectangle(gold, 0, 4 * s, w, 1.5f * s);
                    break;
                }
            }
        }
    }

    // Кружки выбора темы в шапке окна. Последний — «своя» тема: открывает редактор.
    class ThemeDots : Widget
    {
        int hoverIdx = -1;
        const int Count = 4;

        float D { get { return 20 * s; } }
        float Gap { get { return 9 * s; } }

        int IndexAt(PointF p)
        {
            int i = (int)((p.X - B.X) / (D + Gap));
            return i >= 0 && i < Count && p.X - B.X - i * (D + Gap) <= D ? i : -1;
        }

        public override void Paint(Graphics g)
        {
            Config cfg = F.App.C;
            int cur = cfg.Theme;
            for (int i = 0; i < Count; i++)
            {
                float x = B.X + i * (D + Gap), y = B.Y + (B.Height - D) / 2;
                if (i == cur || (Hover && i == hoverIdx))
                    using (Pen ring = new Pen(i == cur ? Th.Text : Th.Sub, 1.6f * s))
                        g.DrawEllipse(ring, x - 3 * s, y - 3 * s, D + 6 * s, D + 6 * s);
                if (i < 3)
                {
                    using (SolidBrush b = new SolidBrush(Th.Swatch[i, 0])) g.FillEllipse(b, x, y, D, D);
                    using (SolidBrush b = new SolidBrush(Th.Swatch[i, 1])) g.FillPie(b, x, y, D, D, -90, 180);
                }
                else
                {
                    // «своя»: цвета пользователя и маленький плюсик
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, Color.FromArgb(cfg.CustomBg)))) g.FillEllipse(b, x, y, D, D);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, Color.FromArgb(cfg.CustomAccent)))) g.FillPie(b, x, y, D, D, -90, 120);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, Color.FromArgb(cfg.CustomAccent2)))) g.FillPie(b, x, y, D, D, 30, 120);
                    using (Pen plus = new Pen(Color.White, 1.8f * s))
                    {
                        float cx = x + D / 2, cy = y + D / 2, r = 4 * s;
                        using (SolidBrush dark = new SolidBrush(Color.FromArgb(150, 0, 0, 0))) g.FillEllipse(dark, cx - 6 * s, cy - 6 * s, 12 * s, 12 * s);
                        g.DrawLine(plus, cx - r, cy, cx + r, cy); g.DrawLine(plus, cx, cy - r, cx, cy + r);
                    }
                }
                using (Pen edge = new Pen(Color.FromArgb(70, 255, 255, 255), 1f)) g.DrawEllipse(edge, x, y, D, D);
            }
            if (Hover && hoverIdx >= 0)
                Th.Text2(g, L.En ? Th.NamesEn[hoverIdx] : Th.NamesRu[hoverIdx], F.FSub, Th.Sub,
                         new RectangleF(B.X + Count * (D + Gap) + 4 * s, B.Y, 120 * s, B.Height), StringAlignment.Near);
        }

        public override void OnMove(PointF p) { int i = IndexAt(p); if (i != hoverIdx) { hoverIdx = i; F.Invalidate(); } }
        public override void OnUp(PointF p, bool inside) { int i = IndexAt(p); if (inside && i >= 0) F.SetTheme(i); }
    }
}
