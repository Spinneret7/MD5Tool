using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32;

static class Program
{
    public static readonly byte[] Magic = Encoding.ASCII.GetBytes("MD5VRFY1");

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string manifest = ReadEmbeddedManifest();
        if (manifest != null) Application.Run(new VerifyForm(manifest));
        else Application.Run(new MakerForm());
    }

    static string ReadEmbeddedManifest()
    {
        try
        {
            using (FileStream fs = new FileStream(Application.ExecutablePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < 16) return null;
                fs.Seek(-16, SeekOrigin.End);
                byte[] tail = new byte[16];
                fs.Read(tail, 0, 16);
                for (int i = 0; i < 8; i++) if (tail[8 + i] != Magic[i]) return null;
                long len = BitConverter.ToInt64(tail, 0);
                if (len <= 0 || len > fs.Length - 16) return null;
                fs.Seek(-(16 + len), SeekOrigin.End);
                byte[] data = new byte[len];
                int read = 0;
                while (read < len) { int n = fs.Read(data, read, (int)len - read); if (n <= 0) break; read += n; }
                return Encoding.UTF8.GetString(data);
            }
        }
        catch { return null; }
    }
}

static class Theme
{
    public static readonly Color Back = Color.FromArgb(30, 30, 30);
    public static readonly Color Panel = Color.FromArgb(24, 24, 24);
    public static readonly Color Fore = Color.FromArgb(225, 225, 225);
    public static readonly Color Dim = Color.FromArgb(150, 150, 150);
    public static readonly Color Border = Color.FromArgb(75, 75, 75);
    public static readonly Color Button = Color.FromArgb(48, 48, 48);
    public static readonly Color Green = Color.FromArgb(6, 176, 37);
    public static readonly Color Red = Color.FromArgb(235, 90, 90);
    public static readonly Color Orange = Color.FromArgb(240, 170, 60);

    public static void ApplyIcon(Form f)
    {
        try { f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { }
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public static void DarkTitleBar(Form f)
    {
        try
        {
            int on = 1;
            if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0)
                DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
        }
        catch { }
    }

    public static Button MakeButton(string text, int x, int y, int w, int h)
    {
        Button b = new Button();
        b.Text = text;
        b.SetBounds(x, y, w, h);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Border;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, 65, 65);
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(85, 85, 85);
        b.BackColor = Button;
        b.ForeColor = Fore;
        b.UseVisualStyleBackColor = false;
        return b;
    }

    public static Label MakeLabel(string text, int x, int y, int w, int h)
    {
        Label l = new Label();
        l.Text = text;
        l.SetBounds(x, y, w, h);
        l.ForeColor = Fore;
        l.BackColor = Color.Transparent;
        l.AutoEllipsis = true;
        return l;
    }
}

static class Settings
{
    const string KeyPath = @"Software\Md5Tool";

    public static string LoadDrive()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(KeyPath))
            {
                if (k == null) return null;
                string v = k.GetValue("LastDrive") as string;
                return (v != null && Directory.Exists(v)) ? v : null;
            }
        }
        catch { return null; }
    }

    public static void SaveDrive(string drive)
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(KeyPath))
                k.SetValue("LastDrive", drive);
        }
        catch { }
    }
}

class DarkBar : Control
{
    long val, max = 100;
    public DarkBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }
    public void Set(long value, long maximum)
    {
        if (maximum < 1) maximum = 1;
        if (value > maximum) value = maximum;
        if (value == val && maximum == max) return;
        val = value; max = maximum; Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Color.FromArgb(40, 40, 40));
        int w = (int)((Width - 2) * (double)val / max);
        if (w > 0)
            using (SolidBrush b = new SolidBrush(Theme.Green))
                g.FillRectangle(b, 1, 1, w, Height - 2);
        using (Pen p = new Pen(Theme.Border))
            g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }
}

static class Md5Util
{
    public static string HashFile(string path, Action<long> onBytes, Func<bool> cancelled)
    {
        using (MD5 md5 = MD5.Create())
        using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20))
        {
            byte[] buf = new byte[1 << 20];
            long total = 0;
            int n;
            while ((n = fs.Read(buf, 0, buf.Length)) > 0)
            {
                if (cancelled != null && cancelled()) return null;
                md5.TransformBlock(buf, 0, n, null, 0);
                total += n;
                if (onBytes != null) onBytes(total);
            }
            md5.TransformFinalBlock(buf, 0, 0);
            return BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();
        }
    }

    public static string Size(long bytes)
    {
        double mb = bytes / 1048576.0;
        if (mb >= 1) return mb.ToString("0.00", CultureInfo.InvariantCulture) + " Mb";
        return (bytes / 1024.0).ToString("0.00", CultureInfo.InvariantCulture) + " Kb";
    }
}

