// SnowDream: сосульки на верхнем краю мониторов (поток рендера).
using System;
using System.Collections.Generic;

namespace DesktopSnow
{
    class Icicle
    {
        public float x, top, len, maxLen, w, xOff, yOff, vx, vy, timer, phase, rate;
        public int state;   // 0 растёт, 1 висит, 2 падает, 3 ждёт, чтобы вырасти заново
    }

    unsafe partial class SnowApp
    {
        List<Icicle> ice = new List<Icicle>();
        bool iceWas = true;

        void BuildIcicles()
        {
            ice.Clear();
            foreach (N.RECT m in mons)
            {
                float x = m.L + (6f + Rnd() * 20f) * S;
                while (x < m.R - 6f * S)
                {
                    Icicle c = new Icicle();
                    c.x = x; c.top = m.T;
                    Regrow(c, true);
                    ice.Add(c);
                    x += (16f + Rnd() * 40f) * S;
                }
            }
        }

        void Regrow(Icicle c, bool initial)
        {
            c.len = 0;
            c.maxLen = (12f + (float)Math.Pow(Rnd(), 1.6) * 75f) * S;
            c.w = Math.Max(1.6f * S, c.maxLen * (0.07f + 0.04f * Rnd()));
            c.xOff = c.yOff = c.vx = c.vy = 0;
            c.phase = Rnd() * 6.2832f;
            c.rate = 0.7f + 0.6f * Rnd();
            c.state = 3;
            c.timer = initial ? Rnd() * 25f : 6f + Rnd() * 30f;
        }

        void Drop(Icicle c, float vx)
        {
            if (c.state >= 2 || c.len < 2f * S) return;
            c.state = 2; c.vx = vx; c.vy = 0; c.xOff = 0; c.yOff = 0;
        }

        void UpdateIcicles(float dt)
        {
            bool on = C.Icicles;
            if (iceWas && !on)   // выключили — все сосульки падают и разбиваются
                foreach (Icicle c in ice)
                {
                    if (c.state < 2 && c.len > 3f * S) Drop(c, 0);
                    else if (c.state != 2) { c.state = 3; c.len = 0; }
                }
            if (!iceWas && on) foreach (Icicle c in ice) if (c.state == 3) c.timer = Rnd() * 10f;
            iceWas = on;

            float spd = Math.Max(0.05f, C.IceSpeed);
            bool knock = C.Cursor && curValid && curSpeed > 200f * S;
            foreach (Icicle c in ice)
            {
                switch (c.state)
                {
                    case 3:
                        if (!on) break;
                        c.timer -= dt;
                        if (c.timer <= 0) c.state = 0;
                        break;
                    case 0:
                    case 1:
                        if (c.state == 0)
                        {
                            // ~2.5 минуты до полной длины при скорости 1×, к концу рост замедляется
                            c.len += c.maxLen / 150f * spd * c.rate * (1.2f - 0.7f * c.len / c.maxLen) * dt;
                            if (c.len >= c.maxLen) { c.len = c.maxLen; c.state = 1; c.timer = (25f + Rnd() * 120f) / Math.Max(0.3f, spd); }
                        }
                        else { c.timer -= dt; if (c.timer <= 0) { Drop(c, 0); break; } }
                        // капля с кончика
                        if (c.len > 0.4f * c.maxLen && Rnd() < dt * 0.12f * spd)
                        {
                            FW d = new FW();
                            d.kind = 5; d.x = c.x; d.y = c.top + c.len; d.vy = 40f * S;
                            d.life = d.max = 2.5f; d.size = 1.1f * S; d.gy = float.MaxValue;
                            AddFw(d);
                        }
                        // быстрый взмах курсором сбивает сосульку
                        if (knock && c.len > 5f * S && Math.Abs(curX - c.x) < c.w + 4f * S && curY >= c.top && curY <= c.top + c.len)
                            Drop(c, curVx * 0.3f);
                        break;
                    case 2:
                        FallIcicle(c, dt);
                        break;
                }
            }
        }

