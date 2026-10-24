using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

class Settings
{
    public int Style = 0, Size = 10, Gap = 4, Thick = 2, Outline = 1, Opacity = 100;
    public bool OutlineOn = true, CenterDot = false, NoTop = false;
    public int Color = System.Drawing.Color.FromArgb(0, 255, 0).ToArgb();
    public int OutlineColor = System.Drawing.Color.FromArgb(0, 0, 0).ToArgb();

    // App rules (not part of crosshair codes)
    public bool WhitelistOnly = false, OnlyFullscreen = false, HideDesktop = false;
    public List<string> White = new List<string>(), Black = new List<string>();

    public static string Dir { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
    static string FilePath { get { return Path.Combine(Dir, "CrosshairLite.ini"); } }

    public void CopyVisualFrom(Settings o)
    {
        Style = o.Style; Size = o.Size; Gap = o.Gap; Thick = o.Thick; Outline = o.Outline; Opacity = o.Opacity;
        OutlineOn = o.OutlineOn; CenterDot = o.CenterDot; NoTop = o.NoTop; Color = o.Color; OutlineColor = o.OutlineColor;
    }

    public void Save()
    {
        try
        {
            File.WriteAllLines(FilePath, new[] {
                "Style=" + Style, "Size=" + Size, "Gap=" + Gap, "Thick=" + Thick, "Outline=" + Outline,
                "Opacity=" + Opacity, "OutlineOn=" + (OutlineOn ? 1 : 0), "CenterDot=" + (CenterDot ? 1 : 0),
                "NoTop=" + (NoTop ? 1 : 0), "Color=" + Color, "OutlineColor=" + OutlineColor,
                "WhitelistOnly=" + (WhitelistOnly ? 1 : 0), "OnlyFullscreen=" + (OnlyFullscreen ? 1 : 0),
                "HideDesktop=" + (HideDesktop ? 1 : 0),
                "White=" + string.Join(";", White.ToArray()), "Black=" + string.Join(";", Black.ToArray()) });
        }
        catch { }
    }

    public static Settings Load()
    {
        var s = new Settings();
        try
        {
            foreach (var line in File.ReadAllLines(FilePath))
            {
                int i = line.IndexOf('=');
                if (i < 0) continue;
                string k = line.Substring(0, i), val = line.Substring(i + 1);
                if (k == "White") { s.White = Split(val); continue; }
                if (k == "Black") { s.Black = Split(val); continue; }
                int v;
                if (!int.TryParse(val, out v)) continue;
                switch (k)
                {
                    case "Style": s.Style = v; break;
                    case "Size": s.Size = v; break;
                    case "Gap": s.Gap = v; break;
                    case "Thick": s.Thick = v; break;
                    case "Outline": s.Outline = v; break;
                    case "Opacity": s.Opacity = v; break;
                    case "OutlineOn": s.OutlineOn = v != 0; break;
                    case "CenterDot": s.CenterDot = v != 0; break;
                    case "NoTop": s.NoTop = v != 0; break;
                    case "Color": s.Color = v; break;
                    case "OutlineColor": s.OutlineColor = v; break;
                    case "WhitelistOnly": s.WhitelistOnly = v != 0; break;
                    case "OnlyFullscreen": s.OnlyFullscreen = v != 0; break;
                    case "HideDesktop": s.HideDesktop = v != 0; break;
                }
            }
        }
        catch { }
        return s;
    }

    static List<string> Split(string v)
    {
        return v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToLowerInvariant()).Distinct().ToList();
    }
}

// Shareable crosshair codes: "CLX1-" + base64url of a 16 byte payload with a CRC8 check byte.
static class Code
{
    const string Prefix = "CLX1-";

    static byte Crc(byte[] b, int n)
    {
        byte c = 0;
        for (int i = 0; i < n; i++)
        {
            c ^= b[i];
            for (int k = 0; k < 8; k++) c = (byte)((c & 0x80) != 0 ? (c << 1) ^ 0x07 : c << 1);
        }
        return c;
    }