class VerifyForm : Form
{
    class Entry { public string Hash; public string Name; }

    List<Entry> entries = new List<Entry>();
    string root;
    volatile bool cancel;
    bool finished;

    Label lblTitle, lblFile, lblStats;
    DarkBar barFile, barAll;
    ListBox list;
    Button btn;

    public VerifyForm(string manifest)
    {
        root = Path.GetDirectoryName(Application.ExecutablePath);
        foreach (string raw in manifest.Split('\n'))
        {
            string line = raw.Trim('\r', '\n', ' ');
            if (line.Length < 35 || line.StartsWith(";") || line.StartsWith("#")) continue;
            Entry e = new Entry();
            e.Hash = line.Substring(0, 32).ToLowerInvariant();
            e.Name = line.Substring(34);
            entries.Add(e);
        }

        Text = "Verify files";
        Theme.ApplyIcon(this);
        ClientSize = new Size(400, 292);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Back;
        ForeColor = Theme.Fore;
        Font = new Font("Segoe UI", 9f);

        lblTitle = Theme.MakeLabel("Checking files integrity, please wait... 0%", 12, 10, 376, 18);
        lblFile = Theme.MakeLabel("", 12, 34, 376, 18);
        barFile = new DarkBar(); barFile.SetBounds(12, 56, 376, 14);
        lblStats = Theme.MakeLabel("", 12, 80, 376, 18);
        barAll = new DarkBar(); barAll.SetBounds(12, 102, 376, 14);

        list = new ListBox();
        list.SetBounds(12, 126, 376, 120);
        list.BackColor = Theme.Panel;
        list.ForeColor = Theme.Fore;
        list.BorderStyle = BorderStyle.FixedSingle;
        list.DrawMode = DrawMode.OwnerDrawFixed;
        list.ItemHeight = 16;
        list.IntegralHeight = false;
        list.DrawItem += DrawListItem;

        btn = Theme.MakeButton("Cancel", 150, 256, 100, 26);
        btn.Click += delegate
        {
            if (finished) Close(); else { cancel = true; btn.Enabled = false; }
        };

        Controls.AddRange(new Control[] { lblTitle, lblFile, barFile, lblStats, barAll, list, btn });
        lblStats.Text = "Files processed: 0   Total files: " + entries.Count + "   Ok: 0   Missing: 0   Bad: 0";
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.DarkTitleBar(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Thread t = new Thread(Work);
        t.IsBackground = true;
        t.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        cancel = true;
        base.OnFormClosing(e);
    }

    void DrawListItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        string s = (string)list.Items[e.Index];
        Color c = Theme.Fore;
        if (s.EndsWith("Hash_BAD")) c = Theme.Red;
        else if (s.EndsWith("Hash_MISSING")) c = Theme.Orange;
        using (SolidBrush bg = new SolidBrush(Theme.Panel))
            e.Graphics.FillRectangle(bg, e.Bounds);
        TextRenderer.DrawText(e.Graphics, s, list.Font, e.Bounds, c, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    void Ui(MethodInvoker a)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(a); } catch { }
    }

    readonly object sync = new object();
    int next = -1, okCnt, missCnt, badCnt, doneCnt;
    long doneBytes, totalBytes;
    long[] sizes;
    string current = "";
    List<string> pending = new List<string>();
    int threadCount;

    void Work()
    {
        int count = entries.Count;
        sizes = new long[count];
        for (int i = 0; i < count; i++)
        {
            try
            {
                FileInfo fi = new FileInfo(Path.Combine(root, entries[i].Name));
                if (fi.Exists) { sizes[i] = fi.Length; totalBytes += fi.Length; }
            }
            catch { }
        }
        if (totalBytes < 1) totalBytes = 1;

        threadCount = Math.Max(1, Math.Min(Environment.ProcessorCount, count));
        Thread[] ts = new Thread[threadCount];
        for (int i = 0; i < ts.Length; i++)
        {
            ts[i] = new Thread(Worker);
            ts[i].IsBackground = true;
            ts[i].Start();
        }

        bool alive = true;
        while (alive)
        {
            Thread.Sleep(80);
            PushUi();
            alive = false;
            foreach (Thread t in ts) if (t.IsAlive) alive = true;
        }
        foreach (Thread t in ts) t.Join();
        PushUi();

        bool wasCancelled = cancel;
        int fo, fm, fb, tc = threadCount;
        lock (sync) { fo = okCnt; fm = missCnt; fb = badCnt; }
        Ui(delegate
        {
            finished = true;
            btn.Enabled = true;
            btn.Text = "Close";
            lblFile.Text = "Used " + tc + " threads.";
            if (wasCancelled) lblTitle.Text = "Verification cancelled.";
            else if (fm == 0 && fb == 0) lblTitle.Text = "All files are OK. (" + fo + " verified)";
            else lblTitle.Text = "Problems found: " + fm + " missing, " + fb + " bad.";
            if (!wasCancelled && (fm > 0 || fb > 0)) lblTitle.ForeColor = Theme.Red;
        });
    }