        void FallIcicle(Icicle c, float dt)
        {
            float prevTip = c.top + c.yOff + c.len;
            c.vy += G * S * 0.9f * dt;
            c.yOff += c.vy * dt;
            c.xOff += c.vx * dt;
            c.vx += (wind * 0.6f - c.vx) * Math.Min(1f, (0.3f + 2f * gust) * dt);   // ветер сносит падающую сосульку
            float tip = c.top + c.yOff + c.len, tx = c.x + c.xOff;
            int xi = (int)tx;

            // ищем первую поверхность, которую пересёк кончик
            Surface hit = null; int hc = 0; float hy = float.MaxValue;
            for (int i = 0; i < z.Count; i++)
            {
                Surface s = z[i];
                if (!s.Enabled || xi < s.L || xi >= s.R) continue;
                int col = xi - s.L;
                float ys = s.T + s.Base[col] - s.H[col];
                if (ys <= c.top + 2 || ys >= hy) continue;
                if (prevTip <= ys && tip >= ys && !Covered(i, tx, ys - 0.5f)) { hit = s; hc = col; hy = ys; }
            }
            if (hit != null) { Shatter(c, tx, hy, hit, hc); return; }
            if (tip > vT + vH) Shatter(c, tx, vT + vH - 1, null, 0);
        }

        // Сосулька разбивается: осколки разлетаются и подпрыгивают, сугроб от удара брызгает снегом.
        void Shatter(Icicle c, float x, float y, Surface s, int col)
        {
            int n = Math.Min(45, 8 + (int)(c.len / (2.5f * S)));
            for (int k = 0; k < n; k++)
            {
                FW f = new FW();
                f.kind = 4;
                f.x = x + (Rnd() - 0.5f) * c.w * 2f;
                f.y = y - Rnd() * Math.Min(c.len * 0.3f, 10f * S);
                f.vx = (Rnd() - 0.5f) * 520f * S;
                f.vy = -(60f + Rnd() * 360f) * S;
                f.life = f.max = 0.7f + Rnd() * 0.8f;
                f.size = (0.7f + Rnd() * 1.6f) * S;
                f.r = 200 + rng.Next(55); f.g = 225 + rng.Next(30); f.b = 255;
                f.gy = y;
                AddFw(f);
            }
            if (s != null && C.Drifts)
            {
                int r = (int)(10 * S);
                CollapseRange(s, col - r, col + r, 0.35f, 0, -140f * S, s.Id);
            }
            Regrow(c, false);
        }

        void BlowIcicles(float front)
        {
            foreach (Icicle c in ice)
                if (c.state < 2 && (gustSgn > 0 ? c.x < front : c.x > front)) Drop(c, gustSgn * (300f + Rnd() * 400f) * S);
        }

        void DrawIcicles()
        {
            float am = 1f - 0.5f * calm;
            foreach (Icicle c in ice)
            {
                if (c.state == 3 || c.len < 1f) continue;
                DrawIcicle(c.x + c.xOff, c.top + c.yOff, c.len, c.w, c.phase, am);
            }
        }

        void DrawIcicle(float cx, float top, float len, float w, float ph, float am)
        {
            int by0 = (int)Math.Floor(top) - vT, by1 = (int)Math.Ceiling(top + len) - vT;
            int bx0 = (int)Math.Floor(cx - w - 1) - vL, bx1 = (int)Math.Ceiling(cx + w + 1) - vL;
            if (by0 < 0) by0 = 0; if (by1 > vH) by1 = vH;
            if (bx0 < 0) bx0 = 0; if (bx1 > vW) bx1 = vW;
            if (by0 >= by1 || bx0 >= bx1) return;
            Mark(bx0, by0, bx1, by1);
            float cap = 2.5f * S;
            for (int by = by0; by < by1; by++)
            {
                float yy = by + vT + 0.5f - top;
                if (yy < 0 || yy > len) continue;
                float ty = yy / len;
                // сужается к кончику, с небольшими «наплывами» льда
                float hw = w * (float)Math.Pow(1 - ty, 0.85) * (1f + 0.1f * (float)Math.Sin(yy * 0.55f + ph));
                if (hw < 0.3f) hw = 0.3f;
                uint* row = buf + (long)by * vW;
                for (int bx = bx0; bx < bx1; bx++)
                {
                    float dx = bx + vL + 0.5f - cx;
                    float cov = hw + 0.5f - Math.Abs(dx);
                    if (cov <= 0) continue;
                    if (cov > 1) cov = 1;
                    float u = dx / Math.Max(hw, 0.5f);
                    float hl = Math.Max(0f, 1f - Math.Abs(u + 0.4f) * 3.2f);   // блик слева
                    int r, g, b; float a;
                    if (yy < cap) { r = 248; g = 251; b = 255; a = 0.95f; }     // снежная шапочка у основания
                    else
                    {
                        r = (int)(196 + 54 * hl); g = (int)(224 + 30 * hl); b = 255;
                        a = Math.Min(1f, 0.42f + 0.35f * (1f - Math.Abs(u)) + 0.3f * hl);
                    }
                    Blend(row + bx, r, g, b, a * cov * am);
                }
            }
        }
    }
}