    public static string Encode(Settings s)
    {
        var b = new byte[16];
        b[0] = 1; b[1] = (byte)s.Style; b[2] = (byte)s.Size; b[3] = (byte)s.Gap; b[4] = (byte)s.Thick;
        b[5] = (byte)s.Outline; b[6] = (byte)s.Opacity;
        b[7] = (byte)((s.OutlineOn ? 1 : 0) | (s.CenterDot ? 2 : 0) | (s.NoTop ? 4 : 0));
        var c = System.Drawing.Color.FromArgb(s.Color); var o = System.Drawing.Color.FromArgb(s.OutlineColor);
        b[8] = c.R; b[9] = c.G; b[10] = c.B; b[11] = o.R; b[12] = o.G; b[13] = o.B;
        b[15] = Crc(b, 15);
        return Prefix + Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // Validates the code, reconciles out-of-range values by clamping, and reports every adjustment.
    public static bool Decode(string code, Settings target, out string report)
    {
        report = "";
        code = new string((code ?? "").Where(ch => !char.IsWhiteSpace(ch)).ToArray());
        if (!code.StartsWith("CLX", StringComparison.OrdinalIgnoreCase)) { report = "Not a Crosshair Lite code (must start with CLX)."; return false; }
        if (!code.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) { report = "Unsupported code version. Update Crosshair Lite."; return false; }
        string body = code.Substring(Prefix.Length).Replace('-', '+').Replace('_', '/');
        while (body.Length % 4 != 0) body += "=";
        byte[] b;
        try { b = Convert.FromBase64String(body); } catch { report = "Code is corrupted (bad characters)."; return false; }
        if (b.Length != 16) { report = "Code is the wrong length - probably truncated."; return false; }
        if (Crc(b, 15) != b[15]) { report = "Checksum mismatch - the code has a typo or was altered."; return false; }
        if (b[0] != 1) { report = "Unsupported code version."; return false; }

        var notes = new List<string>();
        var n = new Settings();
        n.Style = Clamp("Style", b[1], 0, Overlay.Styles.Length - 1, notes);
        n.Size = Clamp("Size", b[2], 1, 60, notes);
        n.Gap = Clamp("Gap", b[3], 0, 40, notes);
        n.Thick = Clamp("Thickness", b[4], 1, 12, notes);
        n.Outline = Clamp("Outline width", b[5], 1, 6, notes);
        n.Opacity = Clamp("Opacity", b[6], 10, 100, notes);
        n.OutlineOn = (b[7] & 1) != 0; n.CenterDot = (b[7] & 2) != 0; n.NoTop = (b[7] & 4) != 0;
        n.Color = System.Drawing.Color.FromArgb(b[8], b[9], b[10]).ToArgb();
        n.OutlineColor = System.Drawing.Color.FromArgb(b[11], b[12], b[13]).ToArgb();
        target.CopyVisualFrom(n);
        report = notes.Count == 0 ? "Imported." : "Imported with adjustments: " + string.Join("; ", notes.ToArray());
        return true;
    }

    static int Clamp(string name, int v, int min, int max, List<string> notes)
    {
        int r = Math.Max(min, Math.Min(max, v));
        if (r != v) notes.Add(name + " " + v + " -> " + r);
        return r;
    }
}

static class Presets
{
    static string FilePath { get { return Path.Combine(Settings.Dir, "CrosshairLite.presets"); } }

    public static SortedDictionary<string, string> Load()
    {
        var d = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var line in File.ReadAllLines(FilePath))
            {
                int i = line.LastIndexOf('|');
                if (i > 0) d[line.Substring(0, i)] = line.Substring(i + 1);
            }
        }
        catch { }
        return d;
    }

    public static void Save(SortedDictionary<string, string> d)
    {
        try { File.WriteAllLines(FilePath, d.Select(kv => kv.Key + "|" + kv.Value).ToArray()); } catch { }
    }
}