    void Worker()
    {
        int count = entries.Count;
        while (!cancel)
        {
            int i;
            lock (sync) { i = ++next; }
            if (i >= count) break;

            Entry en = entries[i];
            string full = Path.Combine(root, en.Name);
            long size = sizes[i];
            string status;

            if (!File.Exists(full))
            {
                status = "Hash_MISSING";
            }
            else
            {
                lock (sync) { current = en.Name + " (" + Md5Util.Size(size) + ")"; }
                long last = 0;
                string hash;
                try
                {
                    hash = Md5Util.HashFile(full, delegate(long n)
                    {
                        long d = n - last; last = n;
                        lock (sync) { doneBytes += d; }
                    }, delegate { return cancel; });
                }
                catch { hash = "error"; }

                if (hash == null) break;
                if (last < size) lock (sync) { doneBytes += size - last; }
                status = (hash == en.Hash) ? "Hash_OK" : "Hash_BAD";
            }

            lock (sync)
            {
                doneCnt++;
                if (status == "Hash_OK") okCnt++;
                else if (status == "Hash_BAD") badCnt++;
                else missCnt++;
                pending.Add(en.Name + " ... " + status);
            }
        }
    }

    void PushUi()
    {
        int d, o, m, b; long db; string cur; string[] items;
        lock (sync)
        {
            d = doneCnt; o = okCnt; m = missCnt; b = badCnt; db = doneBytes; cur = current;
            items = pending.ToArray(); pending.Clear();
        }
        int total = entries.Count; long tb = totalBytes; int tc = threadCount;
        Ui(delegate
        {
            if (items.Length > 0)
            {
                list.BeginUpdate();
                foreach (string it in items) list.Items.Add(it);
                list.TopIndex = Math.Max(0, list.Items.Count - 1);
                list.EndUpdate();
            }
            barFile.Set(db, tb);
            barAll.Set(d, total);
            lblStats.Text = "Files processed: " + d + "   Total files: " + total + "   Ok: " + o + "   Missing: " + m + "   Bad: " + b;
            int pct = (int)Math.Min(100, db * 100 / tb);
            if (!finished) lblTitle.Text = "Checking files integrity, please wait... " + pct + "%";
            if (!finished && cur.Length > 0) lblFile.Text = "Checking " + cur + "  [" + tc + " threads]";
        });
    }
}

class MakerForm : Form
{
    TextBox txtFolder;
    Button btnBrowse, btnMd5, btnExe;
    DarkBar bar;
    Label lblStatus;
    volatile bool busy;

