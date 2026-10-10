// Banana Bunch: a display font where every stroke of every letter is one banana.
// H is three bananas, T is two, O is two bananas meeting at the stem.
//
// Writes, into the output folder:
//   BananaBunch.ttf  - TrueType font. Colour bananas via COLR/CPAL layers, with plain
//                      single-colour outlines for apps that don't support colour fonts.
//   glyphs.json      - the same banana shapes, for drawing the letters on a web page.
//   font-test.html   - a specimen page that loads the .ttf, for checking it in a browser.
//
// Run with the .NET 10 SDK:  dotnet run BuildBananaBunch.cs -- <outputFolder>
// (build.ps1 in the repository root runs this and rebuilds index.html.)

using System.Globalization;
using System.Text;

string outDir = args.Length > 0 ? args[0] : ".";
Directory.CreateDirectory(outDir);
var glyphs = GlyphSet.Build();
byte[] ttf = FontWriter.Build(glyphs);
File.WriteAllBytes(Path.Combine(outDir, "BananaBunch.ttf"), ttf);
File.WriteAllText(Path.Combine(outDir, "glyphs.json"), JsonExport.Build(glyphs));
File.WriteAllText(Path.Combine(outDir, "font-test.html"), TestPage.Build(ttf));
Console.WriteLine($"{glyphs.Count} glyphs, {glyphs.Sum(g => g.Bananas.Count)} bananas, {ttf.Length:N0} bytes");