class Overlay : Form
{
    const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80,
              WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOPMOST = 0x8, WM_HOTKEY = 0x312;

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObj, int idChild, uint thread, uint time);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    public static readonly string[] Styles = { "Cross", "Dot", "Circle", "Square", "Diamond", "X", "Chevron", "Triangle" };
    static readonly Color Key = Color.FromArgb(1, 2, 3);

    public Settings S = Settings.Load();
    public event Action Changed;
    NotifyIcon tray;
    public static Icon AppIcon;
    SettingsForm win;
    bool ready, userOn = true;
    IntPtr hook;
    WinEventProc hookProc;
    Timer fsTimer = new Timer { Interval = 1000 };
    readonly int myPid = Process.GetCurrentProcess().Id;

    public Overlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Key;
        TransparencyKey = Key;
        Apply();

        var m = new ContextMenuStrip();
        m.Items.Add("Settings...", null, (s, e) => ShowSettings(0));
        m.Items.Add("Toggle (Ctrl+Alt+X)", null, (s, e) => Toggle());
        m.Items.Add("Exit", null, (s, e) => { tray.Visible = false; Application.Exit(); });
        string ico = Environment.GetEnvironmentVariable("CROSSHAIR_ICON");
        Icon = AppIcon = (ico != null && File.Exists(ico)) ? new Icon(ico) : Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        tray = new NotifyIcon { Icon = AppIcon, Text = "Crosshair Lite", ContextMenuStrip = m, Visible = true };
        tray.DoubleClick += (s, e) => ShowSettings(0);
        fsTimer.Tick += (s, e) => Evaluate();
    }

    public void Start()
    {
        ready = true;
        hookProc = (h, ev, hw, o, c, t, tm) => Evaluate();
        hook = SetWinEventHook(3, 3, IntPtr.Zero, hookProc, 0, 0, 0); // EVENT_SYSTEM_FOREGROUND
        Show();
        Evaluate();
    }

    void Toggle() { userOn = !userOn; Evaluate(); }

    public void ShowSettings(int tab)
    {
        if (win != null && !win.IsDisposed) { win.Activate(); return; }
        win = new SettingsForm(this, tab);
        win.Show();
    }

    public void Apply()
    {
        int d = (S.Size + S.Gap + S.Thick + (S.OutlineOn ? S.Outline : 0)) * 2 + 8;
        var b = Screen.PrimaryScreen.Bounds;
        Bounds = new Rectangle(b.X + (b.Width - d) / 2, b.Y + (b.Height - d) / 2, d, d);
        Opacity = Math.Max(0.1, S.Opacity / 100.0);
        Invalidate();
        fsTimer.Enabled = S.OnlyFullscreen; // the only polling, and only when that option is on
        Evaluate();
        if (Changed != null) Changed();
    }

    void Evaluate()
    {
        if (!ready) return;
        bool show = userOn && Rule();
        if (show != Visible) Visible = show;
    }

    bool Rule()
    {
        IntPtr fg = GetForegroundWindow();
        int pid = 0;
        if (fg != IntPtr.Zero) GetWindowThreadProcessId(fg, out pid);
        if (pid == myPid) return true; // let the user preview while configuring

        bool desktop = fg == IntPtr.Zero || IsShellWindow(fg);
        if (desktop && S.HideDesktop) return false;

        string name = ProcName(pid);
        if (S.Black.Contains(name)) return false;
        bool white = S.White.Contains(name);
        if (S.WhitelistOnly && !S.OnlyFullscreen) return white;
        if (S.OnlyFullscreen) return white || (!desktop && IsFullscreen(fg));
        if (S.WhitelistOnly) return white;
        return true;
    }

    static string ProcName(int pid)
    {
        try { using (var p = Process.GetProcessById(pid)) return p.ProcessName.ToLowerInvariant(); }
        catch { return ""; }
    }

    static bool IsShellWindow(IntPtr h)
    {
        var sb = new StringBuilder(64);
        GetClassName(h, sb, 64);
        string c = sb.ToString();
        return c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd";
    }

    static bool IsFullscreen(IntPtr h)
    {
        RECT r;
        if (!GetWindowRect(h, out r)) return false;
        var b = Screen.FromHandle(h).Bounds;
        return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            return cp;
        }
    }
    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterHotKey(Handle, 1, 0x1 | 0x2, (uint)Keys.X);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY) Toggle(); else base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        int c = ClientSize.Width / 2;
        if (S.OutlineOn) Shape(g, Color.FromArgb(S.OutlineColor), c, S.Outline);
        Shape(g, Color.FromArgb(S.Color), c, 0);
    }

    // o > 0 is the outline pass: same geometry with fatter strokes.
    void Shape(Graphics g, Color col, int c, int o)
    {
        int t = S.Thick, sz = S.Size, gap = S.Gap, h = t / 2;
        using (var br = new SolidBrush(col))
        using (var pen = new Pen(col, t + 2 * o) { LineJoin = LineJoin.Miter })
        {
            if (o > 0) { pen.StartCap = LineCap.Square; pen.EndCap = LineCap.Square; }
            int r = sz / 2 + gap;
            switch (S.Style)
            {
                case 0:
                    if (!S.NoTop) g.FillRectangle(br, c - h - o, c - gap - sz - h - o, t + 2 * o, sz + 2 * o);
                    g.FillRectangle(br, c - h - o, c + gap + h - o, t + 2 * o, sz + 2 * o);
                    g.FillRectangle(br, c - gap - sz - h - o, c - h - o, sz + 2 * o, t + 2 * o);
                    g.FillRectangle(br, c + gap + h - o, c - h - o, sz + 2 * o, t + 2 * o);
                    break;
                case 1:
                    int dr = Math.Max(t, 2) + o;
                    g.FillEllipse(br, c - dr, c - dr, dr * 2, dr * 2);
                    break;
                case 2: g.DrawEllipse(pen, c - r, c - r, r * 2, r * 2); break;
                case 3: g.DrawRectangle(pen, c - r, c - r, r * 2, r * 2); break;
                case 4: g.DrawPolygon(pen, new[] { new Point(c, c - r), new Point(c + r, c), new Point(c, c + r), new Point(c - r, c) }); break;
                case 5:
                    g.DrawLine(pen, c - gap - sz, c - gap - sz, c - gap, c - gap);
                    g.DrawLine(pen, c + gap + sz, c - gap - sz, c + gap, c - gap);
                    g.DrawLine(pen, c - gap - sz, c + gap + sz, c - gap, c + gap);
                    g.DrawLine(pen, c + gap + sz, c + gap + sz, c + gap, c + gap);
                    break;
                case 6: g.DrawLines(pen, new[] { new Point(c - r, c + r / 2 + sz / 2), new Point(c, c - r + sz / 2), new Point(c + r, c + r / 2 + sz / 2) }); break;
                case 7: g.DrawPolygon(pen, new[] { new Point(c, c - r), new Point(c + r, c + r), new Point(c - r, c + r) }); break;
            }
            if (S.CenterDot && S.Style != 1)
            {
                int dr = Math.Max(h, 1) + o;
                g.FillRectangle(br, c - dr, c - dr, dr * 2, dr * 2);
            }
        }
    }

    protected override void Dispose(bool d)
    {
        if (hook != IntPtr.Zero) UnhookWinEvent(hook);
        if (tray != null) tray.Dispose();
        base.Dispose(d);
    }

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        var o = new Overlay();
        o.Start();
        if (args.Length == 0 || args[0] != "-hidden") o.ShowSettings(0);
        Application.Run();
    }
}

