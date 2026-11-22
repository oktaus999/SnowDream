// SnowDream: сугроб отваливается глыбами, которые падают и ложатся на то, во что попали (поток рендера).
using System;
using System.Collections.Generic;

namespace DesktopSnow
{
    class Chunk
    {
        public float x, y, vx, vy, ignT;   // x — левый край, y — линия кромки окна, с которой сорвалась глыба
        public float[] h, bo;              // высота снега и смещение низа по каждому столбцу
        public int ign, seed;
    }

    unsafe partial class SnowApp
    {
        List<Chunk> chunks = new List<Chunk>();
        const int MaxChunks = 700;

        // Снег со столбцов [c0, c1) отрывается кусками по 15–55 px. Каждый кусок сохраняет форму сугроба.
        void CollapseRange(Surface s, int c0, int c1, float frac, float vx, float vy, int ign)
        {
            if (chunks.Count > MaxChunks) { CrumbleRange(s, c0, c1, frac, vx, vy, ign); return; }
            float[] h = s.H; int n = h.Length;
            if (c0 < 0) c0 = 0; if (c1 > n) c1 = n;
            int c = c0;
            while (c < c1)
            {
                if (h[c] * frac < 0.35f) { h[c] -= h[c] * frac; c++; continue; }
                int w = (int)((15f + Rnd() * 40f) * S);
                int e = Math.Min(c1, c + Math.Max(4, w));
                int len = e - c;
                Chunk k = new Chunk();
                k.h = new float[len]; k.bo = new float[len];
                float mass = 0;
                for (int i = 0; i < len; i++)
                {
                    float full = h[c + i], part = full * frac;
                    k.h[i] = part;
                    // низ глыбы лежит на оставшемся снеге (если сорвалась только верхняя часть)
                    k.bo[i] = s.Base[c + i] - (full - part);
                    h[c + i] = full - part;
                    mass += part;
                }
                c = e;
                if (mass < 1.5f) continue;
                k.x = s.L + (e - len);
                k.y = s.T;
                k.vx = vx + (Rnd() - 0.5f) * 30f * S;   // куски слегка расходятся
                k.vy = vy + Rnd() * 35f * S;
                k.ign = ign; k.ignT = ign != 0 ? 0.5f : 0f;
                k.seed = rng.Next();
                chunks.Add(k);
            }
        }

