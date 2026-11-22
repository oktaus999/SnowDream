// SnowDream: редактор «своей» темы — фото на фон и палитра цветов.
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DesktopSnow
{
    // Строка выбора цвета: готовая палитра и «…» — любой цвет через стандартный диалог Windows.
    class ColorRow : Widget
    {
        string label; Func<int> get; Action<int> set;
        int hoverIdx = -1;
        public ColorRow(string label, Func<int> get, Action<int> set) { this.label = label; this.get = get; this.set = set; }

        float Chip { get { return 19 * s; } }
        float Gap { get { return 4 * s; } }
        float RowY { get { return B.Y + 26 * s; } }
        int Count { get { return Th.Palette.Length + 1; } }

        int IndexAt(PointF p)
        {
            if (p.Y < RowY - 2 * s || p.Y > RowY + Chip + 2 * s) return -1;
            int i = (int)((p.X - B.X) / (Chip + Gap));
            return i >= 0 && i < Count ? i : -1;
        }

        public override void Paint(Graphics g)
        {
            Color cur = Color.FromArgb(255, Color.FromArgb(get()));
            Th.Text2(g, label, F.FText, Th.Text, new RectangleF(B.X, B.Y, B.Width, 20 * s), StringAlignment.Near);
            // текущий цвет и его код справа
            string hex = "#" + cur.R.ToString("X2") + cur.G.ToString("X2") + cur.B.ToString("X2");
            Th.Text2(g, hex, F.FSub, Th.Sub, new RectangleF(B.X, B.Y, B.Width - 30 * s, 20 * s), StringAlignment.Far);
            Th.Fill(g, new RectangleF(B.Right - 24 * s, B.Y + 3 * s, 24 * s, 14 * s), 4 * s, cur);

            for (int i = 0; i < Count; i++)
            {
                float x = B.X + i * (Chip + Gap), y = RowY;
                bool last = i == Count - 1;
                Color c = last ? Th.Field : Th.Palette[i];
                bool sel = !last && c.ToArgb() == cur.ToArgb();
                if (sel || (Hover && hoverIdx == i))
                    using (Pen ring = new Pen(sel ? Th.Text : Th.Sub, 1.5f * s))
                        g.DrawEllipse(ring, x - 2.5f * s, y - 2.5f * s, Chip + 5 * s, Chip + 5 * s);
                using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x, y, Chip, Chip);
                using (Pen edge = new Pen(Color.FromArgb(60, 255, 255, 255), 1f)) g.DrawEllipse(edge, x, y, Chip, Chip);
                if (last) Th.Text2(g, "…", F.FTextB, Th.Text, new RectangleF(x, y - 3 * s, Chip, Chip), StringAlignment.Center);
            }
        }

        public override void OnMove(PointF p) { int i = IndexAt(p); if (i != hoverIdx) { hoverIdx = i; F.Invalidate(); } }

        public override void OnUp(PointF p, bool inside)
        {
            int i = IndexAt(p);
            if (!inside || i < 0) return;
            if (i < Th.Palette.Length) set(Th.Palette[i].ToArgb());
            else
            {
                using (ColorDialog dlg = new ColorDialog())
                {
                    dlg.FullOpen = true;
                    dlg.Color = Color.FromArgb(255, Color.FromArgb(get()));
                    if (dlg.ShowDialog(F) != DialogResult.OK) return;
                    set(dlg.Color.ToArgb());
                }
            }
            F.App.RefreshTheme();
            F.Changed();
        }
    }

    class ThemeEditorForm : SettingsForm
    {
        public ThemeEditorForm(SnowApp app) : base(app)
        {
            Text = "SnowDream — " + L.T("своя тема", "custom theme");
            StartPosition = FormStartPosition.Manual;
        }

        protected override void PaintHeader(Graphics g)
        {
            float ix = P(22), iy = P(20);
            Th.Text2(g, L.T("Своя тема", "Custom theme"), FTitle, Th.Text, new RectangleF(ix, iy - P(2), P(300), P(22)), StringAlignment.Near);
            Th.Text2(g, L.T("Фото на фон и свои цвета", "Background photo and your colors"), FSub, Th.Sub, new RectangleF(ix, iy + P(19), P(300), P(18)), StringAlignment.Near);
        }

        protected override void Build()
        {
            Config C = App.C;
            float pad = P(20), gap = P(14), W = P(380), cardW = W - 2 * pad;
            headerH = P(74);
            Add(new CloseBtn(), W - pad - P(34), P(20), P(34), P(34));

            float y = headerH;
            BeginCard(pad, y, cardW, L.T("ФОТО НА ФОНЕ", "BACKGROUND PHOTO"));
            float inner = cw - P(36), bw = (float)Math.Round(inner * 0.62f);
            Btn pick = new Btn(delegate { return string.IsNullOrEmpty(C.CustomPhoto) ? L.T("Выбрать фото…", "Choose a photo…") : L.T("Другое фото…", "Another photo…"); },
                               PickPhoto, false, null);
            pick.Secondary = true;
            Add(pick, cx + P(18), cy, bw, P(36));
            Btn rm = new Btn(delegate { return L.T("Убрать", "Remove"); }, RemovePhoto, false, delegate { return !string.IsNullOrEmpty(C.CustomPhoto); });
            rm.Secondary = true;
            Add(rm, cx + P(18) + bw + P(8), cy, inner - bw - P(8), P(36));
            cy += P(36 + 14);
            RowCaption(delegate { return L.T("Затемнение фото", "Photo dimming"); }, delegate { return C.CustomDim + "%"; }, false);
            RowSlider(0, 90, 5, delegate { return C.CustomDim; }, delegate(float v) { C.CustomDim = (int)Math.Round(v); App.RefreshTheme(); });
            y = EndCard() + gap;

            BeginCard(pad, y, cardW, L.T("ЦВЕТА", "COLORS"));
            RowColor(L.T("Фон", "Background"), delegate { return C.CustomBg; }, delegate(int v) { C.CustomBg = v; });
            RowColor(L.T("Акцент", "Accent"), delegate { return C.CustomAccent; }, delegate(int v) { C.CustomAccent = v; });
            RowColor(L.T("Второй цвет кнопки", "Button gradient"), delegate { return C.CustomAccent2; }, delegate(int v) { C.CustomAccent2 = v; });
            y = EndCard() + P(10);

            Add(new Caption(delegate { return L.T("Изменения сразу видны в окне настроек", "Changes show up in the settings window right away"); }, null, true),
                pad + P(4), y, cardW, P(28));
            y += P(28 + 12);
            ClientSize = new Size((int)W, (int)y);
        }

        void RowColor(string label, Func<int> get, Action<int> set)
        {
            Add(new ColorRow(label, get, set), cx + P(18), cy, cw - P(36), P(48));
            cy += P(48 + 10);
        }

        void PickPhoto()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = L.T("Фото на фон окна", "Background photo");
                dlg.Filter = L.T("Изображения", "Images") + "|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (Image test = Image.FromFile(dlg.FileName)) { }   // проверяем, что это картинка
                    Directory.CreateDirectory(Config.Dir);
                    // копируем к себе: фото останется, даже если оригинал удалят или переместят
                    string name = "custom-bg-" + DateTime.Now.Ticks + Path.GetExtension(dlg.FileName).ToLowerInvariant();
                    File.Copy(dlg.FileName, Path.Combine(Config.Dir, name), true);
                    DeleteOldPhoto();
                    App.C.CustomPhoto = name;
                    App.RefreshTheme();
                    Changed();
                }
                catch
                {
                    MessageBox.Show(this, L.T("Не получилось открыть это изображение.", "Couldn't open this image."), "SnowDream");
                }
            }
        }

        void RemovePhoto()
        {
            DeleteOldPhoto();
            App.C.CustomPhoto = "";
            App.RefreshTheme();
            Changed();
        }

        void DeleteOldPhoto()
        {
            if (string.IsNullOrEmpty(App.C.CustomPhoto)) return;
            try { File.Delete(Path.Combine(Config.Dir, App.C.CustomPhoto)); } catch { }
        }
    }
}
