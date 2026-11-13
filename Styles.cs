// SnowDream: разные виды снежинок и блочные сугробы (поток рендера).
using System;

namespace DesktopSnow
{
    unsafe partial class SnowApp
    {
        // Шестилучевая снежинка-кристалл радиуса R, повёрнутая на угол rot.
        void Crystal(float cx, float cy, float R, float alpha, float rot, int cr, int cg, int cb)
        {
            if (alpha <= 0.004f) return;
            float bx = cx - vL, by = cy - vT;
            int x0 = (int)Math.Floor(bx - R - 1), x1 = (int)Math.Ceiling(bx + R + 1);
            int y0 = (int)Math.Floor(by - R - 1), y1 = (int)Math.Ceiling(by + R + 1);
            if (x0 < 0) x0 = 0; if (y0 < 0) y0 = 0;
            if (x1 > vW) x1 = vW; if (y1 > vH) y1 = vH;
            if (x0 >= x1 || y0 >= y1) return;
            Mark(x0, y0, x1, y1);

            float c0 = (float)Math.Cos(rot), s0 = (float)Math.Sin(rot);
            // три оси через центр: 0°, 60°, 120°
            float ux0 = c0, uy0 = s0;
            float ux1 = c0 * 0.5f - s0 * 0.866f, uy1 = s0 * 0.5f + c0 * 0.866f;
            float ux2 = -c0 * 0.5f - s0 * 0.866f, uy2 = -s0 * 0.5f + c0 * 0.866f;
            float core = R * 0.28f + 0.5f, arm = 0.62f + R * 0.04f;   // толщина лучей растёт с размером

            for (int y = y0; y < y1; y++)
            {
                float dy = y + 0.5f - by;
                uint* row = buf + (long)y * vW;
                for (int x = x0; x < x1; x++)
                {
                    float dx = x + 0.5f - bx;
                    float best = core - (float)Math.Sqrt(dx * dx + dy * dy);
                    best = Math.Max(best, Arm(dx, dy, ux0, uy0, R, arm));
                    best = Math.Max(best, Arm(dx, dy, ux1, uy1, R, arm));
                    best = Math.Max(best, Arm(dx, dy, ux2, uy2, R, arm));
                    if (best <= 0) continue;
                    if (best > 1) best = 1;
                    Blend(row + x, cr, cg, cb, best * alpha);
                }
            }
        }

        // Покрытие пикселя лучом вдоль оси (ux, uy); на 60% длины — маленькие «веточки» шире луча.
        static float Arm(float dx, float dy, float ux, float uy, float R, float w)
        {
            float along = Math.Abs(dx * ux + dy * uy);
            if (along > R + 0.5f) return 0;
            float perp = Math.Abs(-dx * uy + dy * ux);
            float width = w;
            float bd = Math.Abs(along - R * 0.6f);
            if (R > 3f && bd < 1.2f) width += (1.2f - bd) * 0.9f;   // утолщение-веточка
            float cov = width - perp;
            float tip = R + 0.5f - along;
            if (tip < cov) cov = tip;
            return cov;
        }

        // Пиксельная снежинка: квадратный блок без сглаживания, светлая верхняя-левая грань и тёмная нижняя-правая.
        void Block(float cx, float cy, float r, float alpha, int cr, int cg, int cb)
        {
            if (alpha <= 0.004f) return;
            int side = Math.Max(2, (int)Math.Round(r * 1.7f));
            int sx0 = (int)Math.Floor(cx - side * 0.5f) - vL, sy0 = (int)Math.Floor(cy - side * 0.5f) - vT;
            int sx1 = sx0 + side, sy1 = sy0 + side;
            int x0 = Math.Max(0, sx0), y0 = Math.Max(0, sy0), x1 = Math.Min(vW, sx1), y1 = Math.Min(vH, sy1);
            if (x0 >= x1 || y0 >= y1) return;
            Mark(x0, y0, x1, y1);
            bool shade = side >= 4;
            int hr = Math.Min(255, cr + 22), hg = Math.Min(255, cg + 18), hb = 255;
            int lr = cr * 4 / 5, lg = cg * 4 / 5, lb = cb * 9 / 10;
            for (int y = y0; y < y1; y++)
            {
                uint* row = buf + (long)y * vW;
                for (int x = x0; x < x1; x++)
                {
                    if (shade && (x == sx0 || y == sy0)) Blend(row + x, hr, hg, hb, alpha);
                    else if (shade && (x == sx1 - 1 || y == sy1 - 1)) Blend(row + x, lr, lg, lb, alpha);
                    else Blend(row + x, cr, cg, cb, alpha);
                }
            }
        }

        // Пиксельные спрайты: # — белый пиксель, o — светлая серединка.
        static readonly string[] SprPlus = {
            ".#.",
            "#o#",
            ".#." };
        static readonly string[] SprStar = {
            "..#...#..",
            "...#.#...",
            "#..###..#",
            ".##ooo##.",
            "..#ooo#..",
            ".##ooo##.",
            "#..###..#",
            "...#.#...",
            "..#...#.." };