        // Старое поведение: снег рассыпается мелкими комками (для брызг и как запасной вариант).
        void CrumbleRange(Surface s, int c0, int c1, float frac, float vx, float vy, int ign)
        {
            float[] h = s.H; int n = h.Length;
            if (c0 < 0) c0 = 0; if (c1 > n) c1 = n;
            for (int i = c0; i < c1; i += 2)
            {
                bool two = i + 1 < c1;
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

        void UpdateChunks(float dt)
        {
            float tv = 1100f * S, k2 = Math.Min(1f, (0.4f + 3f * gust) * dt);
            float gk = G * S * (1f - 0.6f * gust) * dt;
            for (int ci = chunks.Count - 1; ci >= 0; ci--)
            {
                Chunk k = chunks[ci];
                float oy = k.y;
                k.vy += gk; if (k.vy > tv) k.vy = tv;
                k.vx += (wind * 0.3f - k.vx) * k2;
                k.x += k.vx * dt; k.y += k.vy * dt;
                if (k.ignT > 0) { k.ignT -= dt; if (k.ignT <= 0) k.ign = 0; }

                if (C.Drifts && !Blowing && HitChunk(k, oy)) { chunks.RemoveAt(ci); continue; }
                if (k.y - 60f * S > vT + vH || k.x > vL + vW + 400 * S || k.x + k.h.Length < vL - 400 * S) chunks.RemoveAt(ci);
            }
        }

        // Глыба коснулась поверхности? Тогда её снег ложится в сугроб, а вверх летит немного снежной пыли.
        bool HitChunk(Chunk k, float oy)
        {
            int len = k.h.Length;
            int step = Math.Max(1, len / 4);
            Surface best = null; float bestY = float.MaxValue; int bestIdx = -1;
            for (int i = step / 2; i < len; i += step)
            {
                if (k.h[i] < 0.3f) continue;
                int xi = (int)(k.x + i);
                float by = k.y + k.bo[i], oby = oy + k.bo[i];
                for (int j = 0; j < z.Count; j++)
                {
                    Surface s = z[j];
                    if (!s.Enabled || s.Id == k.ign || xi < s.L || xi >= s.R) continue;
                    int c = xi - s.L;
                    float ys = s.T + s.Base[c] - s.H[c];
                    if (oby <= ys + 1f && by >= ys && ys < bestY && !Covered(j, xi, ys - 0.5f)) { best = s; bestY = ys; bestIdx = j; }
                }
            }
            if (best == null) return false;

            float cap = best.HMax * 1.2f;
            for (int i = 0; i < len; i++)
            {
                float m = k.h[i];
                if (m < 0.05f) continue;
                int c = (int)(k.x + i) - best.L;
                if (c >= 0 && c < best.H.Length) best.H[c] = Math.Min(cap, best.H[c] + m * 0.9f);
                else Clump(k.x + i, bestY - 2f, k.vx * 0.5f, 0, m, 0);   // край глыбы свесился за край окна — осыпается
            }
            // облачко снежной пыли от удара
            int puffs = Math.Min(10, 2 + len / 8);
            for (int p = 0; p < puffs; p++)
                Clump(k.x + Rnd() * len, bestY - 2f * S, (Rnd() - 0.5f) * 140f * S, -(50f + Rnd() * 140f) * S, 0.6f + Rnd(), best.Id);
            return true;
        }

        void DrawChunks()
        {
            bool pixel = C.Style >= 2;
            int B = Math.Max(3, (int)Math.Round(4 * S));
            foreach (Chunk k in chunks)
            {
                int len = k.h.Length;
                int mx0 = int.MaxValue, my0 = int.MaxValue, mx1 = -1, my1 = -1;
                for (int i = 0; i < len; i++)
                {
                    float hh = k.h[i];
                    // края глыбы скруглены, чтобы она выглядела комом, а не вырезанным прямоугольником
                    float edge = Math.Min(1f, Math.Min((i + 1) / 3f, (len - i) / 3f));
                    hh *= 0.55f + 0.45f * edge;
                    if (pixel) hh = (float)Math.Floor(hh / B) * B;
                    if (hh < 0.3f) continue;
                    int bx = (int)Math.Floor(k.x + i) - vL;
                    if (bx < 0 || bx >= vW) continue;
                    // нижняя кромка неровная: куски рвутся не по линейке
                    float rough = pixel ? 0 : ((Hash(k.seed + i / 3) % 5) * 0.45f) * (0.4f + 0.6f * (1f - edge));
                    float bot = k.y + k.bo[i] - rough, top = bot - hh;
                    int by0 = (int)Math.Floor(top) - vT, by1 = (int)Math.Ceiling(bot) - vT;
                    if (by0 < 0) by0 = 0; if (by1 > vH) by1 = vH;
                    if (by0 >= by1) continue;
                    if (bx < mx0) mx0 = bx; if (bx + 1 > mx1) mx1 = bx + 1;
                    if (by0 < my0) my0 = by0; if (by1 > my1) my1 = by1;
                    for (int by = by0; by < by1; by++)
                    {
                        float y = by + vT;
                        float cov = pixel ? 1f : Math.Min(y + 1f, bot) - Math.Max(y, top);
                        if (cov <= 0) continue;
                        if (cov > 1) cov = 1;
                        float dd = y - top; if (dd < 0) dd = 0;
                        float g = Math.Min(1f, dd / 14f) * 0.8f;
                        float under = bot - y < 2f ? 0.86f : 1f;          // тень снизу
                        int cr = (int)((248f - 46f * g) * under), cg = (int)((251f - 34f * g) * under), cb = (int)((255f - 16f * g) * under);
                        if (pixel && (((int)(k.x + i) - (int)k.x) % B == B - 1 || ((int)(bot - y)) % B == 0)) { cr -= 22; cg -= 18; cb -= 8; }
                        Blend(buf + (long)by * vW + bx, cr, cg, cb, cov * 0.97f);
                    }
                }
                if (mx1 > mx0) Mark(mx0, my0, mx1, my1);
            }
        }
    }
}