    public MakerForm()
    {
        Text = "MD5 Tool";
        Theme.ApplyIcon(this);
        ClientSize = new Size(460, 170);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Back;
        ForeColor = Theme.Fore;
        Font = new Font("Segoe UI", 9f);

        Label l = Theme.MakeLabel("Folder to hash:", 12, 12, 300, 18);
        txtFolder = new TextBox();
        txtFolder.SetBounds(12, 34, 350, 24);
        txtFolder.BackColor = Theme.Panel;
        txtFolder.ForeColor = Theme.Fore;
        txtFolder.BorderStyle = BorderStyle.FixedSingle;
        btnBrowse = Theme.MakeButton("Browse...", 370, 32, 78, 26);
        btnBrowse.Click += delegate
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                string drive = Settings.LoadDrive();
                if (drive != null) d.SelectedPath = drive;
                if (d.ShowDialog() == DialogResult.OK)
                {
                    txtFolder.Text = d.SelectedPath;
                    string root = Path.GetPathRoot(d.SelectedPath);
                    if (!string.IsNullOrEmpty(root)) Settings.SaveDrive(root);
                }
            }
        };

        btnMd5 = Theme.MakeButton("Create .md5 file", 12, 72, 215, 30);
        btnExe = Theme.MakeButton("Create verifier .exe", 233, 72, 215, 30);
        btnMd5.Click += delegate { Go(false); };
        btnExe.Click += delegate { Go(true); };

        bar = new DarkBar(); bar.SetBounds(12, 118, 436, 14);
        lblStatus = Theme.MakeLabel("Ready.", 12, 140, 436, 20);
        lblStatus.ForeColor = Theme.Dim;

        Controls.AddRange(new Control[] { l, txtFolder, btnBrowse, btnMd5, btnExe, bar, lblStatus });
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.DarkTitleBar(this);
    }

    void Ui(MethodInvoker a)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(a); } catch { }
    }

    void SetBusy(bool b)
    {
        busy = b;
        btnMd5.Enabled = btnExe.Enabled = btnBrowse.Enabled = !b;
    }

    void Go(bool makeExe)
    {
        if (busy) return;
        string folder = txtFolder.Text.Trim();
        if (!Directory.Exists(folder))
        {
            lblStatus.Text = "Please pick a valid folder first.";
            return;
        }

        string outPath;
        if (makeExe)
        {
            outPath = Path.Combine(folder, "Verify.exe");
        }
        else
        {
            using (SaveFileDialog sd = new SaveFileDialog())
            {
                sd.InitialDirectory = folder;
                sd.Filter = "MD5 file (*.md5)|*.md5";
                sd.FileName = "checksums.md5";
                if (sd.ShowDialog() != DialogResult.OK) return;
                outPath = sd.FileName;
            }
        }

        SetBusy(true);
        bar.Set(0, 1);
        string root = folder.TrimEnd('\\');
        string outFull = Path.GetFullPath(outPath);
        string self = Application.ExecutablePath;

        Thread coordinator = new Thread(delegate()
        {
            try
            {
                List<string> files = new List<string>();
                long total = 0;
                foreach (string f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    string full = Path.GetFullPath(f);
                    if (string.Equals(full, outFull, StringComparison.OrdinalIgnoreCase)) continue;
                    files.Add(full);
                    total += new FileInfo(full).Length;
                }
                files.Sort(StringComparer.OrdinalIgnoreCase);
                if (total < 1) total = 1;

                int count = files.Count;
                string[] hashes = new string[count];
                int next = -1, doneFiles = 0;
                long doneBytes = 0;
                Exception error = null;
                object sync = new object();

                int threadCount = Math.Max(1, Math.Min(Environment.ProcessorCount, count));
                Thread[] ts = new Thread[threadCount];
                for (int t = 0; t < ts.Length; t++)
                {
                    ts[t] = new Thread(delegate()
                    {
                        while (true)
                        {
                            int i = Interlocked.Increment(ref next);
                            if (i >= count) break;
                            lock (sync) { if (error != null) break; }
                            try
                            {
                                long last = 0;
                                hashes[i] = Md5Util.HashFile(files[i], delegate(long n)
                                {
                                    long d = n - last; last = n;
                                    Interlocked.Add(ref doneBytes, d);
                                }, null);
                                Interlocked.Increment(ref doneFiles);
                            }
                            catch (Exception ex)
                            {
                                lock (sync) { if (error == null) error = ex; }
                            }
                        }
                    });
                    ts[t].IsBackground = true;
                    ts[t].Start();
                }

                int tc = threadCount;
                bool alive = true;
                while (alive)
                {
                    Thread.Sleep(100);
                    long db = Interlocked.Read(ref doneBytes);
                    int df = Thread.VolatileRead(ref doneFiles);
                    Ui(delegate
                    {
                        bar.Set(db, total);
                        lblStatus.Text = "Hashing " + df + "/" + count + " files  [" + tc + " threads]";
                    });
                    alive = false;
                    foreach (Thread t in ts) if (t.IsAlive) alive = true;
                }
                foreach (Thread t in ts) t.Join();
                if (error != null) throw error;

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < count; i++)
                {
                    string rel = files[i].Substring(root.Length).TrimStart('\\');
                    sb.Append(hashes[i]).Append(" *").Append(rel).Append("\r\n");
                }

                byte[] manifest = new UTF8Encoding(false).GetBytes(sb.ToString());
                if (makeExe)
                {
                    File.Copy(self, outFull, true);
                    using (FileStream fs = new FileStream(outFull, FileMode.Append, FileAccess.Write))
                    {
                        fs.Write(manifest, 0, manifest.Length);
                        byte[] len = BitConverter.GetBytes((long)manifest.Length);
                        fs.Write(len, 0, len.Length);
                        fs.Write(Program.Magic, 0, Program.Magic.Length);
                    }
                }
                else
                {
                    File.WriteAllBytes(outFull, manifest);
                }

                Ui(delegate
                {
                    bar.Set(1, 1);
                    lblStatus.Text = "Done. " + count + " files hashed -> " + Path.GetFileName(outFull) + " (" + tc + " threads)";
                    SetBusy(false);
                });
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                Ui(delegate { lblStatus.Text = "Error: " + msg; SetBusy(false); });
            }
        });
        coordinator.IsBackground = true;
        coordinator.Start();
    }
}