readonly struct V
{
    public readonly double X, Y;
    public V(double x, double y) { X = x; Y = y; }
    public static V operator +(V a, V b) => new(a.X + b.X, a.Y + b.Y);
    public static V operator -(V a, V b) => new(a.X - b.X, a.Y - b.Y);
    public static V operator *(V a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public V Unit => this * (1 / Length);
    public V Left => new(-Y, X);
}

// ---------- Stroke centrelines (font units, y up, baseline 0, cap height 700) ----------

abstract class Curve { public abstract V At(double t); }

sealed class Quad(V a, V c, V b) : Curve
{
    public override V At(double t) { double u = 1 - t; return a * (u * u) + c * (2 * u * t) + b * (t * t); }
}

sealed class Cubic(V a, V c1, V c2, V b) : Curve
{
    public override V At(double t)
    {
        double u = 1 - t;
        return a * (u * u * u) + c1 * (3 * u * u * t) + c2 * (3 * u * t * t) + b * (t * t * t);
    }
}

sealed class Arc(double cx, double cy, double rx, double ry, double deg0, double deg1) : Curve
{
    public override V At(double t)
    {
        double a = (deg0 + (deg1 - deg0) * t) * Math.PI / 180;
        return new V(cx + rx * Math.Cos(a), cy + ry * Math.Sin(a));
    }
}

// ---------- One banana laid along a stroke: stem at the start, dark tip at the end ----------

sealed class Banana
{
    public const double MaxWidth = 118, Edge = 10;
    const double SNeck = 0.07, SStem = 0.13;

    readonly V[] pts;
    readonly double[] cum;
    readonly double len, width, stemWidth, convex;

    public List<V> Stem, Body, Shade, Shine, Tip, EdgeStem, EdgeBody;

    public Banana(Curve curve)
    {
        const int M = 800;
        pts = new V[M + 1];
        cum = new double[M + 1];
        for (int i = 0; i <= M; i++)
        {
            pts[i] = curve.At(i / (double)M);
            if (i > 0) cum[i] = cum[i - 1] + (pts[i] - pts[i - 1]).Length;
        }
        len = cum[M];
        width = Math.Min(MaxWidth, 0.2 * len + 40);
        stemWidth = Math.Max(24, 0.3 * width);
        V p0 = Pos(0), chord = Pos(1) - p0, bulge = Pos(0.5) - p0;
        convex = chord.X * bulge.Y - chord.Y * bulge.X >= 0 ? 1 : -1; // +1: the banana bows to its left

        Stem = StemOutline(0);
        Body = BodyOutline(0, 1, 22, 0);
        Tip = BodyOutline(0.955, 1, 6, 0);
        Shade = Band(0.06, 0.95, 16,
            u => convex * W(u) * 0.485,
            u => convex * (W(u) / 2 - 0.3 * W(u) * Math.Pow(Env(u, 0.06, 0.95), 0.6)));
        Shine = Band(0.16, 0.84, 10,
            u => -convex * W(u) * (0.17 - 0.06 * Env(u, 0.16, 0.84)),
            u => -convex * W(u) * (0.17 + 0.06 * Env(u, 0.16, 0.84)));
        EdgeStem = StemOutline(Edge);
        EdgeBody = BodyOutline(0, 1, 22, Edge);
    }

    public IEnumerable<List<V>> Outlines => new[] { Stem, Body, Shade, Shine, Tip, EdgeStem, EdgeBody };

    public void Shift(double dx)
    {
        foreach (var poly in Outlines)
            for (int i = 0; i < poly.Count; i++) poly[i] = new V(poly[i].X + dx, poly[i].Y);
    }

    static double Env(double u, double a, double b) => Math.Sin(Math.PI * Math.Clamp((u - a) / (b - a), 0, 1));
    static double SOf(double u) => SNeck + u * (1 - SNeck);
    static double Spaced(double u0, double u1, double q) => u0 + (u1 - u0) * (0.5 - 0.5 * Math.Cos(Math.PI * q));

    // Body thickness along the banana: full in the middle, rounded nose, blunt neck at the stem.
    double W(double u)
    {
        double w = width * Math.Pow(Math.Max(0, Math.Sin(Math.PI * u)), 0.55);
        return u < 0.3 ? Math.Max(w, 1.1 * stemWidth) : w;
    }

    V Pos(double s)
    {
        double d = Math.Clamp(s, 0, 1) * len;
        int lo = 0, hi = cum.Length - 1;
        while (hi - lo > 1) { int mid = (lo + hi) / 2; if (cum[mid] <= d) lo = mid; else hi = mid; }
        double seg = cum[hi] - cum[lo], f = seg > 0 ? (d - cum[lo]) / seg : 0;
        return pts[lo] + (pts[hi] - pts[lo]) * f;
    }

    V Tan(double s) => (Pos(Math.Min(1, s + 0.004)) - Pos(Math.Max(0, s - 0.004))).Unit;

    List<V> BodyOutline(double u0, double u1, int n, double grow)
    {
        var left = new List<V>();
        var right = new List<V>();
        for (int k = 0; k <= n; k++)
        {
            double u = Spaced(u0, u1, k / (double)n), s = SOf(u);
            V c = Pos(s), t = Tan(s), nrm = t.Left;
            if (k == n && u1 >= 1) { left.Add(c + t * grow); break; }
            double h = W(u) / 2 + grow;
            left.Add(c + nrm * h);
            right.Add(c - nrm * h);
        }
        right.Reverse();
        left.AddRange(right);
        return left;
    }

    List<V> StemOutline(double grow)
    {
        var left = new List<V>();
        var right = new List<V>();
        const int n = 6;
        for (int k = 0; k <= n; k++)
        {
            double s = SStem * k / n;
            V c = Pos(s), t = Tan(s), nrm = t.Left;
            if (k == 0) c = c - t * grow;
            double h = stemWidth * (1 + 0.35 * Math.Max(0, 1 - s / 0.035)) / 2 + grow;
            left.Add(c + nrm * h);
            right.Add(c - nrm * h);
        }
        right.Reverse();
        left.AddRange(right);
        return left;
    }

    List<V> Band(double u0, double u1, int n, Func<double, double> offA, Func<double, double> offB)
    {
        var a = new List<V>();
        var b = new List<V>();
        for (int k = 0; k <= n; k++)
        {
            double u = Spaced(u0, u1, k / (double)n), s = SOf(u);
            V c = Pos(s), nrm = Tan(s).Left;
            a.Add(c + nrm * offA(u));
            b.Add(c + nrm * offB(u));
        }
        b.Reverse();
        a.AddRange(b);
        return a;
    }
}

sealed class Glyph
{
    public string Name = "";
    public int[] Codepoints = [];
    public List<Banana> Bananas = new();
    public int Advance;
}

// ---------- The alphabet: one curve per stroke, stem first ----------

static class GlyphSet
{
    const double Side = 40;

    static Curve Q(double x0, double y0, double cx, double cy, double x1, double y1) =>
        new Quad(new V(x0, y0), new V(cx, cy), new V(x1, y1));
    static Curve C(double x0, double y0, double c1x, double c1y, double c2x, double c2y, double x1, double y1) =>
        new Cubic(new V(x0, y0), new V(c1x, c1y), new V(c2x, c2y), new V(x1, y1));
    static Curve A(double cx, double cy, double rx, double ry, double deg0, double deg1) =>
        new Arc(cx, cy, rx, ry, deg0, deg1);

    // Shared strokes
    static Curve Spine() => Q(0, 700, -50, 350, 0, 0);
    static Curve Dot(double x, double y) => Q(x - 60, y + 26, x, y, x + 60, y + 26);

    public static List<Glyph> Build()
    {
        var list = new List<Glyph>();

        void G(string name, string chars, params Curve[] strokes)
        {
            var g = new Glyph { Name = name, Codepoints = chars.Select(c => (int)c).ToArray() };
            foreach (var s in strokes) g.Bananas.Add(new Banana(s));
            var edge = g.Bananas.SelectMany(b => b.EdgeBody.Concat(b.EdgeStem)).ToList();
            double xMin = edge.Min(p => p.X), xMax = edge.Max(p => p.X);
            foreach (var b in g.Bananas) b.Shift(Side - xMin);
            g.Advance = (int)Math.Round(xMax - xMin + 2 * Side);
            list.Add(g);
        }

        list.Add(new Glyph { Name = " ", Codepoints = [' ', 0xA0], Advance = 300 });

        G("A", "Aa", Q(240, 700, 63, 369, 0, 0), Q(270, 700, 447, 369, 510, 0), Q(80, 235, 255, 205, 430, 235));
        G("B", "Bb", Spine(), A(150, 520, 230, 121, 140, -140), A(170, 200, 260, 141, 140, -140));
        G("C", "Cc", A(270, 350, 270, 291, 45, 315));
        G("D", "Dd", Spine(), A(90, 350, 330, 291, 105, -105));
        G("E", "Ee", Spine(), Q(10, 641, 200, 615, 390, 641), Q(10, 350, 170, 326, 330, 350), Q(10, 59, 210, 33, 410, 59));
        G("F", "Ff", Spine(), Q(10, 641, 200, 615, 390, 641), Q(10, 350, 160, 326, 310, 350));
        G("G", "Gg", A(270, 350, 270, 291, 50, 340), Q(270, 280, 395, 262, 520, 280));
        G("H", "Hh", Q(0, 700, -60, 350, 0, 0), Q(400, 700, 460, 350, 400, 0), Q(-5, 350, 200, 322, 405, 350));
        G("I", "Ii", Q(0, 700, 40, 350, 0, 0));
        G("J", "Jj", C(400, 700, 410, 120, 300, -60, 30, 140));
        G("K", "Kk", Spine(), Q(420, 700, 195, 545, 20, 330), Q(150, 420, 320, 230, 440, 0));
        G("L", "Ll", Spine(), Q(10, 59, 200, 33, 390, 59));
        G("M", "Mm", Q(0, 700, -40, 350, 0, 0), Q(560, 700, 600, 350, 560, 0), Q(20, 700, 130, 430, 280, 190), Q(540, 700, 430, 430, 280, 190));
        G("N", "Nn", Q(0, 700, -40, 350, 0, 0), Q(460, 700, 500, 350, 460, 0), Q(20, 700, 250, 370, 440, 0));
        G("O", "Oo", A(270, 350, 270, 291, 95, 265), A(270, 350, 270, 291, 85, -85));
        G("P", "Pp", Spine(), A(150, 490, 250, 151, 140, -140));
        G("Q", "Qq", A(270, 350, 270, 291, 95, 265), A(270, 350, 270, 291, 85, -85), Q(330, 170, 420, 80, 560, -40));
        G("R", "Rr", Spine(), A(150, 490, 250, 151, 140, -140), Q(170, 330, 345, 190, 460, 0));
        G("S", "Ss", A(240, 520, 220, 121, 25, 290), A(250, 195, 245, 136, 110, -155));
        G("T", "Tt", Q(0, 641, 230, 605, 460, 641), Q(230, 630, 260, 310, 230, 0));
        G("U", "Uu", A(240, 700, 240, 641, 180, 360));
        G("V", "Vv", Q(0, 700, 58, 331, 230, 0), Q(470, 700, 417, 331, 250, 0));
        G("W", "Ww", Q(0, 700, 40, 340, 165, 0), Q(340, 600, 240, 300, 180, 0), Q(360, 600, 465, 300, 530, 0), Q(700, 700, 665, 340, 545, 0));
        G("X", "Xx", Q(0, 700, 250, 380, 460, 0), Q(460, 700, 210, 380, 0, 0));
        G("Y", "Yy", Q(0, 700, 95, 480, 240, 330), Q(480, 700, 385, 480, 240, 330), Q(240, 360, 270, 180, 240, 0));
        G("Z", "Zz", Q(0, 641, 220, 615, 440, 641), Q(430, 620, 250, 330, 20, 80), Q(10, 59, 230, 33, 450, 59));

        G("0", "0", A(220, 350, 220, 291, 95, 265), A(220, 350, 220, 291, 85, -85));
        G("1", "1", Q(200, 700, 240, 350, 200, 0), Q(185, 690, 90, 650, 20, 560));
        G("2", "2", A(220, 500, 210, 141, 165, -30), Q(400, 440, 240, 230, 10, 70), Q(10, 59, 220, 33, 440, 59));
        G("3", "3", A(200, 520, 210, 121, 150, -90), A(200, 200, 240, 141, 90, -150));
        G("4", "4", Q(330, 690, 170, 470, 0, 230), Q(0, 230, 230, 205, 460, 230), Q(350, 700, 380, 350, 350, 0));
        G("5", "5", Q(50, 641, 240, 615, 430, 641), Q(40, 650, 20, 510, 40, 370), A(190, 220, 240, 161, 120, -150));
        G("6", "6", A(250, 350, 250, 291, 60, 250), A(240, 210, 190, 151, -100, 170));
        G("7", "7", Q(0, 641, 220, 615, 440, 641), Q(430, 630, 300, 330, 130, 0));
        G("8", "8", A(230, 530, 170, 111, 95, 265), A(230, 530, 170, 111, 85, -85), A(230, 200, 210, 141, 95, 265), A(230, 200, 210, 141, 85, -85));
        G("9", "9", A(230, 350, 250, 291, 240, 430), A(240, 490, 190, 151, 80, 350));

        G("!", "!", Q(0, 700, 35, 470, 0, 230), Dot(0, 25));
        G("?", "?", A(200, 520, 200, 121, 160, -80), Q(235, 400, 255, 320, 225, 230), Dot(225, 25));
        G(".", ".", Dot(0, 25));
        G(",", ",", Q(40, 100, 45, 0, -30, -90));
        G("'", "'’", Q(0, 700, 15, 620, 0, 520));
        G("-", "-‐–", Q(0, 300, 120, 280, 240, 300));
        G(":", ":", Dot(0, 400), Dot(0, 25));
        G("+", "+", Q(150, 470, 170, 290, 150, 110), Q(-10, 290, 150, 270, 310, 290));
        G("/", "/", Q(380, 720, 180, 370, 0, -20));

        return list;
    }
}

// ---------- TrueType writer ----------

sealed class BE
{
    readonly List<byte> b = new();
    public int Length => b.Count;
    public BE U8(int v) { b.Add((byte)v); return this; }
    public BE U16(int v) { b.Add((byte)(v >> 8)); b.Add((byte)v); return this; }
    public BE I16(int v) => U16(v & 0xFFFF);
    public BE U32(uint v) { b.Add((byte)(v >> 24)); b.Add((byte)(v >> 16)); b.Add((byte)(v >> 8)); b.Add((byte)v); return this; }
    public BE I64(long v) { U32((uint)(v >> 32)); U32((uint)v); return this; }
    public BE Tag(string t) { foreach (char c in t) b.Add((byte)c); return this; }
    public BE Bytes(byte[] d) { b.AddRange(d); return this; }
    public BE Pad4() { while (b.Count % 4 != 0) b.Add(0); return this; }
    public byte[] ToArray() => b.ToArray();
}

static class FontWriter
{
    // Layer colours, drawn in this order for every banana: outline, stem, peel, shade, shine, tip.
    public static readonly string[] Palette = { "#7A4E0B", "#6B5A22", "#FFD43B", "#EAAA1E", "#FFF3B5", "#3A2410" };

    readonly record struct Pt(int X, int Y, bool On);

    sealed class OutGlyph
    {
        public List<List<Pt>> Contours = new();
        public int Advance, XMin, YMin, XMax, YMax, Points;
        public byte[] Data = [];
    }

    static int R(double v) => (int)Math.Round(v);

    // Polygon samples -> smooth quadratic contour: samples become off-curve points,
    // with explicit on-curve points at the midpoints between them. Clockwise, as TrueType expects.
    static List<Pt>? ToContour(List<V> poly)
    {
        var p = new List<V>();
        foreach (var v in poly) if (p.Count == 0 || (v - p[^1]).Length > 0.75) p.Add(v);
        while (p.Count > 2 && (p[0] - p[^1]).Length <= 0.75) p.RemoveAt(p.Count - 1);
        if (p.Count < 3) return null;
        double area = 0;
        for (int i = 0; i < p.Count; i++) { V a = p[i], b = p[(i + 1) % p.Count]; area += a.X * b.Y - b.X * a.Y; }
        if (Math.Abs(area) < 1) return null;
        if (area > 0) p.Reverse();
        var o = new List<Pt>();
        for (int i = 0; i < p.Count; i++)
        {
            V a = p[i], b = p[(i + 1) % p.Count];
            o.Add(new Pt(R((a.X + b.X) / 2), R((a.Y + b.Y) / 2), true));
            o.Add(new Pt(R(b.X), R(b.Y), false));
        }
        return o;
    }

    static void Add(OutGlyph g, List<V> poly)
    {
        var c = ToContour(poly);
        if (c != null) g.Contours.Add(c);
    }

    static void Encode(OutGlyph g)
    {
        var pts = g.Contours.SelectMany(c => c).ToList();
        if (pts.Count == 0) return;
        g.XMin = pts.Min(p => p.X); g.YMin = pts.Min(p => p.Y);
        g.XMax = pts.Max(p => p.X); g.YMax = pts.Max(p => p.Y);
        g.Points = pts.Count;
        var w = new BE();
        w.I16(g.Contours.Count).I16(g.XMin).I16(g.YMin).I16(g.XMax).I16(g.YMax);
        int end = -1;
        foreach (var c in g.Contours) { end += c.Count; w.U16(end); }
        w.U16(0); // no hinting instructions
        var flags = new List<byte>();
        var xs = new BE();
        var ys = new BE();
        int px = 0, py = 0;
        foreach (var p in pts)
        {
            int dx = p.X - px, dy = p.Y - py;
            px = p.X; py = p.Y;
            byte f = (byte)(p.On ? 1 : 0);
            if (dx == 0) f |= 0x10;
            else if (Math.Abs(dx) < 256) { f |= 0x02; if (dx > 0) f |= 0x10; xs.U8(Math.Abs(dx)); }
            else xs.I16(dx);
            if (dy == 0) f |= 0x20;
            else if (Math.Abs(dy) < 256) { f |= 0x04; if (dy > 0) f |= 0x20; ys.U8(Math.Abs(dy)); }
            else ys.I16(dy);
            flags.Add(f);
        }
        w.Bytes(flags.ToArray()).Bytes(xs.ToArray()).Bytes(ys.ToArray()).Pad4();
        g.Data = w.ToArray();
    }

    static uint Checksum(byte[] d)
    {
        uint sum = 0;
        for (int i = 0; i < d.Length; i += 4)
        {
            uint v = 0;
            for (int k = 0; k < 4; k++) v = (v << 8) | (i + k < d.Length ? d[i + k] : (byte)0);
            sum += v;
        }
        return sum;
    }

    public static byte[] Build(List<Glyph> glyphs)
    {
        var order = new List<OutGlyph>();
        var notdef = new OutGlyph { Advance = 500 };
        notdef.Contours.Add(new() { new(50, 0, true), new(50, 700, true), new(450, 700, true), new(450, 0, true) });
        notdef.Contours.Add(new() { new(100, 50, true), new(400, 50, true), new(400, 650, true), new(100, 650, true) });
        order.Add(notdef);

        // Base glyphs: plain outlines (stem + peel of every banana) for apps without colour font support.
        var gid = new Dictionary<Glyph, int>();
        foreach (var g in glyphs)
        {
            gid[g] = order.Count;
            var og = new OutGlyph { Advance = g.Advance };
            foreach (var b in g.Bananas) { Add(og, b.Stem); Add(og, b.Body); }
            order.Add(og);
        }

        // Colour layers (COLR v0): six layer glyphs per banana.
        var baseRecs = new List<(int Gid, int First, int Count)>();
        var layers = new List<(int Gid, int Pal)>();
        foreach (var g in glyphs)
        {
            if (g.Bananas.Count == 0) continue;
            int first = layers.Count;
            foreach (var b in g.Bananas)
            {
                var parts = new[] { new[] { b.EdgeStem, b.EdgeBody }, new[] { b.Stem }, new[] { b.Body }, new[] { b.Shade }, new[] { b.Shine }, new[] { b.Tip } };
                for (int p = 0; p < parts.Length; p++)
                {
                    var og = new OutGlyph { Advance = g.Advance };
                    foreach (var poly in parts[p]) Add(og, poly);
                    layers.Add((order.Count, p));
                    order.Add(og);
                }
            }
            baseRecs.Add((gid[g], first, layers.Count - first));
        }

        foreach (var og in order) Encode(og);
        int numGlyphs = order.Count;
        var inked = order.Where(o => o.Points > 0).ToList();
        int xMin = inked.Min(o => o.XMin), yMin = inked.Min(o => o.YMin), xMax = inked.Max(o => o.XMax), yMax = inked.Max(o => o.YMax);
        int winAscent = Math.Max(yMax, 850), winDescent = Math.Max(-yMin, 200);

        // glyf + loca
        var glyf = new BE();
        var loca = new BE();
        foreach (var og in order) { loca.U32((uint)glyf.Length); glyf.Bytes(og.Data); }
        loca.U32((uint)glyf.Length);

        // hmtx + hhea numbers
        var hmtx = new BE();
        foreach (var og in order) hmtx.U16(og.Advance).I16(og.Points > 0 ? og.XMin : 0);
        int maxAdv = order.Max(o => o.Advance);
        int minLsb = inked.Min(o => o.XMin);
        int minRsb = inked.Min(o => o.Advance - o.XMax);
        int xMaxExtent = inked.Max(o => o.XMax);

        var hhea = new BE();
        hhea.U32(0x00010000).I16(winAscent).I16(-winDescent).I16(0).U16(maxAdv).I16(minLsb).I16(minRsb).I16(xMaxExtent)
            .I16(1).I16(0).I16(0).I16(0).I16(0).I16(0).I16(0).I16(0).U16(numGlyphs);

        var maxp = new BE();
        maxp.U32(0x00010000).U16(numGlyphs).U16(order.Max(o => o.Points)).U16(order.Max(o => o.Contours.Count))
            .U16(0).U16(0).U16(2).U16(0).U16(0).U16(0).U16(0).U16(0).U16(0).U16(0).U16(0);

        long date = (long)(new DateTime(2026, 10, 10) - new DateTime(1904, 1, 1)).TotalSeconds;
        var head = new BE();
        head.U32(0x00010000).U32(0x00010000).U32(0).U32(0x5F0F3CF5).U16(0x000B).U16(1000).I64(date).I64(date)
            .I16(xMin).I16(yMin).I16(xMax).I16(yMax).U16(0).U16(8).I16(2).I16(1).I16(0);

        // cmap: format 4, one segment per character
        var map = new SortedDictionary<int, int>();
        foreach (var g in glyphs) foreach (int cp in g.Codepoints) map[cp] = gid[g];
        var segs = map.Select(kv => (Start: kv.Key, Delta: (kv.Value - kv.Key) & 0xFFFF)).ToList();
        segs.Add((0xFFFF, 1));
        int segCount = segs.Count, pow = 1, log = 0;
        while (pow * 2 <= segCount) { pow *= 2; log++; }
        var sub = new BE();
        sub.U16(4).U16(16 + 8 * segCount).U16(0).U16(segCount * 2).U16(pow * 2).U16(log).U16(segCount * 2 - pow * 2);
        foreach (var s in segs) sub.U16(s.Start);
        sub.U16(0);
        foreach (var s in segs) sub.U16(s.Start);
        foreach (var s in segs) sub.U16(s.Delta);
        foreach (var s in segs) sub.U16(0);
        var cmap = new BE();
        cmap.U16(0).U16(2).U16(0).U16(3).U32(20).U16(3).U16(1).U32(20).Bytes(sub.ToArray());

        var os2 = new BE();
        os2.U16(4).I16((int)order.Where(o => o.Advance > 0).Average(o => o.Advance)).U16(400).U16(5).U16(0)
            .I16(650).I16(600).I16(0).I16(75)
            .I16(650).I16(600).I16(0).I16(350)
            .I16(50).I16(300).I16(0)
            .Bytes(new byte[10])
            .U32(1u | (1u << 31)).U32(0).U32(0).U32(0)
            .Tag("BNNA")
            .U16(0x00C0).U16(map.Keys.Min()).U16(map.Keys.Max())
            .I16(800).I16(-200).I16(200)
            .U16(winAscent).U16(winDescent)
            .U32(1).U32(0)
            .I16(700).I16(700).U16(0).U16(32).U16(0);

        var names = new (int Id, string Text)[]
        {
            (0, "Banana Bunch, October 2026. Free to use."),
            (1, "Banana Bunch"),
            (2, "Regular"),
            (3, "BananaBunch-Regular;1.000;2026"),
            (4, "Banana Bunch"),
            (5, "Version 1.000"),
            (6, "BananaBunch-Regular"),
        };
        var strings = new BE();
        var name = new BE();
        name.U16(0).U16(names.Length).U16(6 + 12 * names.Length);
        foreach (var (id, text) in names)
        {
            byte[] bytes = Encoding.BigEndianUnicode.GetBytes(text);
            name.U16(3).U16(1).U16(0x409).U16(id).U16(bytes.Length).U16(strings.Length);
            strings.Bytes(bytes);
        }
        name.Bytes(strings.ToArray());

        var post = new BE();
        post.U32(0x00030000).U32(0).I16(-100).I16(50).U32(0).U32(0).U32(0).U32(0).U32(0);

        var colr = new BE();
        colr.U16(0).U16(baseRecs.Count).U32(14).U32((uint)(14 + 6 * baseRecs.Count)).U16(layers.Count);
        foreach (var r in baseRecs.OrderBy(r => r.Gid)) colr.U16(r.Gid).U16(r.First).U16(r.Count);
        foreach (var l in layers) colr.U16(l.Gid).U16(l.Pal);

        var cpal = new BE();
        cpal.U16(0).U16(Palette.Length).U16(1).U16(Palette.Length).U32(14).U16(0);
        foreach (string hex in Palette)
        {
            int rgb = int.Parse(hex.AsSpan(1), NumberStyles.HexNumber);
            cpal.U8(rgb & 0xFF).U8((rgb >> 8) & 0xFF).U8((rgb >> 16) & 0xFF).U8(0xFF);
        }

        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["COLR"] = colr.ToArray(), ["CPAL"] = cpal.ToArray(), ["OS/2"] = os2.ToArray(), ["cmap"] = cmap.ToArray(),
            ["glyf"] = glyf.ToArray(), ["head"] = head.ToArray(), ["hhea"] = hhea.ToArray(), ["hmtx"] = hmtx.ToArray(),
            ["loca"] = loca.ToArray(), ["maxp"] = maxp.ToArray(), ["name"] = name.ToArray(), ["post"] = post.ToArray(),
        };
        int n = tables.Count, tp = 1, tl = 0;
        while (tp * 2 <= n) { tp *= 2; tl++; }
        var dir = new BE();
        dir.U32(0x00010000).U16(n).U16(tp * 16).U16(tl).U16(n * 16 - tp * 16);
        var data = new BE();
        int headOffset = 0;
        foreach (var (tag, bytes) in tables)
        {
            int offset = 12 + 16 * n + data.Length;
            if (tag == "head") headOffset = offset;
            dir.Tag(tag).U32(Checksum(bytes)).U32((uint)offset).U32((uint)bytes.Length);
            data.Bytes(bytes).Pad4();
        }
        byte[] font = dir.ToArray().Concat(data.ToArray()).ToArray();
        uint adjust = 0xB1B0AFBA - Checksum(font);
        font[headOffset + 8] = (byte)(adjust >> 24);
        font[headOffset + 9] = (byte)(adjust >> 16);
        font[headOffset + 10] = (byte)(adjust >> 8);
        font[headOffset + 11] = (byte)adjust;
        return font;
    }
}