class SettingsForm : Form
{
    Overlay ov;
    Settings S { get { return ov.S; } }
    TabControl tabs;
    Control cur;
    int y;

    public SettingsForm(Overlay o, int tab)
    {
        ov = o;
        Text = "Crosshair Lite";
        Icon = Overlay.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 530);
        Font = new Font("Segoe UI", 9);

        tabs = new TabControl { Dock = DockStyle.Top, Height = 486 };
        var t1 = new TabPage("Crosshair"); var t2 = new TabPage("Apps"); var t3 = new TabPage("Presets");
        tabs.TabPages.AddRange(new[] { t1, t2, t3 });
        Controls.Add(tabs);
        var close = new Button { Text = "Close", Left = 250, Top = 494, Width = 95 };
        close.Click += (s, e) => Close();
        Controls.Add(close);

        BuildCrosshair(t1); BuildApps(t2); BuildPresets(t3);
        tabs.SelectedIndex = Math.Max(0, Math.Min(2, tab));
        FormClosed += (s, e) => { ov.S.Save(); Dispose(); };
    }

    void Begin(Control page) { cur = page; y = 12; }

    void BuildCrosshair(TabPage p)
    {
        Begin(p);
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 110, Top = y, Width = 230 };
        combo.Items.AddRange(Overlay.Styles);
        combo.SelectedIndex = Math.Min(S.Style, Overlay.Styles.Length - 1);
        combo.SelectedIndexChanged += (s, e) => { S.Style = combo.SelectedIndex; ov.Apply(); };
        Caption("Style"); cur.Controls.Add(combo); y += 34;

        Slider("Size", 1, 60, S.Size, v => S.Size = v);
        Slider("Gap", 0, 40, S.Gap, v => S.Gap = v);
        Slider("Thickness", 1, 12, S.Thick, v => S.Thick = v);
        Slider("Opacity %", 10, 100, S.Opacity, v => S.Opacity = v);
        ColorButton("Color", S.Color, c => S.Color = c);
        Check("Outline", S.OutlineOn, v => S.OutlineOn = v);
        Slider("Outline width", 1, 6, S.Outline, v => S.Outline = v);
        ColorButton("Outline color", S.OutlineColor, c => S.OutlineColor = c);
        Check("Center dot", S.CenterDot, v => S.CenterDot = v);
        Check("T-style (cross only: no top arm)", S.NoTop, v => S.NoTop = v);

        var reset = new Button { Text = "Reset crosshair", Left = 110, Top = y + 4, Width = 130 };
        reset.Click += (s, e) => { var d = new Settings(); S.CopyVisualFrom(d); ov.Apply(); Reload(0); };
        cur.Controls.Add(reset);
    }

    void BuildApps(TabPage p)
    {
        Begin(p);
        Caption("App");
        var proc = new ComboBox { Left = 60, Top = y, Width = 280 };
        proc.DropDown += (s, e) =>
        {
            proc.Items.Clear();
            var names = new SortedSet<string>();
            foreach (var pr in Process.GetProcesses())
            {
                try { if (pr.MainWindowHandle != IntPtr.Zero) names.Add(pr.ProcessName.ToLowerInvariant()); } catch { }
                pr.Dispose();
            }
            proc.Items.AddRange(names.Cast<object>().ToArray());
        };
        cur.Controls.Add(proc); y += 32;

        var wl = new ListBox { Left = 12, Top = 0, Width = 160, Height = 130 };
        var bl = new ListBox { Left = 180, Top = 0, Width = 160, Height = 130 };
        foreach (var n in S.White) wl.Items.Add(n);
        foreach (var n in S.Black) bl.Items.Add(n);

        Action<ListBox, List<string>> add = (box, list) =>
        {
            string n = (proc.Text ?? "").Trim().ToLowerInvariant();
            if (n.EndsWith(".exe")) n = n.Substring(0, n.Length - 4);
            if (n.Length == 0 || list.Contains(n)) return;
            list.Add(n); box.Items.Add(n); ov.Apply();
        };
        Action<ListBox, List<string>> remove = (box, list) =>
        {
            if (box.SelectedItem == null) return;
            string n = (string)box.SelectedItem;
            list.Remove(n); box.Items.Remove(n); ov.Apply();
        };

        var aw = new Button { Text = "Add to whitelist", Left = 12, Top = y, Width = 160 };
        var ab = new Button { Text = "Add to blacklist", Left = 180, Top = y, Width = 160 };
        aw.Click += (s, e) => add(wl, S.White);
        ab.Click += (s, e) => add(bl, S.Black);
        cur.Controls.Add(aw); cur.Controls.Add(ab); y += 34;

        cur.Controls.Add(new Label { Text = "Whitelist", Left = 12, Top = y, Width = 160 });
        cur.Controls.Add(new Label { Text = "Blacklist (always hidden)", Left = 180, Top = y, Width = 160 });
        y += 20;
        wl.Top = bl.Top = y; cur.Controls.Add(wl); cur.Controls.Add(bl); y += 134;
        var rw = new Button { Text = "Remove selected", Left = 12, Top = y, Width = 160 };
        var rb = new Button { Text = "Remove selected", Left = 180, Top = y, Width = 160 };
        rw.Click += (s, e) => remove(wl, S.White);
        rb.Click += (s, e) => remove(bl, S.Black);
        cur.Controls.Add(rw); cur.Controls.Add(rb); y += 40;

        Check("Only show on whitelisted apps", S.WhitelistOnly, v => S.WhitelistOnly = v);
        Check("Only show on fullscreen apps (and whitelisted)", S.OnlyFullscreen, v => S.OnlyFullscreen = v);
        Check("Hide on desktop / taskbar", S.HideDesktop, v => S.HideDesktop = v);
        cur.Controls.Add(new Label { Left = 12, Top = y, Width = 330, Height = 34, ForeColor = SystemColors.GrayText,
            Text = "Type or pick a process name (e.g. csgo). Blacklist always wins. Fullscreen check is the only polling (1/sec)." });
    }

    void BuildPresets(TabPage p)
    {
        Begin(p);
        cur.Controls.Add(new Label { Text = "Current crosshair code", Left = 12, Top = y, Width = 330 }); y += 20;
        var code = new TextBox { Left = 12, Top = y, Width = 250, ReadOnly = true, Text = Code.Encode(S) };
        var copy = new Button { Text = "Copy", Left = 270, Top = y - 1, Width = 70 };
        copy.Click += (s, e) => { try { Clipboard.SetText(code.Text); } catch { } };
        cur.Controls.Add(code); cur.Controls.Add(copy); y += 36;
        Action update = () => { if (!code.IsDisposed) code.Text = Code.Encode(S); };
        ov.Changed += update;
        FormClosed += (s, e) => ov.Changed -= update;

        cur.Controls.Add(new Label { Text = "Presets", Left = 12, Top = y, Width = 330 }); y += 20;
        var presets = Presets.Load();
        var list = new ListBox { Left = 12, Top = y, Width = 328, Height = 110 };
        Action refill = () => { list.Items.Clear(); foreach (var k in presets.Keys) list.Items.Add(k); };
        refill(); cur.Controls.Add(list); y += 116;

        var name = new TextBox { Left = 12, Top = y, Width = 160 };
        var save = new Button { Text = "Save current", Left = 180, Top = y - 1, Width = 160 };
        cur.Controls.Add(name); cur.Controls.Add(save); y += 32;
        var load = new Button { Text = "Load selected", Left = 12, Top = y, Width = 100 };
        var share = new Button { Text = "Copy its code", Left = 120, Top = y, Width = 110 };
        var del = new Button { Text = "Delete", Left = 240, Top = y, Width = 100 };
        cur.Controls.Add(load); cur.Controls.Add(share); cur.Controls.Add(del); y += 42;

        cur.Controls.Add(new Label { Text = "Import a code (paste)", Left = 12, Top = y, Width = 330 }); y += 20;
        var imp = new TextBox { Left = 12, Top = y, Width = 250 };
        var go = new Button { Text = "Import", Left = 270, Top = y - 1, Width = 70 };
        cur.Controls.Add(imp); cur.Controls.Add(go); y += 34;
        var msg = new Label { Left = 12, Top = y, Width = 328, Height = 60 };
        cur.Controls.Add(msg);

        save.Click += (s, e) =>
        {
            string n = (name.Text ?? "").Replace("|", "").Replace("\r", "").Replace("\n", "").Trim();
            if (n.Length == 0) { msg.Text = "Enter a preset name first."; return; }
            presets[n] = Code.Encode(S); Presets.Save(presets); refill(); msg.Text = "Saved '" + n + "'.";
        };
        del.Click += (s, e) =>
        {
            if (list.SelectedItem == null) return;
            presets.Remove((string)list.SelectedItem); Presets.Save(presets); refill(); msg.Text = "Deleted.";
        };
        share.Click += (s, e) =>
        {
            if (list.SelectedItem == null) return;
            try { Clipboard.SetText(presets[(string)list.SelectedItem]); msg.Text = "Code copied."; } catch { }
        };
        Action<string> apply = c =>
        {
            string report;
            bool ok = Code.Decode(c, S, out report);
            msg.Text = report;
            if (ok) { ov.Apply(); Reload(2); }
        };
        load.Click += (s, e) => { if (list.SelectedItem != null) apply(presets[(string)list.SelectedItem]); };
        go.Click += (s, e) => apply(imp.Text);
    }

    // Rebuild the window so every control reflects the new settings.
    void Reload(int tab)
    {
        var o = ov;
        BeginInvoke(new Action(() => { Close(); o.ShowSettings(tab); }));
    }

    void Caption(string t) { cur.Controls.Add(new Label { Text = t, Left = 12, Top = y + 3, Width = 95 }); }

    void Slider(string name, int min, int max, int val, Action<int> set)
    {
        Caption(name);
        var num = new Label { Left = 310, Top = y + 3, Width = 35, Text = val.ToString() };
        var tb = new TrackBar { Left = 105, Top = y - 3, Width = 205, Minimum = min, Maximum = max, TickStyle = TickStyle.None, Value = Math.Max(min, Math.Min(max, val)) };
        tb.ValueChanged += (s, e) => { set(tb.Value); num.Text = tb.Value.ToString(); ov.Apply(); };
        cur.Controls.Add(tb); cur.Controls.Add(num); y += 38;
    }

    void Check(string text, bool val, Action<bool> set)
    {
        var cb = new CheckBox { Text = text, Left = 12, Top = y, Width = 330, Checked = val };
        cb.CheckedChanged += (s, e) => { set(cb.Checked); ov.Apply(); };
        cur.Controls.Add(cb); y += 28;
    }

    void ColorButton(string name, int argb, Action<int> set)
    {
        Caption(name);
        var b = new Button { Left = 110, Top = y, Width = 230, Height = 26, BackColor = Color.FromArgb(argb), FlatStyle = FlatStyle.Flat };
        b.Click += (s, e) =>
        {
            using (var d = new ColorDialog { Color = b.BackColor, FullOpen = true })
                if (d.ShowDialog(this) == DialogResult.OK) { b.BackColor = d.Color; set(d.Color.ToArgb()); ov.Apply(); }
        };
        cur.Controls.Add(b); y += 34;
    }
}
