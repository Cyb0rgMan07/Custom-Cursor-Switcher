
//==================================================================
// CursorSwitcher.cs  —  Layout Redesign v3
// .NET 4.5 | C# 5 | WinForms
// 4-column vertical grid, category filter bar,
// active-pack banner, section headers, no horizontal scroll.
//==================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CursorSwitcher
{
// ──────────────────────────────────────────────────────────────────
// ENTRY POINT
// ──────────────────────────────────────────────────────────────────
static class Program
{
    [STAThread]
    static void Main()
    {
        Application.ThreadException +=
            delegate(object s, System.Threading.ThreadExceptionEventArgs e) {
                MessageBox.Show("Unhandled error:\n\n" + e.Exception.ToString(),
                    "CursorSwitcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
        AppDomain.CurrentDomain.UnhandledException +=
            delegate(object s, UnhandledExceptionEventArgs e) {
                MessageBox.Show("Fatal error:\n\n" + e.ExceptionObject.ToString(),
                    "CursorSwitcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try { Application.Run(new MainForm()); }
        catch (Exception ex) {
            MessageBox.Show("Startup error:\n\n" + ex.ToString(),
                "CursorSwitcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

// ──────────────────────────────────────────────────────────────────
// WIN32 + DWM
// ──────────────────────────────────────────────────────────────────
static class Win32
{
    [DllImport("user32.dll")]
    public static extern bool SystemParametersInfo(uint a, uint b, IntPtr c, uint d);
    [DllImport("user32.dll")]
    public static extern bool SetSystemCursor(IntPtr hcur, uint id);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr LoadCursorFromFile(string path);
    [DllImport("user32.dll")]
    public static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hIcon,
        int cx, int cy, uint step, IntPtr hbr, uint flags);
    [DllImport("user32.dll")]
    public static extern bool DestroyCursor(IntPtr h);
    [DllImport("user32.dll")]
    public static extern IntPtr CopyImage(IntPtr h, uint type, int cx, int cy, uint flags);
    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);
    [DllImport("dwmapi.dll")]
    public static extern int DwmIsCompositionEnabled(out bool enabled);
    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS { public int Left, Right, Top, Bottom; }

    public const uint SPI_SETCURSORS = 0x0057;
    public const uint SPIF_UPDATE    = 0x03;
    public const uint DI_NORMAL      = 0x0003;
    public const uint IMAGE_CURSOR   = 2;
    public const uint LR_DEFAULTSIZE = 0x0040;
    public const uint OCR_NORMAL     = 32512;
    public const int  WM_NCHITTEST   = 0x0084;
    public const int  HTCLIENT       = 1;
    public const int  HTCAPTION      = 2;
    public const int  HTLEFT         = 10;
    public const int  HTRIGHT        = 11;
    public const int  HTTOP          = 12;
    public const int  HTTOPLEFT      = 13;
    public const int  HTTOPRIGHT     = 14;
    public const int  HTBOTTOM       = 15;
    public const int  HTBOTTOMLEFT   = 16;
    public const int  HTBOTTOMRIGHT  = 17;
}

// ──────────────────────────────────────────────────────────────────
// GRAPHICS UTILITIES
// ──────────────────────────────────────────────────────────────────
static class Gfx
{
    public static float Smooth(float t)
    {
        t = t < 0f ? 0f : (t > 1f ? 1f : t);
        return t * t * (3f - 2f * t);
    }
    public static Color Lerp(Color a, Color b, float t)
    {
        float s = 1f - t;
        return Color.FromArgb(
            (int)(a.A * s + b.A * t), (int)(a.R * s + b.R * t),
            (int)(a.G * s + b.G * t), (int)(a.B * s + b.B * t));
    }
    public static double Luma(Color c)
    {
        return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
    }
    public static Color WithAlpha(Color c, int a)
    {
        return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B);
    }
    public static Color Lighten(Color c, int v)
    {
        return Color.FromArgb(Math.Min(255,c.R+v), Math.Min(255,c.G+v), Math.Min(255,c.B+v));
    }
    public static Color Darken(Color c, int v)
    {
        return Color.FromArgb(Math.Max(0,c.R-v), Math.Max(0,c.G-v), Math.Max(0,c.B-v));
    }
    public static GraphicsPath RoundRect(RectangleF r, float rad)
    {
        float d = rad * 2f;
        var p = new GraphicsPath();
        p.AddArc(r.X,         r.Y,          d, d, 180f, 90f);
        p.AddArc(r.Right - d, r.Y,          d, d, 270f, 90f);
        p.AddArc(r.Right - d, r.Bottom - d, d, d,   0f, 90f);
        p.AddArc(r.X,         r.Bottom - d, d, d,  90f, 90f);
        p.CloseFigure();
        return p;
    }
    public static void FillRR(Graphics g, Brush br, RectangleF r, float rad)
    {
        using (var path = RoundRect(r, rad)) g.FillPath(br, path);
    }
    public static void DrawRR(Graphics g, Pen pen, RectangleF r, float rad)
    {
        using (var path = RoundRect(r, rad)) g.DrawPath(pen, path);
    }
    public static void HQ(Graphics g)
    {
        g.SmoothingMode      = SmoothingMode.AntiAlias;
        g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
    }
    public static void DrawCentred(Graphics g, string text, Font font, Color col, RectangleF r)
    {
        using (var sf = new StringFormat())
        using (var br = new SolidBrush(col))
        {
            sf.Alignment     = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            sf.Trimming      = StringTrimming.EllipsisCharacter;
            g.DrawString(text, font, br, r, sf);
        }
    }
}

// ──────────────────────────────────────────────────────────────────
// DATA MODEL
// ──────────────────────────────────────────────────────────────────
class Pack
{
    public string DisplayName;
    public string Folder;
    public Color  Accent;
    public string Category;
    public Dictionary<string,string> Files;

    public string NormalCursorPath(string installDir)
    {
        string fn;
        if (!Files.TryGetValue("Arrow", out fn) || string.IsNullOrEmpty(fn)) return null;
        return Path.Combine(installDir, Folder, fn);
    }
    public string ShortName() { return DisplayName.Replace("\n", " "); }
}

// ──────────────────────────────────────────────────────────────────
// ALL 13 PACKS
// ──────────────────────────────────────────────────────────────────
static class AllPacks
{
    public static readonly Pack[] List = new Pack[]
    {
        new Pack { DisplayName="Emerald Green v3.0", Category="NIGHT DIAMOND",
            Folder="Night Diamond - Emerald Green v3.0", Accent=Color.FromArgb(0,200,83),
            Files=new Dictionary<string,string>{
                {"Arrow","[EG] Normal Select v3.0.ani"},{"Help","[EG] Help Select v3.0.ani"},
                {"AppStarting","[EG] Working In Background v3.0.ani"},{"Wait","[EG] Busy v3.0.ani"},
                {"Crosshair","[EG] Precision Select v3.0.ani"},{"IBeam","[EG] Text Select v3.0.ani"},
                {"NWPen","[EG] Handwriting v3.0.ani"},{"No","[EG] Unavailable v3.0.ani"},
                {"SizeNS","[EG] Vertical Resize v3.0.ani"},{"SizeWE","[EG] Horizontal Resize v3.0.ani"},
                {"SizeNWSE","[EG] Diagonal Resize 1 v3.0.ani"},{"SizeNESW","[EG] Diagonal Resize 2 v3.0.ani"},
                {"SizeAll","[EG] Move v3.0.ani"},{"UpArrow","[EG] Alternate Select v3.0.ani"},
                {"Hand","[EG] Link Select v3.0.ani"},{"Pin","[EG] Location Select v3.0.ani"},
                {"Person","[EG] Person Select v3.0.ani"}}},
        new Pack { DisplayName="Sapphire Blue v3.0", Category="NIGHT DIAMOND",
            Folder="Night Diamond - Sapphire Blue v3.0", Accent=Color.FromArgb(41,121,255),
            Files=new Dictionary<string,string>{
                {"Arrow","[SB] Normal Select v3.0.ani"},{"Help","[SB] Help Select v3.0.ani"},
                {"AppStarting","[SB] Working In Background v3.0.ani"},{"Wait","[SB] Busy v3.0.ani"},
                {"Crosshair","[SB] Precision Select v3.0.ani"},{"IBeam","[SB] Text Select v3.0.ani"},
                {"NWPen","[SB] Handwriting v3.0.ani"},{"No","[SB] Unavailable v3.0.ani"},
                {"SizeNS","[SB] Vertical Resize v3.0.ani"},{"SizeWE","[SB] Horizontal Resize v3.0.ani"},
                {"SizeNWSE","[SB] Diagonal Resize 1 v3.0.ani"},{"SizeNESW","[SB] Diagonal Resize 2 v3.0.ani"},
                {"SizeAll","[SB] Move v3.0.ani"},{"UpArrow","[SB] Alternate Select v3.0.ani"},
                {"Hand","[SB] Link Select v3.0.ani"},{"Pin","[SB] Location Select v3.0.ani"},
                {"Person","[SB] Person Select v3.0.ani"}}},
        new Pack { DisplayName="Ruby Red v3.0", Category="NIGHT DIAMOND",
            Folder="Night Diamond - Ruby Red v3.0", Accent=Color.FromArgb(229,57,53),
            Files=new Dictionary<string,string>{
                {"Arrow","[RR] Normal Select v3.0.ani"},{"Help","[RR] Help Select v3.0.ani"},
                {"AppStarting","[RR] Working In Background v3.0.ani"},{"Wait","[RR] Busy v3.0.ani"},
                {"Crosshair","[RR] Precision Select v3.0.ani"},{"IBeam","[RR] Text Select v3.0.ani"},
                {"NWPen","[RR] Handwriting v3.0.ani"},{"No","[RR] Unavailable v3.0.ani"},
                {"SizeNS","[RR] Vertical Resize v3.0.ani"},{"SizeWE","[RR] Horizontal Resize v3.0.ani"},
                {"SizeNWSE","[RR] Diagonal Resize 1 v3.0.ani"},{"SizeNESW","[RR] Diagonal Resize 2 v3.0.ani"},
                {"SizeAll","[RR] Move v3.0.ani"},{"UpArrow","[RR] Alternate Select v3.0.ani"},
                {"Hand","[RR] Link Select v3.0.ani"},{"Pin","[RR] Location Select v3.0.ani"},
                {"Person","[RR] Person Select v3.0.ani"}}},
        new Pack { DisplayName="Paper White", Category="KAMI v2 HD",
            Folder="Kami v2 HD - Paper White", Accent=Color.FromArgb(210,210,210),
            Files=new Dictionary<string,string>{
                {"Arrow","[PW] Normal v2 HD.cur"},{"Help","[PW] Help v2 HD.ani"},
                {"AppStarting","[PW] Working v2 HD.ani"},{"Wait","[PW] Busy v2 HD.ani"},
                {"Crosshair","[PW] Precision v2 HD.ani"},{"IBeam","[PW] Text v2 HD.ani"},
                {"NWPen","[PW] Handwrite v2 HD.ani"},{"No","[PW] Unavailable v2 HD.ani"},
                {"SizeNS","[PW] VertRes v2 HD.ani"},{"SizeWE","[PW] HoriRes v2 HD.ani"},
                {"SizeNWSE","[PW] DiaRes1 v2 HD.ani"},{"SizeNESW","[PW] DiaRes2 v2 HD.ani"},
                {"SizeAll","[PW] Move v2 HD.ani"},{"UpArrow","[PW] Alternate v2 HD.ani"},
                {"Hand","[PW] Link v2 HD.ani"},{"Pin","[PW] Location v2 HD.ani"},
                {"Person","[PW] Person v2 HD.ani"}}},
        new Pack { DisplayName="Jet Black", Category="KAMI v2 HD",
            Folder="Kami v2 HD - Jet Black", Accent=Color.FromArgb(120,120,130),
            Files=new Dictionary<string,string>{
                {"Arrow","[JB] Normal v2 HD.cur"},{"Help","[JB] Help v2 HD.ani"},
                {"AppStarting","[JB] Working v2 HD.ani"},{"Wait","[JB] Busy v2 HD.ani"},
                {"Crosshair","[JB] Precision v2 HD.ani"},{"IBeam","[JB] Text v2 HD.ani"},
                {"NWPen","[JB] Handwrite v2 HD.ani"},{"No","[JB] Unavailable v2 HD.ani"},
                {"SizeNS","[JB] VertRes v2 HD.ani"},{"SizeWE","[JB] HoriRes v2 HD.ani"},
                {"SizeNWSE","[JB] DiaRes1 v2 HD.ani"},{"SizeNESW","[JB] DiaRes2 v2 HD.ani"},
                {"SizeAll","[JB] Move v2 HD.ani"},{"UpArrow","[JB] Alternate v2 HD.ani"},
                {"Hand","[JB] Link v2 HD.ani"},{"Pin","[JB] Location v2 HD.ani"},
                {"Person","[JB] Person v2 HD.ani"}}},
        new Pack { DisplayName="Pointer White", Category="MINIMAL",
            Folder="point.er white", Accent=Color.FromArgb(189,189,189),
            Files=new Dictionary<string,string>{
                {"Arrow","pointer.cur"},{"Help","help.cur"},{"AppStarting","work.ani"},
                {"Wait","busy.ani"},{"Crosshair","cross.cur"},{"IBeam","text.cur"},
                {"NWPen","handwriting.cur"},{"No","unavailiable.cur"},{"SizeNS","vert.cur"},
                {"SizeWE","horz.cur"},{"SizeNWSE","dgn1.cur"},{"SizeNESW","dgn2.cur"},
                {"SizeAll","move.cur"},{"UpArrow","alternate.cur"},{"Hand","link.cur"},
                {"Pin","pin.cur"},{"Person","person.cur"}}},
        new Pack { DisplayName="Conspiracy", Category="ANIMATED",
            Folder="Conspiracy", Accent=Color.FromArgb(149,117,205),
            Files=new Dictionary<string,string>{
                {"Arrow","Arrow.ani"},{"Help","Help.ani"},{"AppStarting","Button.ani"},
                {"Wait","Wait.ani"},{"Crosshair","cross.ani"},{"IBeam","IBeam.ani"},
                {"NWPen","Handwriting.ani"},{"No","NO.ani"},{"SizeNS","SizeNS.ani"},
                {"SizeWE","SizeWE.ani"},{"SizeNWSE","SizeNWSE.ani"},{"SizeNESW","SizeNESW.ani"},
                {"SizeAll","SizeAll.ani"},{"UpArrow","UpArrow.ani"},{"Hand","Hand.ani"},
                {"Pin",""},{"Person",""}}},
        new Pack { DisplayName="Vision White", Category="VISION",
            Folder="vision cursor white", Accent=Color.FromArgb(0,188,212),
            Files=new Dictionary<string,string>{
                {"Arrow","pointer.cur"},{"Help","help.cur"},{"AppStarting","work.ani"},
                {"Wait","busy.ani"},{"Crosshair","cross.cur"},{"IBeam","text.cur"},
                {"NWPen","handwriting.cur"},{"No","unavailiable.cur"},{"SizeNS","vert.cur"},
                {"SizeWE","horz.cur"},{"SizeNWSE","dgn1.cur"},{"SizeNESW","dgn2.cur"},
                {"SizeAll","move.cur"},{"UpArrow","alternate.cur"},{"Hand","link.cur"},
                {"Pin","pin.cur"},{"Person","person.cur"}}},
        new Pack { DisplayName="Vision Black", Category="VISION",
            Folder="vision cursor black", Accent=Color.FromArgb(55,71,79),
            Files=new Dictionary<string,string>{
                {"Arrow","pointer.cur"},{"Help","help.cur"},{"AppStarting","work.ani"},
                {"Wait","busy.ani"},{"Crosshair","cross.cur"},{"IBeam","text.cur"},
                {"NWPen","handwriting.cur"},{"No","unavailiable.cur"},{"SizeNS","vert.cur"},
                {"SizeWE","horz.cur"},{"SizeNWSE","dgn1.cur"},{"SizeNESW","dgn2.cur"},
                {"SizeAll","move.cur"},{"UpArrow","alternate.cur"},{"Hand","link.cur"},
                {"Pin","pin.cur"},{"Person","person.cur"}}},
        new Pack { DisplayName="Prototype Ice", Category="PROTOTYPE",
            Folder="Prototype 01", Accent=Color.FromArgb(0,188,255),
            Files=new Dictionary<string,string>{
                {"Arrow",""},{"Help","Help.ani"},{"AppStarting","AppStarting.ani"},
                {"Wait","Wait.ani"},{"Crosshair","cross.ani"},{"IBeam","IBeam.cur"},
                {"NWPen","Handwriting.ani"},{"No","NO.ani"},{"SizeNS","SizeNS.ani"},
                {"SizeWE","SizeWE.ani"},{"SizeNWSE","SizeNWSE.ani"},{"SizeNESW","SizeNESW.ani"},
                {"SizeAll","SizeAll.ani"},{"UpArrow","UpArrow.ani"},{"Hand","Hand.ani"},
                {"Pin",""},{"Person",""}}},
        new Pack { DisplayName="Prototype Fire", Category="PROTOTYPE",
            Folder="Prototype02", Accent=Color.FromArgb(255,109,0),
            Files=new Dictionary<string,string>{
                {"Arrow","Hand.ani"},{"Help","Help.ani"},{"AppStarting","AppStarting.ani"},
                {"Wait","Wait.ani"},{"Crosshair","Cross.ani"},{"IBeam","IBeam.ani"},
                {"NWPen","Handwriting.ani"},{"No","NO.ani"},{"SizeNS","SizeNS.ani"},
                {"SizeWE","SizeWE.ani"},{"SizeNWSE","SizeNWSE.ani"},{"SizeNESW","SizeNESW.ani"},
                {"SizeAll","SizeAll.ani"},{"UpArrow","Arrow_Down.ani"},{"Hand",""},
                {"Pin",""},{"Person",""}}},
        new Pack { DisplayName="InfraRed v4", Category="DIM",
            Folder="DIM v4 - InfraRed", Accent=Color.FromArgb(255,23,68),
            Files=new Dictionary<string,string>{
                {"Arrow","[IR] Normal v4.cur"},{"Help","[IR] Help v4.ani"},
                {"AppStarting","[IR] Working v4.ani"},{"Wait","[IR] Busy v4.ani"},
                {"Crosshair","[IR] Precision v4.ani"},{"IBeam","[IR] Text v4.ani"},
                {"NWPen","[IR] Handwrite v4.ani"},{"No","[IR] Unavailable v4.ani"},
                {"SizeNS","[IR] VertRes v4.ani"},{"SizeWE","[IR] HoriRes v4.ani"},
                {"SizeNWSE","[IR] DiaRes1 v4.ani"},{"SizeNESW","[IR] DiaRes2 v4.ani"},
                {"SizeAll","[IR] Move v4.ani"},{"UpArrow","[IR] Alternate v4.ani"},
                {"Hand","[IR] Link v4.ani"},{"Pin","[IR] Location v4.ani"},
                {"Person","[IR] Person v4.ani"}}},
        new Pack { DisplayName="Windows Default", Category="SYSTEM",
            Folder="Default cursor", Accent=Color.FromArgb(68,138,255),
            Files=new Dictionary<string,string>{
                {"Arrow","aero_arrow.cur"},{"Help","aero_helpsel.cur"},
                {"AppStarting","aero_working.ani"},{"Wait","aero_busy.ani"},
                {"Crosshair",""},{"IBeam",""},{"NWPen","aero_pen.cur"},
                {"No","aero_unavail.cur"},{"SizeNS","aero_ns.cur"},{"SizeWE","aero_ew.cur"},
                {"SizeNWSE","aero_nwse.cur"},{"SizeNESW","aero_nesw.cur"},
                {"SizeAll","aero_move.cur"},{"UpArrow","aero_up.cur"},
                {"Hand","aero_link.cur"},{"Pin","aero_pin.cur"},{"Person","aero_person.cur"}}},
    };
}

// ──────────────────────────────────────────────────────────────────
// SETTINGS
// ──────────────────────────────────────────────────────────────────
static class Settings
{
    static readonly string _dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CursorSwitcher");
    static readonly string _file;
    public static string InstallDir { get; private set; }
    public static string ActivePack { get; set; }
    static Settings() { _file = Path.Combine(_dir, "settings.ini"); }
    public static void Load()
    {
        if (!File.Exists(_file)) return;
        foreach (string line in File.ReadAllLines(_file))
        {
            string[] p = line.Split(new char[]{'='}, 2);
            if (p.Length != 2) continue;
            switch (p[0].Trim())
            {
                case "InstallDir": InstallDir = p[1].Trim(); break;
                case "ActivePack": ActivePack  = p[1].Trim(); break;
            }
        }
    }
    public static void Save()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllLines(_file, new string[]{
            "InstallDir=" + (InstallDir ?? ""),
            "ActivePack="  + (ActivePack  ?? ""),
        });
    }
    public static void SetInstallDir(string d) { InstallDir = d; Save(); }
}

// ──────────────────────────────────────────────────────────────────
// CURSOR CARD  — fully custom-painted, dynamic width
// ──────────────────────────────────────────────────────────────────
class CursorCard : Panel
{
    public  Pack   PackData    { get { return _pack; } }
    readonly Pack  _pack;
    readonly string _installDir;

    IntPtr _hCursor  = IntPtr.Zero;
    uint   _aniStep  = 0;

    float _cardHoverT = 0f;
    float _prevBtnT   = 0f;
    float _applyBtnT  = 0f;
    float _activeT    = 0f;
    float _pulseT     = 0f;

    bool  _cardHovered  = false;
    bool  _prevBtnIn    = false;
    bool  _applyBtnIn   = false;
    bool  _prevPressed  = false;
    bool  _applyPressed = false;
    bool  _isActive     = false;
    bool  _isPreviewing = false;

    public event EventHandler<Pack> PreviewClicked;
    public event EventHandler<Pack> ApplyClicked;

    // Fixed height; width is set by the grid layout engine
    public const int CARD_H  = 204;
    const int RAD     = 10;
    const int STRIP   = 4;
    const int PREV_H  = 90;
    const int INFO_H  = 40;
    const int BTN_H   = 27;
    const int BTN_PAD = 8;
    // Y positions derived from top
    const int PREV_Y  = STRIP;
    const int INFO_Y  = PREV_Y + PREV_H + 7;
    const int BTN_Y   = INFO_Y + INFO_H + 4;

    static readonly Font _catFont  = new Font("Segoe UI", 6.5f, FontStyle.Bold);
    static readonly Font _nameFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
    static readonly Font _btnFont  = new Font("Segoe UI", 7.5f, FontStyle.Bold);
    static readonly Font _badgeFont= new Font("Segoe UI", 6f,   FontStyle.Bold);

    public CursorCard(Pack pack, string installDir)
    {
        _pack       = pack;
        _installDir = installDir;
        Height      = CARD_H;
        Cursor      = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TryLoadCursor();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateRegion();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
        Invalidate();
    }

    void UpdateRegion()
    {
        try {
            using (var path = Gfx.RoundRect(new RectangleF(0, 0, Width, Height), RAD))
                Region = new Region(path);
        } catch { }
    }

    void TryLoadCursor()
    {
        if (_hCursor != IntPtr.Zero) { Win32.DestroyCursor(_hCursor); _hCursor = IntPtr.Zero; }
        string p = _pack.NormalCursorPath(_installDir);
        if (p != null && File.Exists(p)) _hCursor = Win32.LoadCursorFromFile(p);
    }

    public void AnimTick(float pulse)
    {
        bool r = false;
        r |= Approach(ref _cardHoverT, _cardHovered   ? 1f : 0f, 0.18f);
        r |= Approach(ref _prevBtnT,   _prevBtnIn     ? 1f : 0f, 0.22f);
        r |= Approach(ref _applyBtnT,  _applyBtnIn    ? 1f : 0f, 0.22f);
        r |= Approach(ref _activeT,    _isActive      ? 1f : 0f, 0.12f);
        if (_isPreviewing) { _pulseT = pulse; r = true; }
        _aniStep = (_aniStep + 1) % 200;
        if (_hCursor != IntPtr.Zero) r = true;
        if (r) Invalidate();
    }

    static bool Approach(ref float val, float target, float rate)
    {
        float d = (target - val) * rate;
        if (Math.Abs(d) < 0.0005f) { val = target; return false; }
        val += d; return true;
    }

    Rectangle PrevR()
    {
        int bw = (Width - BTN_PAD * 2 - 6) / 2;
        return new Rectangle(BTN_PAD, BTN_Y, bw, BTN_H);
    }
    Rectangle ApplyR()
    {
        int bw = (Width - BTN_PAD * 2 - 6) / 2;
        return new Rectangle(BTN_PAD + bw + 6, BTN_Y, Width - BTN_PAD * 2 - bw - 6, BTN_H);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _cardHovered = true; }
    protected override void OnMouseLeave(EventArgs e)
    { base.OnMouseLeave(e); _cardHovered = _prevBtnIn = _applyBtnIn = _prevPressed = _applyPressed = false; }
    protected override void OnMouseMove(MouseEventArgs e)
    { base.OnMouseMove(e); _prevBtnIn = PrevR().Contains(e.Location); _applyBtnIn = ApplyR().Contains(e.Location); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _prevPressed  = PrevR().Contains(e.Location);
        _applyPressed = ApplyR().Contains(e.Location);
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        bool wp = _prevPressed, wa = _applyPressed;
        _prevPressed = _applyPressed = false;
        if (wp && PrevR().Contains(e.Location)  && PreviewClicked != null) PreviewClicked(this, _pack);
        if (wa && ApplyR().Contains(e.Location) && ApplyClicked   != null) ApplyClicked(this, _pack);
        Invalidate();
    }

    public void SetActive(bool v)     { _isActive     = v; Invalidate(); }
    public void SetPreviewing(bool v) { _isPreviewing = v; if (!v) _pulseT = 0f; Invalidate(); }
    public void ReloadCursor()        { TryLoadCursor(); }

    protected override void OnPaint(PaintEventArgs ev)
    {
        if (Width < 10 || Height < 10) return;
        Graphics g = ev.Graphics;
        Gfx.HQ(g);
        int w = Width;

        float ht = Gfx.Smooth(_cardHoverT);
        float at = Gfx.Smooth(_activeT);

        // ── Background ───────────────────────────────────────────
        Color bgBase  = Color.FromArgb(30, 30, 36);
        Color bgHover = Color.FromArgb(42, 42, 50);
        Color bgActiv = Color.FromArgb(
            (int)(30 + (_pack.Accent.R - 30) * 0.09f),
            (int)(30 + (_pack.Accent.G - 30) * 0.09f),
            (int)(30 + (_pack.Accent.B - 30) * 0.09f));
        Color bg = Gfx.Lerp(Gfx.Lerp(bgBase, bgHover, ht), bgActiv, at);
        using (var br = new SolidBrush(bg))
            Gfx.FillRR(g, br, new RectangleF(0, 0, w, Height), RAD);

        // ── Accent strip ─────────────────────────────────────────
        g.SetClip(new Rectangle(0, 0, w, STRIP));
        using (var lb = new LinearGradientBrush(new PointF(0,0), new PointF(w,0),
            _pack.Accent, Gfx.WithAlpha(_pack.Accent, 90)))
            g.FillRectangle(lb, 0, 0, w, STRIP);
        g.ResetClip();

        // ── Preview area ─────────────────────────────────────────
        var prevRF = new RectangleF(0, PREV_Y, w, PREV_H);
        g.FillRectangle(new SolidBrush(Color.FromArgb(14, 14, 20)), prevRF);

        // Radial centre glow
        float cx = w / 2f, cy2 = PREV_Y + PREV_H / 2f, rr = PREV_H * 0.52f;
        using (var ep = new GraphicsPath())
        {
            ep.AddEllipse(cx - rr, cy2 - rr, rr * 2, rr * 2);
            using (var pgb = new PathGradientBrush(ep))
            {
                pgb.CenterPoint    = new PointF(cx, cy2);
                pgb.CenterColor    = Color.FromArgb(40, 40, 50);
                pgb.SurroundColors = new Color[]{ Color.FromArgb(14, 14, 20) };
                g.FillEllipse(pgb, cx - rr, cy2 - rr, rr * 2, rr * 2);
            }
        }
        // Dot grid
        using (var dotBr = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
            for (int xx = 8; xx < w; xx += 13)
                for (int yy = PREV_Y + 5; yy < PREV_Y + PREV_H - 2; yy += 13)
                    g.FillRectangle(dotBr, xx, yy, 1, 1);
        // Bottom glow
        float gh = 16f;
        using (var glb = new LinearGradientBrush(
            new PointF(0, PREV_Y + PREV_H - gh), new PointF(0, PREV_Y + PREV_H),
            Color.Transparent, Gfx.WithAlpha(_pack.Accent, (int)(12 + 20 * at))))
            g.FillRectangle(glb, 0, PREV_Y + PREV_H - gh, w, gh);

        // ── Cursor sprite ────────────────────────────────────────
        if (_hCursor != IntPtr.Zero)
        {
            int csz = 48, cx3 = (w - csz) / 2, cy3 = PREV_Y + (PREV_H - csz) / 2;
            IntPtr hdc = g.GetHdc();
            Win32.DrawIconEx(hdc, cx3, cy3, _hCursor, csz, csz, _aniStep, IntPtr.Zero, Win32.DI_NORMAL);
            g.ReleaseHdc(hdc);
        }
        else
        {
            using (var br = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
            {
                int cx3 = w / 2 - 10, cy3 = PREV_Y + (PREV_H - 28) / 2;
                g.FillPolygon(br, new Point[]{
                    new Point(cx3,cy3), new Point(cx3,cy3+28),
                    new Point(cx3+10,cy3+20), new Point(cx3+14,cy3+28),
                    new Point(cx3+18,cy3+26), new Point(cx3+14,cy3+18),
                    new Point(cx3+22,cy3+18)});
            }
        }

        // ── Active badge ─────────────────────────────────────────
        if (_activeT > 0.02f)
        {
            int bw2 = 44, bh2 = 15, bx2 = w - bw2 - 6, by2 = PREV_Y + 6;
            using (var path = Gfx.RoundRect(new RectangleF(bx2, by2, bw2, bh2), 4))
            using (var br = new SolidBrush(Gfx.WithAlpha(_pack.Accent, (int)(210 * _activeT))))
                g.FillPath(br, path);
            Color badgeFg = Gfx.Luma(_pack.Accent) > 0.5
                ? Gfx.WithAlpha(Color.Black,   (int)(220 * _activeT))
                : Gfx.WithAlpha(Color.White, (int)(220 * _activeT));
            Gfx.DrawCentred(g, "ACTIVE", _badgeFont, badgeFg, new RectangleF(bx2, by2, bw2, bh2));
        }

        // ── Previewing ring ──────────────────────────────────────
        if (_isPreviewing)
        {
            int alpha = (int)(70 + 80 * _pulseT);
            using (var pen = new Pen(Gfx.WithAlpha(_pack.Accent, alpha), 1.5f))
                g.DrawRectangle(pen, 1, PREV_Y + 1, w - 3, PREV_H - 2);
        }

        // ── Info area: category + name ───────────────────────────
        float ix = 11f, iw = w - 22f;
        using (var br = new SolidBrush(Gfx.WithAlpha(_pack.Accent, 170)))
            g.DrawString(_pack.Category, _catFont, br, new RectangleF(ix, INFO_Y, iw, 16));
        using (var br = new SolidBrush(Color.FromArgb(232, 232, 238)))
            g.DrawString(_pack.DisplayName, _nameFont, br, new RectangleF(ix, INFO_Y + 15, iw, 22));

        // ── Buttons ──────────────────────────────────────────────
        DrawBtn(g, PrevR(),  "PREVIEW", false, _prevBtnT,  _prevPressed);
        DrawBtn(g, ApplyR(), "APPLY",   true,  _applyBtnT, _applyPressed);

        // ── Border ───────────────────────────────────────────────
        Color borderN = Color.FromArgb(46, 46, 54);
        Color borderH = Color.FromArgb(72, 72, 82);
        Color borderA = Gfx.WithAlpha(_pack.Accent, (int)(170 * at));
        Color borderP = Gfx.WithAlpha(_pack.Accent, (int)(110 + 110 * _pulseT));
        Color border;
        float bwidth;
        if (_isPreviewing) { border = borderP; bwidth = 1.5f; }
        else if (at > 0.05f) { border = Gfx.Lerp(Gfx.Lerp(borderN, borderH, ht), borderA, at); bwidth = 1.5f; }
        else { border = Gfx.Lerp(borderN, borderH, ht); bwidth = 1f; }
        using (var pen = new Pen(border, bwidth))
            Gfx.DrawRR(g, pen, new RectangleF(0.5f, 0.5f, w - 1, Height - 1), RAD);
    }

    void DrawBtn(Graphics g, Rectangle r, string label, bool filled, float hoverT, bool pressed)
    {
        float ht2 = Gfx.Smooth(hoverT);
        var rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
        if (filled)
        {
            Color fill = pressed ? Gfx.Darken(_pack.Accent, 18)
                                 : Gfx.Lerp(_pack.Accent, Gfx.Lighten(_pack.Accent, 22), ht2);
            using (var br = new SolidBrush(fill))
                Gfx.FillRR(g, br, rf, 5f);
            Color fg = Gfx.Luma(_pack.Accent) > 0.5
                ? Color.FromArgb(20, 20, 20) : Color.FromArgb(245, 245, 245);
            Gfx.DrawCentred(g, label, _btnFont, fg, rf);
        }
        else
        {
            Color bg2  = pressed ? Gfx.WithAlpha(_pack.Accent, 40)
                                 : Gfx.WithAlpha(_pack.Accent, (int)(28 * ht2));
            Color bord = Gfx.WithAlpha(_pack.Accent, (int)(130 + 80 * ht2));
            using (var br = new SolidBrush(bg2))
                Gfx.FillRR(g, br, rf, 5f);
            using (var pen = new Pen(bord, 1f))
                Gfx.DrawRR(g, pen, rf, 5f);
            Color fg = Gfx.WithAlpha(_pack.Accent, (int)(175 + 65 * ht2));
            Gfx.DrawCentred(g, label, _btnFont, fg, rf);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _hCursor != IntPtr.Zero)
        { Win32.DestroyCursor(_hCursor); _hCursor = IntPtr.Zero; }
        base.Dispose(disposing);
    }
}

// ──────────────────────────────────────────────────────────────────
// SECTION HEADER LABEL  — drawn inline in the card panel
// ──────────────────────────────────────────────────────────────────
class SectionHeader : Panel
{
    readonly string _label;
    readonly Color  _accent;
    static readonly Font _font = new Font("Segoe UI", 8f, FontStyle.Bold);

    public SectionHeader(string label, Color accent)
    {
        _label    = label;
        _accent   = accent;
        Height    = 30;
        BackColor = Color.Transparent;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Gfx.HQ(g);
        int w = Width;

        // Label text
        SizeF ts = g.MeasureString(_label, _font);
        int tx = 4;
        using (var br = new SolidBrush(Gfx.WithAlpha(_accent, 200)))
            g.DrawString(_label, _font, br, tx, (Height - ts.Height) / 2f);

        // Rule line to the right of the label
        int lx = tx + (int)ts.Width + 10;
        int ly = Height / 2;
        using (var pen = new Pen(Gfx.WithAlpha(_accent, 45), 1f))
            g.DrawLine(pen, lx, ly, w - 8, ly);
    }
}

// ──────────────────────────────────────────────────────────────────
// FILTER BAR  — category pill buttons
// ──────────────────────────────────────────────────────────────────
class FilterBar : Panel
{
    static readonly string[] CATS = {
        "ALL","NIGHT DIAMOND","KAMI v2 HD","MINIMAL",
        "ANIMATED","VISION","PROTOTYPE","DIM","SYSTEM"
    };
    // Accent colour per category (index matches CATS)
    static readonly Color[] CAT_ACCENT = {
        Color.FromArgb(90,140,255),
        Color.FromArgb(0,200,83),
        Color.FromArgb(210,210,210),
        Color.FromArgb(189,189,189),
        Color.FromArgb(149,117,205),
        Color.FromArgb(0,188,212),
        Color.FromArgb(0,188,255),
        Color.FromArgb(255,23,68),
        Color.FromArgb(68,138,255),
    };

    int      _sel     = 0;   // selected index
    int      _hover   = -1;
    float[]  _hoverT;
    Rectangle[] _rects;

    public string Selected { get { return CATS[_sel]; } }
    public event EventHandler FilterChanged;

    static readonly Font _font = new Font("Segoe UI", 8f, FontStyle.Bold);

    public FilterBar()
    {
        Height    = 44;
        BackColor = Color.FromArgb(16, 16, 22);
        _hoverT   = new float[CATS.Length];
        _rects    = new Rectangle[CATS.Length];
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }

    public void AnimTick()
    {
        bool r = false;
        for (int i = 0; i < _hoverT.Length; i++)
        {
            float target = (_hover == i) ? 1f : 0f;
            float d = (target - _hoverT[i]) * 0.22f;
            if (Math.Abs(d) > 0.0005f) { _hoverT[i] += d; r = true; }
            else _hoverT[i] = target;
        }
        if (r) Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Gfx.HQ(g);
        int w = Width;

        // Background + bottom separator
        g.FillRectangle(new SolidBrush(Color.FromArgb(16, 16, 22)), 0, 0, w, Height);
        using (var pen = new Pen(Color.FromArgb(36, 36, 44), 1))
            g.DrawLine(pen, 0, Height - 1, w, Height - 1);

        // Lay out pills left to right with 6px gap
        int x = 12, y = (Height - 24) / 2;
        using (Graphics mg = Graphics.FromImage(new Bitmap(1, 1)))
        {
            for (int i = 0; i < CATS.Length; i++)
            {
                SizeF ts = mg.MeasureString(CATS[i], _font);
                int pw = (int)ts.Width + 20;
                _rects[i] = new Rectangle(x, y, pw, 24);
                x += pw + 6;
            }
        }

        // Draw pills
        for (int i = 0; i < CATS.Length; i++)
        {
            bool sel  = (_sel == i);
            float ht2 = Gfx.Smooth(_hoverT[i]);
            Color ac  = CAT_ACCENT[i];
            var rf    = new RectangleF(_rects[i].X, _rects[i].Y, _rects[i].Width, _rects[i].Height);

            if (sel)
            {
                // Filled pill
                using (var br = new SolidBrush(ac))
                    Gfx.FillRR(g, br, rf, 12f);
                Color fg = Gfx.Luma(ac) > 0.5 ? Color.FromArgb(18,18,18) : Color.White;
                Gfx.DrawCentred(g, CATS[i], _font, fg, rf);
            }
            else
            {
                // Outline pill
                Color bg2   = Gfx.WithAlpha(ac, (int)(22 * ht2));
                Color bord2 = Gfx.WithAlpha(ac, (int)(60 + 60 * ht2));
                Color fg2   = Gfx.WithAlpha(ac, (int)(130 + 80 * ht2));
                using (var br = new SolidBrush(bg2))
                    Gfx.FillRR(g, br, rf, 12f);
                using (var pen = new Pen(bord2, 1f))
                    Gfx.DrawRR(g, pen, rf, 12f);
                Gfx.DrawCentred(g, CATS[i], _font, fg2, rf);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int prev = _hover; _hover = -1;
        for (int i = 0; i < _rects.Length; i++)
            if (_rects[i].Contains(e.Location)) { _hover = i; break; }
        if (_hover != prev) Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    { base.OnMouseLeave(e); _hover = -1; Invalidate(); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        for (int i = 0; i < _rects.Length; i++)
            if (_rects[i].Contains(e.Location) && _sel != i)
            {
                _sel = i; Invalidate();
                if (FilterChanged != null) FilterChanged(this, EventArgs.Empty);
                break;
            }
    }
}

// ──────────────────────────────────────────────────────────────────
// ACTIVE PACK BANNER
// ──────────────────────────────────────────────────────────────────
class ActiveBanner : Panel
{
    Pack _pack = null;
    static readonly Font _catFont  = new Font("Segoe UI", 7.5f, FontStyle.Bold);
    static readonly Font _nameFont = new Font("Segoe UI", 12f,  FontStyle.Bold);
    static readonly Font _tagFont  = new Font("Segoe UI", 7f,   FontStyle.Bold);

    public ActiveBanner()
    {
        Height    = 54;
        BackColor = Color.FromArgb(14, 14, 20);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public void SetPack(Pack p)
    {
        _pack    = p;
        Visible  = (p != null);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_pack == null) return;
        Graphics g = e.Graphics;
        Gfx.HQ(g);
        int w = Width;
        Color ac = _pack.Accent;

        // Background gradient: dark → very slight accent tint
        using (var lb = new LinearGradientBrush(new Point(0,0), new Point(w, 0),
            Color.FromArgb(20, 20, 28),
            Color.FromArgb(
                (int)(20 + (ac.R - 20) * 0.08f),
                (int)(20 + (ac.G - 20) * 0.08f),
                (int)(20 + (ac.B - 20) * 0.08f))))
            g.FillRectangle(lb, 0, 0, w, Height);

        // Left accent bar (4px)
        using (var br = new SolidBrush(ac))
            g.FillRectangle(br, 0, 0, 4, Height);

        // Left glow behind bar
        using (var gb = new LinearGradientBrush(new PointF(4,0), new PointF(80,0),
            Gfx.WithAlpha(ac, 30), Color.Transparent))
            g.FillRectangle(gb, 4, 0, 80, Height);

        // Top & bottom separator lines
        using (var pen = new Pen(Color.FromArgb(38, 38, 48), 1))
        {
            g.DrawLine(pen, 0, 0,          w, 0);
            g.DrawLine(pen, 0, Height - 1, w, Height - 1);
        }

        // Category label
        using (var br = new SolidBrush(Gfx.WithAlpha(ac, 180)))
            g.DrawString(_pack.Category, _catFont, br, 16, 10);

        // Pack name
        using (var br = new SolidBrush(Color.FromArgb(235, 235, 240)))
            g.DrawString(_pack.DisplayName, _nameFont, br, 14, 25);

        // "CURRENTLY APPLIED" badge on the right
        int bw2 = 120, bh = 18;
        int bx = w - bw2 - 16, by2 = (Height - bh) / 2;
        using (var path = Gfx.RoundRect(new RectangleF(bx, by2, bw2, bh), 4))
        using (var br = new SolidBrush(Gfx.WithAlpha(ac, 28)))
            g.FillPath(br, path);
        using (var path = Gfx.RoundRect(new RectangleF(bx, by2, bw2, bh), 4))
        using (var pen = new Pen(Gfx.WithAlpha(ac, 80), 1))
            g.DrawPath(pen, path);
        Color tagFg = Gfx.WithAlpha(ac, 190);
        Gfx.DrawCentred(g, "CURRENTLY APPLIED", _tagFont, tagFg,
            new RectangleF(bx, by2, bw2, bh));
    }
}

// ──────────────────────────────────────────────────────────────────
// TOAST PANEL
// ──────────────────────────────────────────────────────────────────
class ToastPanel : Panel
{
    Label  _lblText;
    Button _btnApply;
    Button _btnRevert;
    Color  _accent = Color.FromArgb(68, 138, 255);
    const int TOAST_H = 50;

    public event EventHandler ApplyClicked;
    public event EventHandler RevertClicked;

    public ToastPanel()
    {
        Height    = TOAST_H;
        Dock      = DockStyle.Bottom;
        BackColor = Color.FromArgb(18, 18, 26);
        Visible   = false;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);

        _lblText = new Label {
            AutoSize  = false,
            ForeColor = Color.FromArgb(210, 210, 220),
            Font      = new Font("Segoe UI", 8.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            Left = 20, Top = 0, Width = 560, Height = TOAST_H,
        };

        _btnApply  = MakeBtn("Apply Now", true);
        _btnApply.Click += delegate { if (ApplyClicked  != null) ApplyClicked(this, EventArgs.Empty); };
        _btnRevert = MakeBtn("Revert",    false);
        _btnRevert.Click += delegate { if (RevertClicked != null) RevertClicked(this, EventArgs.Empty); };

        Controls.Add(_lblText);
        Controls.Add(_btnApply);
        Controls.Add(_btnRevert);
    }

    Button MakeBtn(string text, bool filled)
    {
        return new Button {
            Text = text, Size = new Size(96, 28),
            Top = (TOAST_H - 28) / 2,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
    }

    public void Show2(string packName, Color accent)
    {
        _accent        = accent;
        _lblText.Text  = string.Format("Previewing  \u2022  {0}   \u2014   move your mouse to test it", packName);
        Color fg       = Gfx.Luma(accent) > 0.5 ? Color.Black : Color.White;

        _btnApply.BackColor  = accent;
        _btnApply.ForeColor  = fg;
        _btnApply.FlatAppearance.BorderColor = accent;
        _btnApply.FlatAppearance.BorderSize  = 0;

        _btnRevert.BackColor = Color.FromArgb(46, 46, 56);
        _btnRevert.ForeColor = Color.FromArgb(190, 190, 205);
        _btnRevert.FlatAppearance.BorderColor = Color.FromArgb(65, 65, 75);
        _btnRevert.FlatAppearance.BorderSize  = 1;

        PositionButtons();
        Visible = true;
        Invalidate();
    }

    public void Hide2() { Visible = false; }

    void PositionButtons()
    {
        if (_btnRevert == null || _btnApply == null || Width == 0) return;
        _btnRevert.Left = Width - 112;
        _btnApply.Left  = Width - 216;
    }

    protected override void OnResize(EventArgs e)
    { base.OnResize(e); PositionButtons(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.FillRectangle(new SolidBrush(Color.FromArgb(18, 18, 26)), ClientRectangle);
        using (var br = new SolidBrush(_accent))
            g.FillRectangle(br, 0, 0, 4, Height);
        using (var gb = new LinearGradientBrush(new PointF(4,0), new PointF(70,0),
            Gfx.WithAlpha(_accent, 28), Color.Transparent))
            g.FillRectangle(gb, 4, 0, 70, Height);
        using (var pen = new Pen(Color.FromArgb(40, 40, 52), 1))
            g.DrawLine(pen, 0, 0, Width, 0);
    }
}

// ──────────────────────────────────────────────────────────────────
// CARD GRID PANEL  — manual 4-column layout, vertical scroll only
// ──────────────────────────────────────────────────────────────────
class CardGridPanel : Panel
{
    public CardGridPanel()
    {
        BackColor  = Color.FromArgb(18, 18, 22);
        AutoScroll = true;
        // Hide horizontal scroll bar
        HorizontalScroll.Maximum = 0;
        HorizontalScroll.Enabled = false;
        AutoScrollMinSize = new Size(1, 1);
    }

    protected override System.Windows.Forms.CreateParams CreateParams
    {
        get {
            // WS_VSCROLL only — suppress horizontal scrollbar at the Win32 level
            var cp = base.CreateParams;
            cp.Style &= ~0x00100000; // remove WS_HSCROLL
            return cp;
        }
    }
}

// ──────────────────────────────────────────────────────────────────
// MAIN FORM
// ──────────────────────────────────────────────────────────────────
class MainForm : Form
{
    Panel         _titleBar;
    Label         _lblTitle;
    Label         _lblSubtitle;
    Label         _btnMin;
    Label         _btnClose;
    FilterBar     _filterBar;
    ActiveBanner  _activeBanner;
    CardGridPanel _grid;
    ToastPanel    _toast;
    Timer         _animTimer;

    // Inner panel that holds both section headers and cards
    Panel         _innerPanel;

    List<CursorCard>    _cards   = new List<CursorCard>();
    List<SectionHeader> _headers = new List<SectionHeader>();
    CursorCard          _prevCard = null;
    float               _pulse   = 0f;
    float               _pDir    = 0.025f;
    string              _currentFilter = "ALL";

    const int TITLE_H   = 54;
    const int FILTER_H  = 44;
    const int BANNER_H  = 54;
    const int PAD_X     = 14;
    const int PAD_Y     = 12;
    const int GAP_X     = 10;
    const int GAP_Y     = 10;
    const int COLS      = 4;
    const int HDR_H     = 30;

    public MainForm()
    {
        Settings.Load();
        Text            = "Cursor Switcher";
        Size            = new Size(1010, 720);
        MinimumSize     = new Size(760, 520);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = Color.FromArgb(18, 18, 22);
        DoubleBuffered  = true;

        BuildChrome();
        Load += delegate(object s, EventArgs e) { EnableDwmShadow(); };

        if (string.IsNullOrEmpty(Settings.InstallDir) || !Directory.Exists(Settings.InstallDir))
        {
            if (!FirstRunSetup()) { Load += delegate { Close(); }; return; }
        }
        else
        {
            EnsureExtracted();
        }

        BuildCards();
        LayoutGrid();
        UpdateHeader();
    }

    // ── DWM shadow ───────────────────────────────────────────────
    void EnableDwmShadow()
    {
        try {
            bool comp;
            Win32.DwmIsCompositionEnabled(out comp);
            if (!comp) return;
            var m = new Win32.MARGINS { Left=1, Right=1, Top=1, Bottom=1 };
            Win32.DwmExtendFrameIntoClientArea(Handle, ref m);
        } catch { }
    }

    // ── Borderless resize + drag ─────────────────────────────────
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32.WM_NCHITTEST)
        {
            int lp = m.LParam.ToInt32();
            Point p = PointToClient(new Point(lp & 0xFFFF, (lp >> 16) & 0xFFFF));
            int e = 6;
            bool L = p.X < e, R = p.X > Width - e, T = p.Y < e, B = p.Y > Height - e;
            if      (L && T) m.Result = (IntPtr)Win32.HTTOPLEFT;
            else if (R && T) m.Result = (IntPtr)Win32.HTTOPRIGHT;
            else if (L && B) m.Result = (IntPtr)Win32.HTBOTTOMLEFT;
            else if (R && B) m.Result = (IntPtr)Win32.HTBOTTOMRIGHT;
            else if (L)      m.Result = (IntPtr)Win32.HTLEFT;
            else if (R)      m.Result = (IntPtr)Win32.HTRIGHT;
            else if (T)      m.Result = (IntPtr)Win32.HTTOP;
            else if (B)      m.Result = (IntPtr)Win32.HTBOTTOM;
            else if (p.Y < TITLE_H && !_btnMin.Bounds.Contains(p) && !_btnClose.Bounds.Contains(p))
                             m.Result = (IntPtr)Win32.HTCAPTION;
            else             m.Result = (IntPtr)Win32.HTCLIENT;
            return;
        }
        base.WndProc(ref m);
    }

    // ── Chrome ───────────────────────────────────────────────────
    void BuildChrome()
    {
        // ── Title bar ────────────────────────────────────────────
        _titleBar = new Panel {
            Dock = DockStyle.Top, Height = TITLE_H,
            BackColor = Color.FromArgb(12, 12, 18),
        };
        _titleBar.Paint += OnTitlePaint;

        _lblTitle = new Label {
            Text = "CURSOR SWITCHER",
            ForeColor = Color.FromArgb(235,235,242),
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            AutoSize = true, Location = new Point(18, 10),
            BackColor = Color.Transparent,
        };

        _lblSubtitle = new Label {
            Text = "No cursor pack applied",
            ForeColor = Color.FromArgb(100,100,115),
            Font = new Font("Segoe UI", 8f),
            AutoSize = true, Location = new Point(20, 36),
            BackColor = Color.Transparent,
        };

        _btnMin = new Label {
            Text = "\u2013", Font = new Font("Segoe UI", 13f),
            ForeColor = Color.FromArgb(130,130,145),
            AutoSize = false, Size = new Size(46, TITLE_H),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand, BackColor = Color.Transparent,
        };
        _btnMin.MouseEnter += delegate { _btnMin.ForeColor = Color.White; _titleBar.Invalidate(); };
        _btnMin.MouseLeave += delegate { _btnMin.ForeColor = Color.FromArgb(130,130,145); _titleBar.Invalidate(); };
        _btnMin.Click      += delegate { WindowState = FormWindowState.Minimized; };

        _btnClose = new Label {
            Text = "\u00d7", Font = new Font("Segoe UI", 15f),
            ForeColor = Color.FromArgb(130,130,145),
            AutoSize = false, Size = new Size(46, TITLE_H),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand, BackColor = Color.Transparent,
        };
        _btnClose.MouseEnter += delegate { _btnClose.BackColor = Color.FromArgb(196,43,28); _btnClose.ForeColor = Color.White; };
        _btnClose.MouseLeave += delegate { _btnClose.BackColor = Color.Transparent; _btnClose.ForeColor = Color.FromArgb(130,130,145); };
        _btnClose.Click      += delegate { Close(); };

        _titleBar.Controls.AddRange(new Control[]{ _lblTitle, _lblSubtitle, _btnMin, _btnClose });
        Controls.Add(_titleBar);

        // ── Filter bar ───────────────────────────────────────────
        _filterBar = new FilterBar { Dock = DockStyle.Top };
        _filterBar.FilterChanged += delegate { _currentFilter = _filterBar.Selected; LayoutGrid(); };
        Controls.Add(_filterBar);

        // ── Active banner ────────────────────────────────────────
        _activeBanner = new ActiveBanner { Dock = DockStyle.Top, Visible = false };
        Controls.Add(_activeBanner);

        // ── Card grid ────────────────────────────────────────────
        _grid = new CardGridPanel { Dock = DockStyle.Fill };

        // Inner panel that gets manually positioned inside the grid
        _innerPanel = new Panel {
            BackColor = Color.FromArgb(18, 18, 22),
            Location  = new Point(0, 0),
        };
        _grid.Controls.Add(_innerPanel);
        _grid.Resize += delegate { LayoutGrid(); };
        Controls.Add(_grid);

        // ── Toast ─────────────────────────────────────────────────
        _toast = new ToastPanel();
        _toast.ApplyClicked  += delegate { if (_toast.Tag is Pack) ApplyPack((Pack)_toast.Tag); };
        _toast.RevertClicked += OnRevert;
        Controls.Add(_toast);

        // ── Animation timer (≈60fps) ──────────────────────────────
        _animTimer = new Timer { Interval = 16 };
        _animTimer.Tick += OnAnimTick;
        _animTimer.Start();

        FormClosed += delegate {
            if (_prevCard != null)
                Win32.SystemParametersInfo(Win32.SPI_SETCURSORS, 0, IntPtr.Zero, Win32.SPIF_UPDATE);
        };

        _titleBar.SizeChanged += delegate { RepositionTitleBtns(); };
        SizeChanged           += delegate { RepositionTitleBtns(); };
        RepositionTitleBtns();
    }

    void RepositionTitleBtns()
    {
        _btnClose.Location = new Point(_titleBar.Width - 46, 0);
        _btnMin.Location   = new Point(_titleBar.Width - 92, 0);
    }

    void OnTitlePaint(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Gfx.HQ(g);
        int w = _titleBar.Width;
        Color ac = GetActiveAccent();

        // Bottom separator
        using (var pen = new Pen(Color.FromArgb(32, 32, 40), 1))
            g.DrawLine(pen, 0, TITLE_H - 1, w, TITLE_H - 1);

        // Left accent glow
        using (var gb = new LinearGradientBrush(new PointF(0,0), new PointF(140,0),
            Gfx.WithAlpha(ac, 22), Color.Transparent))
            g.FillRectangle(gb, 0, 0, 140, TITLE_H);

        // Top accent line
        int lineW = Math.Min(500, w);
        using (var lb = new LinearGradientBrush(new PointF(0,0), new PointF(lineW, 0),
            Gfx.WithAlpha(ac, 200), Color.Transparent))
            g.FillRectangle(lb, 0, 0, lineW, 2);

        // Pack count badge
        string badge = "13 PACKS";
        using (Font f = new Font("Segoe UI", 7f, FontStyle.Bold))
        {
            SizeF ts = g.MeasureString(badge, f);
            int bx = (int)(_lblTitle.Right + 10);
            int by2 = TITLE_H / 2 - 8;
            int bw2 = (int)ts.Width + 14, bh = 16;
            using (var path = Gfx.RoundRect(new RectangleF(bx, by2, bw2, bh), 4))
            using (var br = new SolidBrush(Gfx.WithAlpha(ac, 30)))
                g.FillPath(br, path);
            using (var path = Gfx.RoundRect(new RectangleF(bx, by2, bw2, bh), 4))
            using (var pen = new Pen(Gfx.WithAlpha(ac, 70), 1))
                g.DrawPath(pen, path);
            Gfx.DrawCentred(g, badge, f, Gfx.WithAlpha(ac, 160),
                new RectangleF(bx, by2, bw2, bh));
        }
    }

    Color GetActiveAccent()
    {
        foreach (var c in _cards)
            if (c.PackData.ShortName() == Settings.ActivePack) return c.PackData.Accent;
        return Color.FromArgb(80, 130, 220);
    }

    // ── 60fps tick ───────────────────────────────────────────────
    void OnAnimTick(object sender, EventArgs e)
    {
        _pulse += _pDir;
        if (_pulse >= 1f) { _pulse = 1f; _pDir = -_pDir; }
        if (_pulse <= 0f) { _pulse = 0f; _pDir = -_pDir; }
        foreach (var c in _cards) c.AnimTick(_pulse);
        _filterBar.AnimTick();
    }

    // ── Grid layout  (4 columns, section headers, vertical only) ─
    void LayoutGrid()
    {
        if (_innerPanel == null || _grid == null) return;
        _innerPanel.SuspendLayout();

        // Measure available width (subtract scrollbar width conservatively)
        int avail = _grid.ClientSize.Width - PAD_X * 2 - SystemInformation.VerticalScrollBarWidth - 2;
        if (avail < 100) { _innerPanel.ResumeLayout(); return; }
        int cw = (avail - GAP_X * (COLS - 1)) / COLS;
        if (cw < 80) { _innerPanel.ResumeLayout(); return; }

        // Rebuild section headers
        foreach (var h in _headers) { _innerPanel.Controls.Remove(h); h.Dispose(); }
        _headers.Clear();

        bool showHeaders = (_currentFilter == "ALL");
        string lastCat   = null;
        int x = PAD_X, y = PAD_Y, col = 0;

        foreach (CursorCard card in _cards)
        {
            bool vis = (_currentFilter == "ALL" || card.PackData.Category == _currentFilter);
            card.Visible = vis;
            if (!vis) continue;

            // Section header for new category
            if (showHeaders && card.PackData.Category != lastCat)
            {
                // Finish current row first
                if (col > 0) { y += CursorCard.CARD_H + GAP_Y; col = 0; x = PAD_X; }
                if (lastCat != null) y += 6; // extra gap between categories

                var hdr = new SectionHeader(card.PackData.Category, card.PackData.Accent);
                hdr.Location = new Point(PAD_X, y);
                hdr.Width    = avail;
                _headers.Add(hdr);
                if (!_innerPanel.Controls.Contains(hdr))
                    _innerPanel.Controls.Add(hdr);
                hdr.BringToFront();
                y += HDR_H + 4;
                lastCat = card.PackData.Category;
                col = 0; x = PAD_X;
            }

            card.Size     = new Size(cw, CursorCard.CARD_H);
            card.Location = new Point(x, y);
            x += cw + GAP_X;

            if (++col >= COLS) { col = 0; x = PAD_X; y += CursorCard.CARD_H + GAP_Y; }
        }

        if (col > 0) y += CursorCard.CARD_H + GAP_Y;
        y += PAD_Y;

        _innerPanel.Width  = _grid.ClientSize.Width - 2;
        _innerPanel.Height = Math.Max(y, _grid.ClientSize.Height);
        _grid.AutoScrollMinSize = new Size(1, y);

        _innerPanel.ResumeLayout();
        _innerPanel.Invalidate(true);
    }

    // ── Build cards ──────────────────────────────────────────────
    void BuildCards()
    {
        _innerPanel.SuspendLayout();
        foreach (var c in _cards) { _innerPanel.Controls.Remove(c); c.Dispose(); }
        _cards.Clear();

        foreach (Pack p in AllPacks.List)
        {
            var card = new CursorCard(p, Settings.InstallDir);
            card.PreviewClicked += delegate(object s, Pack pk) { OnPreview(pk); };
            card.ApplyClicked   += delegate(object s, Pack pk) { ApplyPack(pk); };
            _cards.Add(card);
            _innerPanel.Controls.Add(card);
        }
        _innerPanel.ResumeLayout();
        SyncActiveCards();
    }

    // ── Preview ──────────────────────────────────────────────────
    void OnPreview(Pack pack)
    {
        string path = pack.NormalCursorPath(Settings.InstallDir);
        if (path == null || !File.Exists(path))
        {
            foreach (var kv in pack.Files)
            {
                if (string.IsNullOrEmpty(kv.Value)) continue;
                string alt = Path.Combine(Settings.InstallDir, pack.Folder, kv.Value);
                if (File.Exists(alt)) { path = alt; break; }
            }
        }
        if (path == null || !File.Exists(path)) return;

        IntPtr hCur  = Win32.LoadCursorFromFile(path);
        if (hCur == IntPtr.Zero) return;
        IntPtr hCopy = Win32.CopyImage(hCur, Win32.IMAGE_CURSOR, 0, 0, Win32.LR_DEFAULTSIZE);
        Win32.DestroyCursor(hCur);
        if (hCopy == IntPtr.Zero) return;
        Win32.SetSystemCursor(hCopy, Win32.OCR_NORMAL);

        if (_prevCard != null) _prevCard.SetPreviewing(false);
        _prevCard = _cards.Find(delegate(CursorCard c) { return c.PackData == pack; });
        if (_prevCard != null) _prevCard.SetPreviewing(true);

        _toast.Show2(pack.ShortName(), pack.Accent);
        _toast.Tag = pack;
    }

    void OnRevert(object sender, EventArgs e)
    {
        Win32.SystemParametersInfo(Win32.SPI_SETCURSORS, 0, IntPtr.Zero, Win32.SPIF_UPDATE);
        if (_prevCard != null) { _prevCard.SetPreviewing(false); _prevCard = null; }
        _toast.Hide2();
    }

    // ── Apply ────────────────────────────────────────────────────
    void ApplyPack(Pack pack)
    {
        try {
            using (RegistryKey key =
                Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors", true)
                ?? Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors"))
            {
                foreach (KeyValuePair<string,string> kv in pack.Files)
                {
                    string full = string.IsNullOrEmpty(kv.Value) ? ""
                        : Path.Combine(Settings.InstallDir, pack.Folder, kv.Value);
                    key.SetValue(kv.Key, full, RegistryValueKind.ExpandString);
                }
                key.SetValue("", pack.ShortName(), RegistryValueKind.String);
            }
        } catch (Exception ex) {
            MessageBox.Show("Registry write failed:\n\n" + ex.Message, "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Win32.SystemParametersInfo(Win32.SPI_SETCURSORS, 0, IntPtr.Zero, Win32.SPIF_UPDATE);
        if (_prevCard != null) { _prevCard.SetPreviewing(false); _prevCard = null; }
        _toast.Hide2();

        Settings.ActivePack = pack.ShortName();
        Settings.Save();
        SyncActiveCards();
        UpdateHeader();
        _titleBar.Invalidate();
        _activeBanner.SetPack(pack);
    }

    void SyncActiveCards()
    {
        foreach (var c in _cards)
            c.SetActive(c.PackData.ShortName() == Settings.ActivePack);
    }

    void UpdateHeader()
    {
        if (string.IsNullOrEmpty(Settings.ActivePack))
        {
            _lblSubtitle.Text = "No cursor pack applied";
            _activeBanner.SetPack(null);
        }
        else
        {
            _lblSubtitle.Text = "Active:  " + Settings.ActivePack;
            Pack ap = null;
            foreach (Pack p in AllPacks.List)
                if (p.ShortName() == Settings.ActivePack) { ap = p; break; }
            _activeBanner.SetPack(ap);
        }
    }

    // ── First-run + extraction ────────────────────────────────────
    bool FirstRunSetup()
    {
        using (var dlg = new FirstRunDialog())
        {
            if (dlg.ShowDialog() != DialogResult.OK) return false;
            Settings.SetInstallDir(dlg.ChosenDir);
        }
        using (var prog = new ExtractDialog(Settings.InstallDir))
        {
            prog.ShowDialog();
            if (!prog.Success)
            {
                MessageBox.Show("Extraction failed:\n\n" + prog.Error,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        return true;
    }

    void EnsureExtracted()
    {
        foreach (var p in AllPacks.List)
            if (!Directory.Exists(Path.Combine(Settings.InstallDir, p.Folder)))
            {
                using (var prog = new ExtractDialog(Settings.InstallDir))
                    prog.ShowDialog();
                return;
            }
    }
}

// ──────────────────────────────────────────────────────────────────
// FIRST RUN DIALOG
// ──────────────────────────────────────────────────────────────────
class FirstRunDialog : Form
{
    public string ChosenDir { get; private set; }
    Label  _lblPath;
    Button _btnBrowse, _btnOK, _btnCancel;

    public FirstRunDialog()
    {
        Text            = "Cursor Switcher — Setup";
        Size            = new Size(520, 260);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterScreen;
        MaximizeBox     = false;
        BackColor       = Color.FromArgb(22, 22, 30);

        var lblTitle = new Label {
            Text="First-Time Setup", ForeColor=Color.FromArgb(235,235,242),
            Font=new Font("Segoe UI", 13f, FontStyle.Bold),
            AutoSize=true, Location=new Point(24,20),
        };
        var lblInfo = new Label {
            Text="Choose a folder where your cursor packs will be installed.\n" +
                 "A  CursorSwitcher  subfolder will be created inside it.",
            ForeColor=Color.FromArgb(135,135,150), Font=new Font("Segoe UI",9f),
            AutoSize=false, Size=new Size(470,44), Location=new Point(24,52),
        };
        var lblPT = new Label {
            Text="Install location:", ForeColor=Color.FromArgb(135,135,150),
            Font=new Font("Segoe UI",8.5f), AutoSize=true, Location=new Point(24,108),
        };
        _lblPath = new Label {
            Text="No folder selected", ForeColor=Color.FromArgb(80,80,95),
            Font=new Font("Segoe UI",8.5f), AutoSize=false,
            Size=new Size(334,26), Location=new Point(24,128),
            BorderStyle=BorderStyle.FixedSingle,
            BackColor=Color.FromArgb(14,14,20), TextAlign=ContentAlignment.MiddleLeft,
        };
        _btnBrowse = new Button {
            Text="Browse...", Size=new Size(90,26), Location=new Point(364,128),
            FlatStyle=FlatStyle.Flat, ForeColor=Color.FromArgb(175,175,190),
            BackColor=Color.FromArgb(38,38,50),
        };
        _btnBrowse.FlatAppearance.BorderColor=Color.FromArgb(58,58,72);
        _btnBrowse.Click += OnBrowse;

        _btnOK = new Button {
            Text="Install", Size=new Size(90,30), Location=new Point(294,196),
            FlatStyle=FlatStyle.Flat, ForeColor=Color.White,
            BackColor=Color.FromArgb(41,121,255), Enabled=false,
        };
        _btnOK.FlatAppearance.BorderSize=0;
        _btnOK.Click += delegate { DialogResult=DialogResult.OK; Close(); };

        _btnCancel = new Button {
            Text="Cancel", Size=new Size(90,30), Location=new Point(394,196),
            FlatStyle=FlatStyle.Flat, ForeColor=Color.FromArgb(155,155,170),
            BackColor=Color.FromArgb(38,38,50),
        };
        _btnCancel.FlatAppearance.BorderColor=Color.FromArgb(58,58,72);
        _btnCancel.Click += delegate { DialogResult=DialogResult.Cancel; Close(); };

        Controls.AddRange(new Control[]{ lblTitle,lblInfo,lblPT,_lblPath,_btnBrowse,_btnOK,_btnCancel });
    }

    void OnBrowse(object sender, EventArgs e)
    {
        using (var dlg = new FolderBrowserDialog {
            Description="Choose install parent folder:", ShowNewFolderButton=true })
        {
            if (dlg.ShowDialog() != DialogResult.OK) return;
            ChosenDir           = Path.Combine(dlg.SelectedPath, "CursorSwitcher");
            _lblPath.Text       = ChosenDir;
            _lblPath.ForeColor  = Color.FromArgb(200,200,215);
            _btnOK.Enabled      = true;
        }
    }
}

// ──────────────────────────────────────────────────────────────────
// EXTRACT DIALOG
// ──────────────────────────────────────────────────────────────────
class ExtractDialog : Form
{
    public bool   Success { get; private set; }
    public string Error   { get; private set; }
    readonly string _dest;
    ProgressBar _bar;
    Label       _lbl;

    public ExtractDialog(string dest)
    {
        _dest = dest;
        Text="Extracting cursor files..."; Size=new Size(420,130);
        FormBorderStyle=FormBorderStyle.FixedDialog;
        StartPosition=FormStartPosition.CenterScreen;
        ControlBox=false; BackColor=Color.FromArgb(22,22,30);

        _lbl = new Label {
            Text="Extracting cursor files, please wait...",
            ForeColor=Color.FromArgb(175,175,190), Font=new Font("Segoe UI",9f),
            AutoSize=true, Location=new Point(20,18),
        };
        _bar = new ProgressBar {
            Style=ProgressBarStyle.Marquee, MarqueeAnimationSpeed=30,
            Size=new Size(378,16), Location=new Point(20,50),
        };
        Controls.Add(_lbl); Controls.Add(_bar);

        Load += delegate {
            var t = new System.Threading.Thread(ExtractThread);
            t.IsBackground = true; t.Start();
        };
    }

    void ExtractThread()
    {
        try {
            Directory.CreateDirectory(_dest);
            using (Stream stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("CursorSwitcher.Cursors"))
            {
                if (stream == null) throw new Exception("Cursor resource not found in executable.");
                using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Read))
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue;
                        string rel  = entry.FullName;
                        if (rel.StartsWith("Cursor/")) rel = rel.Substring(7);
                        string dest = Path.Combine(_dest, rel.Replace('/', Path.DirectorySeparatorChar));
                        string dir  = Path.GetDirectoryName(dest);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        using (Stream src = entry.Open())
                        using (FileStream dst = File.Create(dest))
                            src.CopyTo(dst);
                    }
            }
            Success = true;
        } catch (Exception ex) { Error = ex.Message; }
        if (IsHandleCreated)
            Invoke(new Action(delegate { Close(); }));
    }
}

} // namespace CursorSwitcher
