// SnowDream: частицы, курсор, салют и отрисовка (работает в потоке рендера).
using System;

namespace DesktopSnow
{
    struct FW
    {
        public float x, y, vx, vy, life, max, size, gy;
        public int r, g, b;
        public byte kind;   // 0 искра, 1 ракета, 2 вспышка, 3 след ракеты, 4 осколок льда, 5 капля
    }

    unsafe partial class SnowApp
    {
        // ---------- снежинки и комки ----------
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
            float margin = 150f * S + Math.Abs(windBias) * 3f;   // при сильном ветре снег должен залетать из-за края
            if (where == 2)
            {
                p.x = gustSgn > 0 ? vL - 10f - Rnd() * 60f * S : vL + vW + 10f + Rnd() * 60f * S;
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

        // ---------- курсор ----------
        float curX, curY, curVx, curVy, curSpeed;
        bool curValid;

        void UpdateCursor(float dt)
        {
            N.POINT p;
            if (!C.Cursor || !N.GetCursorPos(out p)) { curValid = false; return; }
            if (curValid)
            {
                curVx += ((p.X - curX) / dt - curVx) * 0.4f;
                curVy += ((p.Y - curY) / dt - curVy) * 0.4f;
            }
            else { curVx = 0; curVy = 0; }
            curX = p.X; curY = p.Y; curValid = true;
            curSpeed = (float)Math.Sqrt(curVx * curVx + curVy * curVy);
        }

        void UpdateParticles(float dt)
        {
            float tv = 1100f * S, gk = G * S * (1f - 0.85f * gust) * dt, k = Math.Min(1f, (0.8f + 4f * gust) * dt);
            float edge = 250 * S + Math.Abs(windBias) * 3f;   // запас за краями экрана растёт с силой ветра
            float right = vL + vW + edge, left = vL - edge, bottom = vT + vH + 10;
            float spd = C.Speed;
            bool canLand = C.Drifts && !Blowing;
            bool cur = curValid;
            float R = 90f * S, R2 = R * R, baseF = 700f * S + 2.2f * curSpeed;
            float damp = Math.Max(0f, 1f - 2.5f * dt);
            for (int i = 0; i < pc; )
            {
                P p = ps[i];
                float oy = p.y;
                // курсор расталкивает снежинки и немного тянет их за собой
                float ax = 0, ay = 0;
                if (cur)
                {
                    float dx = p.x - curX, dy = p.y - curY, d2 = dx * dx + dy * dy;
                    if (d2 < R2 && d2 > 0.25f)
                    {
                        float d = (float)Math.Sqrt(d2), f = 1f - d / R; f *= f;
                        ax = (dx / d * baseF + curVx * 3f) * f;
                        ay = (dy / d * baseF + curVy * 3f) * f;
                    }
                }
                if (!p.clump)
                {
                    float sway = (float)Math.Sin(t * p.fr + p.ph) * (6f + 10f * p.d) * S;
                    p.ux = (p.ux + ax * dt) * damp;
                    p.uy = (p.uy + ay * dt) * damp;
                    p.hv = wind * (0.35f + 0.75f * p.d) + sway + p.ux;
                    p.x += p.hv * dt;
                    p.y += (p.vy * spd + p.uy) * dt;
                }
                else
                {
                    p.vx += ax * dt; p.vy += ay * dt;
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
                if (p.y >= ys && oy <= ys + 1.5f + s.MoveUp && ys < by && !Covered(i, xi, ys - 0.5f))
                {
                    best = s; bc = c; by = ys;
                }
            }
            if (best == null) return false;
            AddSnow(best, bc, p.m, p.r);
            return true;
        }

        // ---------- салют ----------
        FW[] fw = new FW[8000]; int fc;
        float fwShow, fwAcc;
        int fwYear = -1;
        static readonly int[,] Pal = {
            { 255, 90, 90 }, { 255, 200, 70 }, { 100, 200, 255 }, { 130, 255, 150 },
            { 255, 120, 230 }, { 255, 255, 255 }, { 190, 150, 255 }, { 255, 150, 60 } };

        // 1 января в 00:00 — салют на минуту с небольшим.
        void CheckMidnight()
        {
            DateTime n = CityNow();   // полночь — по времени выбранного города
            if (C.Fireworks && n.Month == 1 && n.Day == 1 && n.Hour == 0 && n.Minute < 3 && fwYear != n.Year)
            {
                fwYear = n.Year;
                fwShow = 75f;
            }
        }

        void AddFw(FW f) { if (fc < fw.Length) fw[fc++] = f; }

        void Rocket()
        {
            FW f = new FW();
            f.kind = 1;
            f.x = vL + vW * (0.1f + 0.8f * Rnd());
            f.y = vT + vH + 5;
            float hgt = vH * (0.45f + 0.3f * Rnd());
            f.vy = -(float)Math.Sqrt(2 * 520f * S * hgt);
            f.vx = (Rnd() - 0.5f) * 80f * S;
            f.life = f.max = 4f;
            f.size = 1.7f * S;
            int c = rng.Next(Pal.GetLength(0));
            f.r = Pal[c, 0]; f.g = Pal[c, 1]; f.b = Pal[c, 2];
            AddFw(f);
        }

        void Explode(FW r)
        {
            int type = rng.Next(4);   // 0 шар, 1 кольцо, 2 «ива», 3 двухцветный
            int n = 70 + rng.Next(60);
            float sp0 = (200f + Rnd() * 170f) * S;
            int c2 = rng.Next(Pal.GetLength(0));
            for (int k = 0; k < n; k++)
            {
                double ang = 6.2832 * k / n + Rnd() * 0.08;
                float sp = type == 1 ? sp0 * (0.95f + 0.1f * Rnd()) : sp0 * (float)Math.Sqrt(Rnd());
                if (type == 2) sp *= 0.75f;
                FW f = new FW();
                f.kind = 0;
                f.x = r.x; f.y = r.y;
                f.vx = (float)Math.Cos(ang) * sp + r.vx * 0.3f;
                f.vy = (float)Math.Sin(ang) * sp + r.vy * 0.2f;
                f.life = f.max = type == 2 ? 2.4f + Rnd() * 0.8f : 1.2f + Rnd() * 0.9f;
                f.size = (1.1f + Rnd() * 0.6f) * S;
                if (type == 2) { f.r = 255; f.g = 200; f.b = 90; }
                else if (type == 3 && (k & 1) == 0) { f.r = Pal[c2, 0]; f.g = Pal[c2, 1]; f.b = Pal[c2, 2]; }
                else { f.r = r.r; f.g = r.g; f.b = r.b; }
                AddFw(f);
            }
            FW fl = new FW();
            fl.kind = 2; fl.x = r.x; fl.y = r.y; fl.life = fl.max = 0.18f; fl.size = 14f * S;
            fl.r = 255; fl.g = 250; fl.b = 235;
            AddFw(fl);
        }

        void UpdateFireworks(float dt)
        {
            if (fwShow > 0)
            {
                fwShow -= dt;
                if (fwShow > 4f) fwAcc += dt * 2.2f;   // последние секунды — только догорают искры
                while (fwAcc >= 1f) { fwAcc -= 1f; Rocket(); }
            }
            float gs = 260f * S;
            for (int i = 0; i < fc; )
            {
                FW f = fw[i];
                f.life -= dt;
                if (f.kind == 4)
                {
                    // осколок льда: летит, подпрыгивает на поверхности и тает; в порыв его уносит ветром
                    f.vy += 1500f * S * (1f - 0.75f * gust) * dt;
                    f.vx += (wind * 0.9f - f.vx) * Math.Min(1f, (0.6f + 3.5f * gust) * dt);
                    f.x += f.vx * dt; f.y += f.vy * dt;
                    if (gust > 0.3f && f.vy > 0 && f.y > f.gy - 2f * S) f.vy = -(40f + Rnd() * 160f) * S;   // ветер подхватывает с поверхности
                    else if (f.y > f.gy && f.vy > 0)
                    {
                        f.y = f.gy; f.vy *= -0.3f; f.vx *= 0.55f;
                        if (Math.Abs(f.vy) < 40f * S) f.vy = 0;
                    }
                }
                else if (f.kind == 5)
                {
                    f.vy += 1400f * S * dt;
                    f.vx += (wind * 0.7f - f.vx) * Math.Min(1f, (0.5f + 3f * gust) * dt);
                    f.x += f.vx * dt; f.y += f.vy * dt;
                    if (f.y > vT + vH) f.life = -1;
                }
                else if (f.kind == 1)
                {
                    f.vy += 520f * S * dt;
                    f.x += f.vx * dt; f.y += f.vy * dt;
                    FW tr = new FW();
                    tr.kind = 3; tr.x = f.x; tr.y = f.y; tr.vy = 30f * S;
                    tr.life = tr.max = 0.4f; tr.size = 0.9f * S;
                    AddFw(tr);
                    if (f.vy > -70f * S || f.life <= 0) { Explode(f); f.life = -1; }
                }
                else
                {
                    float dr = Math.Max(0f, 1f - (f.kind == 3 ? 3f : 1.4f) * dt);
                    f.vx *= dr; f.vy *= dr;
                    f.vy += (f.kind == 3 ? 60f * S : gs) * dt;
                    f.x += f.vx * dt; f.y += f.vy * dt;
                }
                if (f.life <= 0) { fw[i] = fw[--fc]; continue; }
                fw[i] = f; i++;
            }
        }

        // ---------- отрисовка ----------
        // Вместо очистки всего экрана каждый кадр стираем только прямоугольники, где рисовали в прошлый раз.
        int[] dA = new int[16384], dB = new int[16384];
        int dAn, dBn;

        void Mark(int x0, int y0, int x1, int y1)
        {
            if (dBn + 4 > dB.Length) Array.Resize(ref dB, dB.Length * 2);
            dB[dBn++] = x0; dB[dBn++] = y0; dB[dBn++] = x1; dB[dBn++] = y1;
        }

        void Clear()
        {
            long n = (long)vW * vH;
            ulong* q = (ulong*)buf;
            long n2 = n / 2;
            for (long i = 0; i < n2; i++) q[i] = 0;
            if ((n & 1) != 0) buf[n - 1] = 0;
        }

        void ClearDirty()
        {
            for (int i = 0; i < dAn; i += 4)
            {
                int x0 = dA[i], y0 = dA[i + 1], x1 = dA[i + 2], y1 = dA[i + 3];
                for (int y = y0; y < y1; y++)
                {
                    uint* row = buf + (long)y * vW;
                    for (int x = x0; x < x1; x++) row[x] = 0;
                }
            }
            dAn = 0;
        }

        void Present()
        {
            N.GdiFlush();
            N.POINT pt = new N.POINT(vL, vT), src = new N.POINT(0, 0);
            N.SIZE sz = new N.SIZE(vW, vH);
            N.BLENDFUNCTION bf = new N.BLENDFUNCTION(); bf.Alpha = 255; bf.Format = 1;
            if (!N.UpdateLayeredWindow(ov.Handle, screenDC, ref pt, ref sz, memDC, ref src, 0, ref bf, 2))
            {
                dUlwFail++; dLastErr = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
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
            if (alpha <= 0.004f) return;
            float bx = cx - vL, by = cy - vT;
            int x0 = (int)Math.Floor(bx - r - 1), x1 = (int)Math.Ceiling(bx + r + 1);
            int y0 = (int)Math.Floor(by - r - 1), y1 = (int)Math.Ceiling(by + r + 1);
            if (x0 < 0) x0 = 0; if (y0 < 0) y0 = 0;
            if (x1 > vW) x1 = vW; if (y1 > vH) y1 = vH;
            if (x0 >= x1 || y0 >= y1) return;
            Mark(x0, y0, x1, y1);
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
            int mx0 = int.MaxValue, my0 = int.MaxValue, mx1 = -1, my1 = -1;
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
                if (bx < mx0) mx0 = bx; if (bx + 1 > mx1) mx1 = bx + 1;
                if (by0 < my0) my0 = by0; if (by1 > my1) my1 = by1;

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
            if (mx1 > mx0) Mark(mx0, my0, mx1, my1);
        }

        void DrawFireworks()
        {
            for (int i = 0; i < fc; i++)
            {
                FW f = fw[i];
                float lf = f.life / f.max;
                switch (f.kind)
                {
                    case 1: Dot(f.x, f.y, f.size, 1f, 255, 232, 190); break;
                    case 2: Dot(f.x, f.y, f.size * (2f - lf), 0.5f * lf, f.r, f.g, f.b); break;
                    case 3: Dot(f.x, f.y, f.size, 0.5f * lf, 255, 190, 120); break;
                    case 4:
                        float ia = Math.Min(1f, lf * 1.8f);
                        if (((i + frame) % 7) == 0) Dot(f.x, f.y, f.size, ia, 255, 255, 255);   // искорка на гранях
                        else Dot(f.x, f.y, f.size, ia * 0.85f, f.r, f.g, f.b);
                        break;
                    case 5:
                        Dot(f.x, f.y - 2.5f * S, f.size * 0.8f, 0.3f, 190, 225, 255);
                        Dot(f.x, f.y, f.size, 0.75f, 190, 225, 255);
                        break;
                    default:
                        float a = Math.Min(1f, lf * 1.6f);
                        if (lf < 0.45f) a *= 0.55f + 0.45f * (float)Math.Sin(t * 38 + i * 1.7);   // мерцание в конце
                        float w = lf * lf * lf * 0.7f;                                         // свежие искры белее
                        Dot(f.x, f.y, f.size, a,
                            (int)(f.r + (255 - f.r) * w), (int)(f.g + (255 - f.g) * w), (int)(f.b + (255 - f.b) * w));
                        break;
                }
            }
        }

        // Кадр слоя снега поверх тёмного фона → PNG (только для отладки, режим --snapshot).
        void SaveSnapshot(string path)
        {
            try
            {
                using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(vW, vH, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    System.Drawing.Imaging.BitmapData bd = bmp.LockBits(new System.Drawing.Rectangle(0, 0, vW, vH),
                        System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    for (int y = 0; y < vH; y++)
                    {
                        uint* src = buf + (long)y * vW;
                        uint* dst = (uint*)((byte*)bd.Scan0 + (long)y * bd.Stride);
                        for (int x = 0; x < vW; x++)
                        {
                            uint p = src[x]; int ia = 255 - (int)(p >> 24);
                            int r = (int)((p >> 16) & 255) + 34 * ia / 255, g = (int)((p >> 8) & 255) + 38 * ia / 255, b = (int)(p & 255) + 48 * ia / 255;
                            dst[x] = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
                        }
                    }
                    bmp.UnlockBits(bd);
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        void Render()
        {
            ClearDirty();
            bool pixel = C.Style >= 2;   // «Блоки» и «Пиксели» — с блочными сугробами
            if (C.Drifts)
                for (int i = z.Count - 1; i >= 0; i--)
                    if (z[i].Enabled) { if (pixel) DrawPilePixel(z[i]); else DrawPile(z[i]); }
            DrawChunks();
            DrawIcicles();
            float rMul = (1f - 0.35f * calm) * C.Size, aMul = 1f - 0.5f * calm;
            bool front = C.Front;
            for (int i = 0; i < pc; i++)
            {
                if (!front && BehindWindow(ps[i].x, ps[i].y)) continue;   // снег только на рабочем столе
                if (ps[i].clump)
                {
                    if (pixel) Block(ps[i].x, ps[i].y, ps[i].r, 0.95f, 244, 248, 255);
                    else Particle(ps[i].x, ps[i].y, ps[i].r, 0.95f, ps[i].hv, 244, 248, 255);
                }
                else
                {
                    float d = ps[i].d;
                    // кристаллы медленно вращаются, каждый в свою сторону
                    float rot = ps[i].ph + (float)t * (0.25f + 0.35f * ps[i].fr) * (ps[i].ph > 3.14f ? 1f : -1f);
                    StyledFlake(ps[i].x, ps[i].y, ps[i].r * rMul, ps[i].a * aMul, ps[i].hv, rot, (int)(222 + 33 * d), (int)(232 + 23 * d), 255);
                }
            }
            DrawFireworks();
            Present();
            int[] tmp = dA; dA = dB; dB = tmp; dAn = dBn; dBn = 0;
        }
    }
}
