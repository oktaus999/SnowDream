// SnowDream: город пользователя — от него считается время до Нового года и запускается салют.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DesktopSnow
{
    class City
    {
        public string Ru, En, Tz;
        public City(string ru, string en, string tz) { Ru = ru; En = en; Tz = tz; }
        public string Name { get { return L.En ? En : Ru; } }
    }

    static class Cities
    {
        public const string Local = "local";   // «по часам этого компьютера»

        // Часовые пояса — стандартные идентификаторы Windows.
        public static readonly City[] All = {
            new City("Калининград", "Kaliningrad", "Kaliningrad Standard Time"),
            new City("Москва", "Moscow", "Russian Standard Time"),
            new City("Санкт-Петербург", "Saint Petersburg", "Russian Standard Time"),
            new City("Нижний Новгород", "Nizhny Novgorod", "Russian Standard Time"),
            new City("Казань", "Kazan", "Russian Standard Time"),
            new City("Воронеж", "Voronezh", "Russian Standard Time"),
            new City("Ростов-на-Дону", "Rostov-on-Don", "Russian Standard Time"),
            new City("Краснодар", "Krasnodar", "Russian Standard Time"),
            new City("Сочи", "Sochi", "Russian Standard Time"),
            new City("Симферополь", "Simferopol", "Russian Standard Time"),
            new City("Ярославль", "Yaroslavl", "Russian Standard Time"),
            new City("Тула", "Tula", "Russian Standard Time"),
            new City("Мурманск", "Murmansk", "Russian Standard Time"),
            new City("Архангельск", "Arkhangelsk", "Russian Standard Time"),
            new City("Махачкала", "Makhachkala", "Russian Standard Time"),
            new City("Волгоград", "Volgograd", "Volgograd Standard Time"),
            new City("Саратов", "Saratov", "Saratov Standard Time"),
            new City("Астрахань", "Astrakhan", "Astrakhan Standard Time"),
            new City("Ульяновск", "Ulyanovsk", "Astrakhan Standard Time"),
            new City("Самара", "Samara", "Russia Time Zone 3"),
            new City("Ижевск", "Izhevsk", "Russia Time Zone 3"),
            new City("Екатеринбург", "Yekaterinburg", "Ekaterinburg Standard Time"),
            new City("Челябинск", "Chelyabinsk", "Ekaterinburg Standard Time"),
            new City("Пермь", "Perm", "Ekaterinburg Standard Time"),
            new City("Уфа", "Ufa", "Ekaterinburg Standard Time"),
            new City("Тюмень", "Tyumen", "Ekaterinburg Standard Time"),
            new City("Оренбург", "Orenburg", "Ekaterinburg Standard Time"),
            new City("Сургут", "Surgut", "Ekaterinburg Standard Time"),
            new City("Омск", "Omsk", "Omsk Standard Time"),
            new City("Новосибирск", "Novosibirsk", "N. Central Asia Standard Time"),
            new City("Барнаул", "Barnaul", "Altai Standard Time"),
            new City("Томск", "Tomsk", "Tomsk Standard Time"),
            new City("Кемерово", "Kemerovo", "North Asia Standard Time"),
            new City("Новокузнецк", "Novokuznetsk", "North Asia Standard Time"),
            new City("Красноярск", "Krasnoyarsk", "North Asia Standard Time"),
            new City("Иркутск", "Irkutsk", "North Asia East Standard Time"),
            new City("Улан-Удэ", "Ulan-Ude", "North Asia East Standard Time"),
            new City("Чита", "Chita", "Transbaikal Standard Time"),
            new City("Якутск", "Yakutsk", "Yakutsk Standard Time"),
            new City("Благовещенск", "Blagoveshchensk", "Yakutsk Standard Time"),
            new City("Хабаровск", "Khabarovsk", "Vladivostok Standard Time"),
            new City("Владивосток", "Vladivostok", "Vladivostok Standard Time"),
            new City("Южно-Сахалинск", "Yuzhno-Sakhalinsk", "Sakhalin Standard Time"),
            new City("Магадан", "Magadan", "Magadan Standard Time"),
            new City("Петропавловск-Камчатский", "Petropavlovsk-Kamchatsky", "Russia Time Zone 11"),
            new City("Анадырь", "Anadyr", "Russia Time Zone 11"),
            new City("Минск", "Minsk", "Belarus Standard Time"),
            new City("Киев", "Kyiv", "FLE Standard Time"),
            new City("Кишинёв", "Chisinau", "E. Europe Standard Time"),
            new City("Рига", "Riga", "FLE Standard Time"),
            new City("Вильнюс", "Vilnius", "FLE Standard Time"),
            new City("Таллин", "Tallinn", "FLE Standard Time"),
            new City("Тбилиси", "Tbilisi", "Georgian Standard Time"),
            new City("Ереван", "Yerevan", "Caucasus Standard Time"),
            new City("Баку", "Baku", "Azerbaijan Standard Time"),
            new City("Астана", "Astana", "West Asia Standard Time"),
            new City("Алматы", "Almaty", "West Asia Standard Time"),
            new City("Ташкент", "Tashkent", "West Asia Standard Time"),
            new City("Душанбе", "Dushanbe", "West Asia Standard Time"),
            new City("Бишкек", "Bishkek", "Central Asia Standard Time"),
            new City("Ашхабад", "Ashgabat", "West Asia Standard Time"),
            new City("Лондон", "London", "GMT Standard Time"),
            new City("Берлин", "Berlin", "W. Europe Standard Time"),
            new City("Париж", "Paris", "Romance Standard Time"),
            new City("Рим", "Rome", "W. Europe Standard Time"),
            new City("Мадрид", "Madrid", "Romance Standard Time"),
            new City("Прага", "Prague", "Central Europe Standard Time"),
            new City("Варшава", "Warsaw", "Central European Standard Time"),
            new City("Хельсинки", "Helsinki", "FLE Standard Time"),
            new City("Стамбул", "Istanbul", "Turkey Standard Time"),
            new City("Тель-Авив", "Tel Aviv", "Israel Standard Time"),
            new City("Дубай", "Dubai", "Arabian Standard Time"),
            new City("Дели", "Delhi", "India Standard Time"),
            new City("Бангкок", "Bangkok", "SE Asia Standard Time"),
            new City("Пекин", "Beijing", "China Standard Time"),
            new City("Сингапур", "Singapore", "Singapore Standard Time"),
            new City("Сеул", "Seoul", "Korea Standard Time"),
            new City("Токио", "Tokyo", "Tokyo Standard Time"),
            new City("Сидней", "Sydney", "AUS Eastern Standard Time"),
            new City("Нью-Йорк", "New York", "Eastern Standard Time"),
            new City("Торонто", "Toronto", "Eastern Standard Time"),
            new City("Чикаго", "Chicago", "Central Standard Time"),
            new City("Денвер", "Denver", "Mountain Standard Time"),
            new City("Лос-Анджелес", "Los Angeles", "Pacific Standard Time"),
            new City("Мехико", "Mexico City", "Central Standard Time (Mexico)"),
            new City("Сан-Паулу", "Sao Paulo", "E. South America Standard Time"),
            new City("Буэнос-Айрес", "Buenos Aires", "Argentina Standard Time") };

        public static City Find(string en)
        {
            foreach (City c in All) if (c.En == en) return c;
            return null;
        }

        // Часовой пояс города; null — брать часы компьютера.
        public static TimeZoneInfo Zone(string en)
        {
            City c = Find(en);
            if (c == null) return null;
            try { return TimeZoneInfo.FindSystemTimeZoneById(c.Tz); } catch { return null; }
        }

        public static List<City> Search(string q)
        {
            List<City> res = new List<City>();
            q = (q ?? "").Trim().ToLowerInvariant();
            foreach (City c in All)
                if (q.Length == 0 || c.Ru.ToLowerInvariant().Contains(q) || c.En.ToLowerInvariant().Contains(q)) res.Add(c);
            return res;
        }
    }

    // Список городов с прокруткой колёсиком. Первая строка — «по часам этого компьютера».
    class CityList : Widget
    {
        public List<City> Items = new List<City>();   // null-элемент в начале = часы компьютера
        public int Sel = -1, Scroll;
        public Action Confirm;
        float RowH { get { return 34 * s; } }
        int Visible { get { return (int)(B.Height / RowH); } }
        int hoverRow = -1; DateTime lastClick; int lastRow = -1;

        int RowAt(PointF p) { int r = (int)((p.Y - B.Y) / RowH) + Scroll; return r >= 0 && r < Items.Count ? r : -1; }

        public void EnsureVisible()
        {
            if (Sel < Scroll) Scroll = Sel;
            if (Sel >= Scroll + Visible) Scroll = Sel - Visible + 1;
            if (Scroll < 0) Scroll = 0;
        }

        public override void Paint(Graphics g)
        {
            Th.Fill(g, B, 10 * s, Th.Field);
            GraphicsState st = g.Save();
            g.SetClip(B);
            for (int i = 0; i < Visible + 1; i++)
            {
                int idx = Scroll + i;
                if (idx >= Items.Count) break;
                RectangleF r = new RectangleF(B.X + 4 * s, B.Y + i * RowH + 2 * s, B.Width - 8 * s, RowH - 4 * s);
                if (idx == Sel) Th.Fill(g, r, 8 * s, Th.Accent);
                else if (Hover && idx == hoverRow) Th.Fill(g, r, 8 * s, Th.Mix(Th.Field, Th.Text, 0.08f));
                City c = Items[idx];
                Color col = idx == Sel ? Th.AccentText : Th.Text;
                string name = c == null ? L.T("По часам этого компьютера", "Use this computer's clock") : c.Name;
                Th.Text2(g, name, idx == Sel ? F.FTextB : F.FText, col, new RectangleF(r.X + 12 * s, r.Y, r.Width - 24 * s, r.Height), StringAlignment.Near);
                if (c != null)
                {
                    TimeZoneInfo tz = Cities.Zone(c.En);
                    if (tz != null)
                    {
                        TimeSpan off = tz.GetUtcOffset(DateTime.UtcNow);
                        string utc = "UTC" + (off < TimeSpan.Zero ? "−" : "+") + Math.Abs(off.Hours) + (off.Minutes != 0 ? ":" + Math.Abs(off.Minutes).ToString("00") : "");
                        Th.Text2(g, utc, F.FSub, idx == Sel ? Th.AccentText : Th.Sub, new RectangleF(r.X, r.Y, r.Width - 12 * s, r.Height), StringAlignment.Far);
                    }
                }
            }
            g.Restore(st);
            if (Items.Count == 0)
                Th.Text2(g, L.T("Ничего не нашлось", "Nothing found"), F.FText, Th.Sub, B, StringAlignment.Center);
        }

        public override void OnMove(PointF p) { int r = RowAt(p); if (r != hoverRow) { hoverRow = r; F.Invalidate(); } }

        public override void OnUp(PointF p, bool inside)
        {
            int r = RowAt(p);
            if (!inside || r < 0) return;
            bool dbl = r == lastRow && (DateTime.Now - lastClick).TotalMilliseconds < 450;
            Sel = r; lastRow = r; lastClick = DateTime.Now;
            F.Invalidate();
            if (dbl && Confirm != null) Confirm();
        }

        public override void OnWheel(int delta)
        {
            int max = Math.Max(0, Items.Count - Visible);
            Scroll = Math.Max(0, Math.Min(max, Scroll - Math.Sign(delta) * 3));
            F.Invalidate();
        }
    }

    // Окно «Где вы встречаете Новый год?»
    class CityForm : SettingsForm
    {
        TextBox search;
        CityList list;
        Action done;

        public CityForm(SnowApp app, Action onDone) : base(app)
        {
            done = onDone;
            Text = "SnowDream — " + L.T("город", "city");
            Shown += delegate { search.Focus(); };
        }

        protected override void PaintHeader(Graphics g)
        {
            float ix = P(22), iy = P(20);
            Th.Text2(g, L.T("Где встречаете Новый год?", "Where do you celebrate New Year?"), FTitle, Th.Text, new RectangleF(ix, iy - P(2), P(360), P(22)), StringAlignment.Near);
            Th.Text2(g, L.T("Отсчёт и салют будут по времени этого города", "The countdown and fireworks will follow this city's time"), FSub, Th.Sub, new RectangleF(ix, iy + P(19), P(380), P(18)), StringAlignment.Near);
        }

        protected override void Build()
        {
            float pad = P(20), W = P(430), inner = W - 2 * pad;
            headerH = P(74);
            Add(new CloseBtn(), W - pad - P(34), P(20), P(34), P(34));

            // поле поиска: фон рисуем сами, внутри — обычный TextBox без рамки
            float y = headerH + P(4);
            Add(new SearchBg(), pad, y, inner, P(40));
            if (search == null)
            {
                search = new TextBox();
                search.BorderStyle = BorderStyle.None;
                search.Font = FText;
                search.TextChanged += delegate { Filter(); };
                search.KeyDown += OnSearchKey;
                Controls.Add(search);
            }
            search.BackColor = Th.Field; search.ForeColor = Th.Text;
            search.Location = new Point((int)(pad + P(38)), (int)(y + (P(40) - search.PreferredHeight) / 2));
            search.Width = (int)(inner - P(50));
            y += P(40 + 10);

            list = new CityList();
            list.Confirm = Confirm;
            Add(list, pad, y, inner, P(34) * 9);
            y += P(34) * 9 + P(14);

            Btn ok = new Btn(delegate { return L.T("Готово", "Done"); }, Confirm, true, delegate { return list.Sel >= 0; });
            Add(ok, pad, y, inner, P(46));
            y += P(46 + 16);
            ClientSize = new Size((int)W, (int)y);

            Filter();
            // текущий выбор сразу подсвечиваем
            City cur = Cities.Find(App.C.City);
            for (int i = 0; i < list.Items.Count; i++)
                if (list.Items[i] == cur && (cur != null || App.C.City == Cities.Local)) { list.Sel = i; list.EnsureVisible(); break; }
        }

        void Filter()
        {
            if (list == null) return;
            List<City> found = Cities.Search(search.Text);
            list.Items.Clear();
            if (search.Text.Trim().Length == 0) list.Items.Add(null);   // «по часам компьютера» — первой строкой
            list.Items.AddRange(found);
            list.Scroll = 0;
            list.Sel = search.Text.Trim().Length > 0 && list.Items.Count > 0 ? 0 : -1;
            Invalidate();
        }

        void OnSearchKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down) { list.Sel = Math.Min(list.Items.Count - 1, list.Sel + 1); list.EnsureVisible(); Invalidate(); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { list.Sel = Math.Max(0, list.Sel - 1); list.EnsureVisible(); Invalidate(); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { Confirm(); e.Handled = true; e.SuppressKeyPress = true; }
        }

        void Confirm()
        {
            if (list.Sel < 0 || list.Sel >= list.Items.Count) return;
            City c = list.Items[list.Sel];
            App.SetCity(c == null ? Cities.Local : c.En);
            Action d = done; done = null;
            Close();
            if (d != null) d();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // закрыли без выбора — считаем по часам компьютера, чтобы не спрашивать снова
            if (string.IsNullOrEmpty(App.C.City)) App.SetCity(Cities.Local);
            base.OnFormClosed(e);
            Action d = done; done = null;
            if (d != null) d();
        }

        public void ThemeRefreshCity() { if (search != null) { search.BackColor = Th.Field; search.ForeColor = Th.Text; } }
    }

    // Фон поля поиска с лупой.
    class SearchBg : Widget
    {
        public override bool Clickable { get { return false; } }
        public override void Paint(Graphics g)
        {
            Th.Fill(g, B, 10 * s, Th.Field);
            using (Pen p = new Pen(Th.Sub, 1.8f * s))
            {
                float cx = B.X + 18 * s, cy = B.Y + B.Height / 2 - 1 * s, r = 6 * s;
                g.DrawEllipse(p, cx - r, cy - r, 2 * r, 2 * r);
                g.DrawLine(p, cx + r * 0.7f, cy + r * 0.7f, cx + r * 1.5f, cy + r * 1.5f);
            }
        }
    }
}