        // Рисует спрайт: каждая клетка — квадрат k×k экранных пикселей, без сглаживания.
        void Sprite(float cx, float cy, string[] spr, int k, float alpha)
        {
            if (alpha <= 0.004f) return;
            int n = spr.Length, side = n * k;
            int sx0 = (int)Math.Round(cx - side * 0.5f) - vL, sy0 = (int)Math.Round(cy - side * 0.5f) - vT;
            int x0 = Math.Max(0, sx0), y0 = Math.Max(0, sy0), x1 = Math.Min(vW, sx0 + side), y1 = Math.Min(vH, sy0 + side);
            if (x0 >= x1 || y0 >= y1) return;
            Mark(x0, y0, x1, y1);
            for (int y = y0; y < y1; y++)
            {
                string line = spr[(y - sy0) / k];
                uint* row = buf + (long)y * vW;
                for (int x = x0; x < x1; x++)
                {
                    char ch = line[(x - sx0) / k];
                    if (ch == '#') Blend(row + x, 255, 255, 255, alpha);
                    else if (ch == 'o') Blend(row + x, 214, 222, 235, alpha);
                }
            }
        }

        // Снежинка выбранного вида. rot — угол поворота кристалла.
        void StyledFlake(float x, float y, float r, float a, float hv, float rot, int cr, int cg, int cb)
        {
            switch (C.Style)
            {
                case 1:
                    if (r < 1.3f) Particle(x, y, r, a, hv, cr, cg, cb);          // мелкие и дальние — просто точки
                    else
                    {
                        if (Math.Abs(hv) > 250f * S) Particle(x, y, r * 0.6f, a * 0.6f, hv, cr, cg, cb);   // шлейф в порыв
                        Crystal(x, y, r * 2.1f, a, rot, cr, cg, cb);
                    }
                    break;
                case 2:
                    Block(x, y, r, a, cr, cg, cb);
                    break;
                case 3:
                    // пиксельные снежинки: дальние — квадратик, средние — крестик, ближние — звёздочка
                    if (r < 1.2f) Block(x, y, r, a, cr, cg, cb);
                    else if (r < 2.1f) Sprite(x, y, SprPlus, 2, a);
                    else Sprite(x, y, SprStar, r >= 3.2f ? 3 : 2, a);
                    break;
                default:
                    Particle(x, y, r, a, hv, cr, cg, cb);
                    break;
            }
        }

        // Блочный сугроб для пиксельного стиля: высота ступеньками, грани блоков и светлый верхний слой.
        void DrawPilePixel(Surface s)
        {
            float[] h = s.H, bs = s.Base; int n = h.Length, occN = s.Occ.Count;
            int B = Math.Max(3, (int)Math.Round(4 * S));
            int mx0 = int.MaxValue, my0 = int.MaxValue, mx1 = -1, my1 = -1;
            for (int c = 0; c < n; c++)
            {
                int g0 = c - c % B;
                int mid = Math.Min(n - 1, g0 + B / 2);
                float hq = (float)Math.Floor(h[mid] / B) * B;   // высота блока — по середине колонки блоков
                if (hq < B) continue;
                int x = s.L + c, bx = x - vL;
                if (bx < 0 || bx >= vW) continue;
                int bot = (int)Math.Round(s.T + bs[c]);
                int ytop = bot - (int)hq;
                int by0 = ytop - vT, by1 = bot - vT;
                if (by0 < 0) by0 = 0; if (by1 > vH) by1 = vH;
                if (by0 >= by1) continue;
                if (bx < mx0) mx0 = bx; if (bx + 1 > mx1) mx1 = bx + 1;
                if (by0 < my0) my0 = by0; if (by1 > my1) my1 = by1;

                int k = 0;
                for (int j = 0; j < occN && k < 64; j++)
                {
                    Surface o = z[s.Occ[j]];
                    if (x >= o.L && x < o.R) { oT[k] = o.T; oB[k] = o.B; k++; }
                }
                bool edgeX = (c % B) == B - 1;
                for (int by = by0; by < by1; by++)
                {
                    int y = by + vT;
                    bool hid = false;
                    for (int q = 0; q < k; q++) if (y >= oT[q] && y < oB[q]) { hid = true; break; }
                    if (hid) continue;
                    int fromTop = y - ytop, row = (bot - 1 - y) / B;
                    // лёгкий «шум» на каждом блоке, чтобы сугроб не был плоским
                    int v = (int)(Hash(g0 * 131 + row * 7919 + s.Id * 31) % 13) - 6;
                    int cr, cg, cb;
                    if (fromTop < B) { cr = 252; cg = 254; cb = 255; }                    // верхний слой — самый светлый
                    else { cr = 226 + v; cg = 236 + v; cb = 250; }
                    if (edgeX || (bot - y) % B == 0) { cr -= 22; cg -= 18; cb -= 8; }   // грани блоков
                    Blend(buf + (long)by * vW + bx, cr, cg, cb, 0.97f);
                }
            }
            if (mx1 > mx0) Mark(mx0, my0, mx1, my1);
        }
    }
}