// ---------- Web export ----------

static class JsonExport
{
    static string Arr(List<V> poly) =>
        "[" + string.Join(",", poly.SelectMany(p => new[] { (int)Math.Round(p.X), (int)Math.Round(p.Y) })) + "]";

    public static string Build(List<Glyph> glyphs)
    {
        var all = glyphs.SelectMany(g => g.Bananas).SelectMany(b => b.EdgeBody.Concat(b.EdgeStem)).ToList();
        var sb = new StringBuilder();
        sb.Append("{\"upm\":1000,\"cap\":700");
        sb.Append($",\"yMin\":{(int)Math.Floor(all.Min(p => p.Y))},\"yMax\":{(int)Math.Ceiling(all.Max(p => p.Y))}");
        sb.Append(",\"edge\":").Append((int)Banana.Edge);
        sb.Append(",\"colors\":[").Append(string.Join(",", FontWriter.Palette.Select(c => $"\"{c}\""))).Append(']');
        sb.Append(",\"glyphs\":{");
        sb.Append(string.Join(",", glyphs.Select(g =>
        {
            string key = g.Name == "\"" ? "\\\"" : g.Name;
            string bananas = string.Join(",", g.Bananas.Select(b =>
                $"{{\"s\":{Arr(b.Stem)},\"b\":{Arr(b.Body)},\"d\":{Arr(b.Shade)},\"h\":{Arr(b.Shine)},\"t\":{Arr(b.Tip)}}}"));
            return $"\"{key}\":{{\"a\":{g.Advance},\"b\":[{bananas}]}}";
        })));
        sb.Append("}}");
        return sb.ToString();
    }
}

static class TestPage
{
    public static string Build(byte[] ttf) => $$"""
        <!doctype html><meta charset="utf-8">
        <style>
        @font-face { font-family: "Banana Bunch"; src: url(data:font/ttf;base64,{{Convert.ToBase64String(ttf)}}) format("truetype"); }
        body { margin: 0; padding: 24px 32px; background: #fff; font-family: "Banana Bunch", monospace; }
        .big { font-size: 170px; line-height: 1.12; }
        .mid { font-size: 64px; line-height: 1.25; }
        .dark { background: #133821; padding: 8px 20px; }
        .mono { font-size: 48px; color: #222; font-palette: normal; }
        </style>
        <div class="big">ABCDEFGH<br>IJKLMNOP<br>QRSTUVWX<br>YZ 01234<br>56789 !?<br>.,'-:+/</div>
        <div class="mid dark">HAPPY BIRTHDAY ANNA BANANA!</div>
        """;
}
