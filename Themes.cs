// SnowDream: темы окна настроек и их украшения.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DesktopSnow
{
    static partial class Th
    {
        public static Color Accent2 = Color.FromArgb(140, 203, 255);   // второй цвет градиента главной кнопки
        public static int Decor;                                        // 0 нет, 1 леденец, 2 тёплое свечение, 3 радуга со звёздами

        public static readonly string[] NamesRu = { "Ночь", "Ёлка", "Мандарин", "Радуга" };
        public static readonly string[] NamesEn = { "Night", "Fir tree", "Tangerine", "Rainbow" };

        // Цвета кружков-переключателей: фон и акцент каждой темы.
        public static readonly Color[,] Swatch = {
            { Color.FromArgb(21, 23, 28),  Color.FromArgb(140, 203, 255) },
            { Color.FromArgb(18, 52, 36),  Color.FromArgb(226, 62, 62) },
            { Color.FromArgb(52, 24, 14),  Color.FromArgb(255, 140, 40) },
            { Color.FromArgb(14, 42, 86),  Color.FromArgb(255, 110, 190) } };

        // Пастельная радуга: красный, оранжевый, жёлтый, зелёный, голубой, фиолетовый.
        public static readonly Color[] Rainbow = {
            Color.FromArgb(255, 92, 92), Color.FromArgb(255, 170, 60), Color.FromArgb(255, 232, 80),
            Color.FromArgb(110, 220, 110), Color.FromArgb(90, 170, 255), Color.FromArgb(170, 110, 255) };

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        public static void Apply(int theme)
        {
            switch (theme)
            {
                case 1:   // Ёлка: хвойный фон, красный акцент, белый текст
                    Bg = C(13, 30, 22); Card = C(20, 44, 32); Field = C(30, 60, 44); Line = C(40, 74, 54);
                    Text = C(246, 248, 242); Sub = C(160, 186, 166);
                    Accent = C(226, 62, 62); Accent2 = C(196, 34, 44); AccentText = C(255, 255, 255);
                    Track = C(44, 80, 60); Knob = C(196, 210, 198); Decor = 1;
                    break;
                case 2:   // Мандарин: тёплый тёмный фон, оранжево-красный акцент
                    Bg = C(28, 15, 11); Card = C(43, 24, 17); Field = C(60, 34, 24); Line = C(74, 42, 30);
                    Text = C(255, 242, 232); Sub = C(200, 164, 144);
                    Accent = C(255, 146, 48); Accent2 = C(236, 62, 44); AccentText = C(42, 14, 4);
                    Track = C(84, 48, 34); Knob = C(214, 188, 172); Decor = 2;
                    break;
                case 3:   // Радуга: космически-синий фон, звёзды, радужные акценты
                    Bg = C(12, 36, 74); Card = C(19, 50, 96); Field = C(27, 63, 116); Line = C(36, 76, 132);
                    Text = C(255, 255, 255); Sub = C(166, 194, 236);
                    Accent = C(255, 120, 196); Accent2 = C(255, 120, 196); AccentText = C(30, 14, 50);
                    Track = C(40, 80, 136); Knob = C(196, 214, 244); Decor = 3;
                    break;
                default:  // Ночь
                    Bg = C(21, 23, 28); Card = C(30, 33, 40); Field = C(40, 44, 53); Line = C(48, 53, 64);
                    Text = C(234, 237, 243); Sub = C(138, 146, 162);
                    Accent = C(140, 203, 255); Accent2 = C(140, 203, 255); AccentText = C(10, 24, 38);
                    Track = C(55, 60, 72); Knob = C(160, 168, 182); Decor = 0;
                    break;
            }
        }

        // Заливка главной кнопки: однотонная, градиент или радуга. toward/amt — подсветка при наведении/нажатии.
        public static void FillAccent(Graphics g, RectangleF r, float rad, Color toward, float amt)
        {
            using (GraphicsPath p = Round(r, rad))
            {
                if (Decor == 3)
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(r, Color.Red, Color.Blue, LinearGradientMode.Horizontal))
                    {
                        ColorBlend cb = new ColorBlend(Rainbow.Length);
                        for (int i = 0; i < Rainbow.Length; i++)
                        {
                            cb.Colors[i] = Mix(Mix(Rainbow[i], Color.White, 0.18f), toward, amt);
                            cb.Positions[i] = i / (float)(Rainbow.Length - 1);
                        }
                        b.InterpolationColors = cb;
                        g.FillPath(b, p);
                    }
                }
                else if (Accent2 != Accent)
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(r, Mix(Accent, toward, amt), Mix(Accent2, toward, amt), LinearGradientMode.Horizontal))
                        g.FillPath(b, p);
                }
                else using (SolidBrush b = new SolidBrush(Mix(Accent, toward, amt))) g.FillPath(b, p);
            }
        }

        // Украшения поверх фона окна (рисуются до карточек). headerH — высота шапки, tick — счётчик кадров анимации.
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
                case 2:   // тёплое свечение из левого верхнего угла
                {
                    RectangleF e = new RectangleF(-w * 0.35f, -headerH * 3.2f, w * 1.1f, headerH * 6f);
                    using (GraphicsPath gp = new GraphicsPath())
                    {
                        gp.AddEllipse(e);
                        using (PathGradientBrush b = new PathGradientBrush(gp))
                        {
                            b.CenterColor = Color.FromArgb(70, 255, 120, 40);
                            b.SurroundColors = new[] { Color.FromArgb(0, 255, 120, 40) };
                            g.FillPath(b, gp);
                        }
                    }
                    break;
                }
                case 3:   // переливающаяся радужная полоса и мерцающие пиксельные звёзды
                {
                    float band = 3 * s, wave = 3 * s;
                    for (int i = 0; i < Rainbow.Length; i++)
                        using (SolidBrush b = new SolidBrush(Rainbow[i]))
                            for (float x = 0; x < w; x += 8 * s)
                            {
                                // кусочки сдвигаются вверх-вниз «лесенкой», как пиксельный шлейф
                                float off = ((int)((x / (8 * s)) + tick / 6) % 2 == 0) ? 0 : wave * 0.5f;
                                g.FillRectangle(b, x, i * band + off, 8 * s + 0.5f, band + 0.5f);
                            }
                    Random r = new Random(1337);
                    for (int i = 0; i < 26; i++)
                    {
                        float x = (float)r.NextDouble() * w, y = 26 * s + (float)r.NextDouble() * (h - 30 * s);
                        int phase = r.Next(60), size = r.Next(3) == 0 ? 2 : 1;
                        int a = (int)(110 + 110 * Math.Sin((tick + phase) * 0.12));
                        if (a < 20) continue;
                        float px = Math.Max(1f, (float)Math.Round(s)) * size;
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(Math.Min(255, a), 255, 255, 255)))
                        {
                            // пиксельная звёздочка-крестик
                            g.FillRectangle(b, x, y, px, px);
                            if (size == 2)
                            {
                                g.FillRectangle(b, x - px, y, px, px); g.FillRectangle(b, x + px, y, px, px);
                                g.FillRectangle(b, x, y - px, px, px); g.FillRectangle(b, x, y + px, px, px);
                            }
                        }
                    }
                    break;
                }
            }
        }
    }

    // Кружки выбора темы в шапке окна.
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
            int cur = F.App.C.Theme;
            for (int i = 0; i < Count; i++)
            {
                float x = B.X + i * (D + Gap), y = B.Y + (B.Height - D) / 2;
                if (i == cur || (Hover && i == hoverIdx))
                    using (Pen ring = new Pen(i == cur ? Th.Text : Th.Sub, 1.6f * s))
                        g.DrawEllipse(ring, x - 3 * s, y - 3 * s, D + 6 * s, D + 6 * s);
                // фон темы, а поверх — её акцент половинкой
                using (SolidBrush b = new SolidBrush(Th.Swatch[i, 0])) g.FillEllipse(b, x, y, D, D);
                if (i == 3)
                {
                    for (int k = 0; k < 6; k++)
                        using (SolidBrush b = new SolidBrush(Th.Rainbow[k]))
                            g.FillPie(b, x, y, D, D, -90 + k * 30, 30);
                }
                else using (SolidBrush b = new SolidBrush(Th.Swatch[i, 1])) g.FillPie(b, x, y, D, D, -90, 180);
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
