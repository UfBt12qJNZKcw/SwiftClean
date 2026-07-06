using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SwiftClean
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // ---- Native (Recycle Bin) ----
    static class Native
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

        public const uint SHERB_NOCONFIRMATION = 0x01, SHERB_NOPROGRESSUI = 0x02, SHERB_NOSOUND = 0x04;
    }

    class CleanCategory
    {
        public string Name;
        public bool IsRecycleBin;
        public bool NeedsAdmin;
        public Func<List<string>> DirsToClear;
        public long ScannedBytes = -1;

        public override string ToString()
        {
            string size = ScannedBytes < 0 ? "" : "   —   " + Util.Human(ScannedBytes);
            return Name + size;
        }
    }

    static class Util
    {
        public static string Human(long b)
        {
            string[] u = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
            double v = b; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return (i == 0 ? v.ToString("0") : v.ToString("0.0")) + " " + u[i];
        }

        public static void CollectFiles(string dir, List<string> acc)
        {
            try { foreach (var f in Directory.GetFiles(dir)) acc.Add(f); } catch { }
            try { foreach (var d in Directory.GetDirectories(dir)) CollectFiles(d, acc); } catch { }
        }

        static readonly string[] LockedExt = { ".vhdx", ".vhd", ".sys" };

        // Считаем файл "удаляемым", чтобы «найдено» совпадало с «освобождено».
        // Виртуальные диски/своп (.vhdx и т.п.) и крупные залоченные файлы исключаем.
        public static bool Deletable(string path, long len)
        {
            string ext = Path.GetExtension(path);
            foreach (var e in LockedExt) if (string.Equals(ext, e, StringComparison.OrdinalIgnoreCase)) return false;
            if (len < 20L * 1024 * 1024) return true; // мелкие почти всегда удаляемы
            try { using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { } return true; }
            catch { return false; }
        }

        public static long DirSize(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;
            var files = new List<string>();
            CollectFiles(dir, files);
            long total = 0;
            foreach (var f in files)
            {
                try { long len = new FileInfo(f).Length; if (Deletable(f, len)) total += len; } catch { }
            }
            return total;
        }

        public static long ClearDir(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;
            long freed = 0;
            var files = new List<string>();
            CollectFiles(dir, files);
            foreach (var f in files)
            {
                try { long s = new FileInfo(f).Length; if (!Deletable(f, s)) continue; File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); freed += s; }
                catch { }
            }
            try { foreach (var sub in Directory.GetDirectories(dir)) try { Directory.Delete(sub, true); } catch { } }
            catch { }
            return freed;
        }

        static readonly string[] ChromiumCaches = { "Cache", "Code Cache", "GPUCache", @"Service Worker\CacheStorage" };

        static void AddChromiumCaches(string userDataRoot, List<string> list)
        {
            if (!Directory.Exists(userDataRoot)) return;
            var profiles = new List<string> { Path.Combine(userDataRoot, "Default") };
            try { profiles.AddRange(Directory.GetDirectories(userDataRoot, "Profile *")); } catch { }
            foreach (var p in profiles)
                foreach (var c in ChromiumCaches)
                {
                    var cd = Path.Combine(p, c);
                    if (Directory.Exists(cd)) list.Add(cd);
                }
        }

        public static List<string> BrowserCacheDirs()
        {
            var lad = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var list = new List<string>();
            AddChromiumCaches(Path.Combine(lad, @"Google\Chrome\User Data"), list);
            AddChromiumCaches(Path.Combine(lad, @"Microsoft\Edge\User Data"), list);
            AddChromiumCaches(Path.Combine(lad, @"Yandex\YandexBrowser\User Data"), list);
            AddChromiumCaches(Path.Combine(lad, @"BraveSoftware\Brave-Browser\User Data"), list);
            var opera = Path.Combine(lad, @"Opera Software\Opera Stable\Cache");
            if (Directory.Exists(opera)) list.Add(opera);
            var operaGx = Path.Combine(lad, @"Opera Software\Opera GX Stable\Cache");
            if (Directory.Exists(operaGx)) list.Add(operaGx);
            var ffRoot = Path.Combine(lad, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(ffRoot))
                try { foreach (var prof in Directory.GetDirectories(ffRoot)) { var c = Path.Combine(prof, "cache2"); if (Directory.Exists(c)) list.Add(c); } } catch { }
            return list;
        }
    }

    class MainForm : Form
    {
        readonly List<CleanCategory> _cats = new List<CleanCategory>();
        CheckedListBox _clb;
        Button _btnScan, _btnClean, _btnFolder, _btnRefresh, _btnDisable;
        Label _lblTotal, _lblStatus;
        ProgressBar _bar;
        ListView _lvStartup;

        static readonly Color Bg = Color.FromArgb(15, 18, 32);
        static readonly Color Card = Color.FromArgb(24, 28, 46);
        static readonly Color Txt = Color.FromArgb(233, 237, 247);
        static readonly Color Mut = Color.FromArgb(154, 163, 189);
        static readonly Color Accent = Color.FromArgb(46, 204, 113);

        public MainForm()
        {
            BuildCategories();
            BuildUi();
        }

        void BuildCategories()
        {
            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var lad = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            _cats.Add(new CleanCategory { Name = "Временные файлы Windows", DirsToClear = () => new List<string> { Path.GetTempPath(), Path.Combine(win, "Temp") } });
            _cats.Add(new CleanCategory { Name = "Кэш браузеров", DirsToClear = Util.BrowserCacheDirs });
            _cats.Add(new CleanCategory { Name = "Корзина", IsRecycleBin = true, DirsToClear = () => new List<string>() });
            _cats.Add(new CleanCategory { Name = "Отчёты об ошибках", DirsToClear = () => new List<string> { Path.Combine(lad, @"Microsoft\Windows\WER"), Path.Combine(progData, @"Microsoft\Windows\WER") } });
            _cats.Add(new CleanCategory { Name = "Хвосты обновлений Windows", NeedsAdmin = true, DirsToClear = () => new List<string> { Path.Combine(win, @"SoftwareDistribution\Download") } });
        }

        void BuildUi()
        {
            Text = "SwiftClean — Очистка компьютера";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(660, 620);
            MinimumSize = new Size(560, 560);
            BackColor = Bg;
            ForeColor = Txt;
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Font;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            var tabClean = new TabPage("Очистка") { BackColor = Bg, UseVisualStyleBackColor = false };
            var tabStartup = new TabPage("Автозагрузка") { BackColor = Bg, UseVisualStyleBackColor = false };
            tabs.TabPages.Add(tabClean);
            tabs.TabPages.Add(tabStartup);

            var top = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Bg };
            var brand = new Label { Text = "✦  SwiftClean", ForeColor = Accent, Font = new Font("Segoe UI", 18f, FontStyle.Bold), AutoSize = true, Location = new Point(16, 11) };
            var tagline = new Label { Text = "Очистка компьютера — бесплатно, без рекламы", ForeColor = Mut, Font = new Font("Segoe UI", 9.5f), AutoSize = true, Location = new Point(20, 45) };
            top.Controls.Add(brand);
            top.Controls.Add(tagline);

            Controls.Add(tabs);   // Fill (added first)
            Controls.Add(top);    // Top (added after)

            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildCleanTab(tabClean);
            BuildStartupTab(tabStartup);
        }

        void BuildCleanTab(TabPage tab)
        {
            var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Bg, Padding = new Padding(14) };
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

            _clb = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Card,
                ForeColor = Txt,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                IntegralHeight = false,
                Font = new Font("Segoe UI", 11f),
                ItemHeight = 30
            };
            RefreshCategoryList(null);

            _lblTotal = new Label { Dock = DockStyle.Fill, Text = "Нажми «Сканировать», чтобы найти мусор.", ForeColor = Accent, Font = new Font("Segoe UI", 12f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };

            _bar = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 1, Value = 0 };

            // строка кнопок
            var bp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Bg };
            bp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            bp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            bp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));

            _btnScan = MakeButton("🔍  Сканировать", false);
            _btnScan.Click += async (s, e) => await DoScan();
            _btnClean = MakeButton("🧹  Очистить", true);
            _btnClean.Click += async (s, e) => await DoClean();
            _btnClean.Enabled = false;
            _btnFolder = MakeButton("📁  Папка…", false);
            _btnFolder.Click += (s, e) => AddCustomFolder();

            bp.Controls.Add(_btnScan, 0, 0);
            bp.Controls.Add(_btnClean, 1, 0);
            bp.Controls.Add(_btnFolder, 2, 0);

            _lblStatus = new Label { Dock = DockStyle.Fill, Text = "Без рекламы · без телеметрии · без бандлов", ForeColor = Mut, Font = new Font("Segoe UI", 9f), TextAlign = ContentAlignment.MiddleLeft };

            tlp.Controls.Add(_clb, 0, 0);
            tlp.Controls.Add(_lblTotal, 0, 1);
            tlp.Controls.Add(_bar, 0, 2);
            tlp.Controls.Add(bp, 0, 3);
            tlp.Controls.Add(_lblStatus, 0, 4);

            tab.Controls.Add(tlp);
        }

        void BuildStartupTab(TabPage tab)
        {
            var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Bg, Padding = new Padding(14) };
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _lvStartup = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, BackColor = Card, ForeColor = Txt, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f) };
            _lvStartup.Columns.Add("Программа", 210);
            _lvStartup.Columns.Add("Команда", 280);
            _lvStartup.Columns.Add("Где", 80);
            tlp.Controls.Add(_lvStartup, 0, 0);
            tlp.SetColumnSpan(_lvStartup, 2);

            _btnRefresh = MakeButton("↻  Обновить", false);
            _btnRefresh.Click += (s, e) => LoadStartup();
            _btnDisable = MakeButton("⛔  Отключить выбранное", true);
            _btnDisable.Click += (s, e) => DisableSelectedStartup();
            tlp.Controls.Add(_btnRefresh, 0, 1);
            tlp.Controls.Add(_btnDisable, 1, 1);

            tab.Controls.Add(tlp);
            LoadStartup();
        }

        Button MakeButton(string text, bool primary)
        {
            var b = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                FlatStyle = FlatStyle.Flat,
                ForeColor = primary ? Color.FromArgb(4, 20, 10) : Txt,
                BackColor = primary ? Accent : Card,
                Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(38, 44, 68);
            b.Resize += (s, e) => Round(b, 11);
            return b;
        }

        static void Round(Control c, int radius)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(c.Width - d - 1, 0, d, d, 270, 90);
            path.AddArc(c.Width - d - 1, c.Height - d - 1, d, d, 0, 90);
            path.AddArc(0, c.Height - d - 1, d, d, 90, 90);
            path.CloseFigure();
            c.Region = new Region(path);
        }

        void RefreshCategoryList(bool[] checkedState)
        {
            _clb.BeginUpdate();
            _clb.Items.Clear();
            for (int i = 0; i < _cats.Count; i++)
            {
                _clb.Items.Add(_cats[i]);
                bool chk = checkedState != null && i < checkedState.Length ? checkedState[i] : !_cats[i].NeedsAdmin;
                _clb.SetItemChecked(i, chk);
            }
            _clb.EndUpdate();
        }

        bool[] CheckedSnapshot()
        {
            var arr = new bool[_cats.Count];
            for (int i = 0; i < _cats.Count; i++) arr[i] = _clb.GetItemChecked(i);
            return arr;
        }

        long SumChecked(bool[] checks)
        {
            long total = 0;
            for (int i = 0; i < _cats.Count; i++)
                if (i < checks.Length && checks[i] && _cats[i].ScannedBytes > 0) total += _cats[i].ScannedBytes;
            return total;
        }

        void ReportUI(int done, string status)
        {
            if (!IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (_bar.Maximum > 0) _bar.Value = Math.Max(0, Math.Min(done, _bar.Maximum));
                    _lblStatus.Text = status;
                }));
            }
            catch { }
        }

        void Busy(bool on)
        {
            _btnScan.Enabled = !on;
            _btnFolder.Enabled = !on;
            _btnClean.Enabled = !on && _cats.Any(c => c.ScannedBytes > 0);
            Cursor = on ? Cursors.WaitCursor : Cursors.Default;
        }

        async Task DoScan()
        {
            var checks = CheckedSnapshot();
            Busy(true);
            _bar.Value = 0; _bar.Maximum = Math.Max(1, _cats.Count);
            _lblTotal.Text = "Сканирую…";
            await Task.Run(() =>
            {
                for (int i = 0; i < _cats.Count; i++)
                {
                    var c = _cats[i];
                    ReportUI(i, "Сканирую: " + c.Name);
                    try { c.ScannedBytes = c.IsRecycleBin ? RecycleBinSize() : c.DirsToClear().Select(d => Util.DirSize(d)).Sum(); }
                    catch { c.ScannedBytes = 0; }
                }
            });
            RefreshCategoryList(checks);
            _bar.Value = _bar.Maximum;
            long total = SumChecked(checks);
            _lblTotal.Text = "Найдено мусора: " + Util.Human(total);
            _lblStatus.Text = "Отмечено к очистке. Жми «Очистить».";
            Busy(false);
        }

        async Task DoClean()
        {
            var checks = CheckedSnapshot();
            if (!checks.Any(x => x)) { MessageBox.Show("Отметь галочками, что чистить.", "SwiftClean"); return; }
            Busy(true);
            _bar.Value = 0; _bar.Maximum = Math.Max(1, _cats.Count * 2);
            long freed = 0, residual = 0;
            await Task.Run(() =>
            {
                // 1) чистим
                for (int i = 0; i < _cats.Count; i++)
                {
                    ReportUI(i, checks[i] ? "Чищу: " + _cats[i].Name : "Пропускаю: " + _cats[i].Name);
                    if (!checks[i]) continue;
                    var c = _cats[i];
                    try
                    {
                        if (c.IsRecycleBin) { long before = RecycleBinSize(); EmptyRecycleBin(); freed += before; }
                        else freed += c.DirsToClear().Select(d => Util.ClearDir(d)).Sum();
                    }
                    catch { }
                }
                // 2) авто-проверка: что реально осталось (занятые/заблокированные файлы не удалились)
                for (int i = 0; i < _cats.Count; i++)
                {
                    if (!checks[i]) continue;
                    var c = _cats[i];
                    ReportUI(_cats.Count + i, "Проверяю: " + c.Name);
                    try { c.ScannedBytes = c.IsRecycleBin ? RecycleBinSize() : c.DirsToClear().Select(d => Util.DirSize(d)).Sum(); }
                    catch { c.ScannedBytes = 0; }
                }
                residual = SumChecked(checks);
            });
            _bar.Value = _bar.Maximum;
            RefreshCategoryList(checks);
            Busy(false);
            _lblTotal.Text = "✅ Освобождено: " + Util.Human(freed);
            if (residual > 512L * 1024)
            {
                _lblStatus.Text = "Осталось " + Util.Human(residual) + " — эти файлы заняты запущенными программами. Закрой браузер и нажми «Очистить» ещё раз.";
                _btnClean.Enabled = true;
            }
            else
            {
                _lblStatus.Text = "🎉 Чисто! Освобождено " + Util.Human(freed) + ".";
                _btnClean.Enabled = false;
            }
        }

        void AddCustomFolder()
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "Выбери папку — её содержимое (файлы и подпапки) будет удалено при очистке.";
                if (d.ShowDialog(this) == DialogResult.OK && Directory.Exists(d.SelectedPath))
                {
                    var path = d.SelectedPath;
                    var checks = CheckedSnapshot();
                    _cats.Add(new CleanCategory { Name = "Папка: " + path, DirsToClear = () => new List<string> { path } });
                    RefreshCategoryList(checks); // новая папка добавится отмеченной
                    _lblStatus.Text = "Добавлена папка: " + path + ". Нажми «Сканировать».";
                }
            }
        }

        static long RecycleBinSize()
        {
            var info = new Native.SHQUERYRBINFO();
            info.cbSize = Marshal.SizeOf(typeof(Native.SHQUERYRBINFO));
            try { if (Native.SHQueryRecycleBin(null, ref info) == 0) return info.i64Size; } catch { }
            return 0;
        }

        static void EmptyRecycleBin()
        {
            try { Native.SHEmptyRecycleBin(IntPtr.Zero, null, Native.SHERB_NOCONFIRMATION | Native.SHERB_NOPROGRESSUI | Native.SHERB_NOSOUND); } catch { }
        }

        void LoadStartup()
        {
            _lvStartup.Items.Clear();
            AddStartupFromKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU");
            AddStartupFromKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM");
            AddStartupFromKey(Registry.LocalMachine, @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM32");
        }

        void AddStartupFromKey(RegistryKey root, string sub, string tag)
        {
            try
            {
                using (var k = root.OpenSubKey(sub, false))
                {
                    if (k == null) return;
                    foreach (var name in k.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(name)) continue;
                        var it = new ListViewItem(new[] { name, Convert.ToString(k.GetValue(name)), tag });
                        it.Tag = new string[] { tag, sub, name };
                        _lvStartup.Items.Add(it);
                    }
                }
            }
            catch { }
        }

        void DisableSelectedStartup()
        {
            if (_lvStartup.SelectedItems.Count == 0) { MessageBox.Show("Выбери программу в списке.", "SwiftClean"); return; }
            var it = _lvStartup.SelectedItems[0];
            var meta = (string[])it.Tag;
            string tag = meta[0], sub = meta[1], name = meta[2];
            var root = tag == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
            try
            {
                using (var k = root.OpenSubKey(sub, true))
                {
                    if (k == null) throw new Exception("нет доступа к ключу");
                    var val = k.GetValue(name);
                    using (var bk = Registry.CurrentUser.CreateSubKey(@"Software\SwiftClean\StartupBackup"))
                        if (bk != null) bk.SetValue(tag + "|" + name, val ?? "");
                    k.DeleteValue(name, false);
                }
                LoadStartup();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось (для HKLM нужны права администратора).\n" + ex.Message, "SwiftClean");
            }
        }
    }
}
