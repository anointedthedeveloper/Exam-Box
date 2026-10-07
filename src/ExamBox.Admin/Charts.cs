using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ExamBox.Admin;

/// <summary>Tiny chart controls drawn directly (no chart library), crisp at any DPI.</summary>
internal static class ChartKit
{
    public static Brush Res(FrameworkElement owner, string key, Brush fallback) => owner.TryFindResource(key) as Brush ?? fallback;

    public static FormattedText Text(Visual owner, string text, double size, Brush brush, bool bold = false) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size, brush, VisualTreeHelper.GetDpi(owner).PixelsPerDip);

    public static Brush WithAlpha(Brush b, byte alpha)
    {
        if (b is SolidColorBrush s) { var f = new SolidColorBrush(Color.FromArgb(alpha, s.Color.R, s.Color.G, s.Color.B)); f.Freeze(); return f; }
        return b;
    }

    /// <summary>Smooth curve through the points (quadratic segments between midpoints).</summary>
    public static StreamGeometry Smooth(IList<Point> pts, bool closeToBottom, double bottom)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        if (pts.Count == 0) return g;
        c.BeginFigure(closeToBottom ? new Point(pts[0].X, bottom) : pts[0], closeToBottom, closeToBottom);
        if (closeToBottom) c.LineTo(pts[0], true, false);
        if (pts.Count == 1) { c.LineTo(pts[0], true, false); }
        else
        {
            for (var i = 0; i < pts.Count - 1; i++)
            {
                var mid = new Point((pts[i].X + pts[i + 1].X) / 2, (pts[i].Y + pts[i + 1].Y) / 2);
                if (i == 0) c.LineTo(pts[0], true, true);
                c.QuadraticBezierTo(pts[i], mid, true, true);
            }
            c.LineTo(pts[^1], true, true);
        }
        if (closeToBottom) { c.LineTo(new Point(pts[^1].X, bottom), true, false); }
        g.Freeze();
        return g;
    }

    public static (double Max, double Step) NiceScale(double max)
    {
        if (max <= 0) return (4, 1);
        double[] steps = { 1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 5000, 10000 };
        foreach (var s in steps)
            if (max <= s * 4) return (Math.Ceiling(max / s) * s, s);
        return (Math.Ceiling(max / 10000) * 10000, 10000);
    }
}

/// <summary>A small trend line with a soft area fill.</summary>
public sealed class Sparkline : FrameworkElement
{
    private double[] _values = Array.Empty<double>();
    public double[] Values { get => _values; set { _values = value ?? Array.Empty<double>(); InvalidateVisual(); } }
    public Brush? Stroke { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth; var h = ActualHeight;
        if (w < 4 || h < 4) return;
        var stroke = Stroke ?? ChartKit.Res(this, "BlueBrush", Brushes.RoyalBlue);
        var n = _values.Length;
        var pts = new List<Point>();
        if (n < 2) { pts.Add(new Point(0, h - 3)); pts.Add(new Point(w, h - 3)); }
        else
        {
            double min = _values.Min(), max = _values.Max(), span = max - min;
            for (var i = 0; i < n; i++)
            {
                var t = span < 1e-9 ? 0.5 : (_values[i] - min) / span;
                pts.Add(new Point(i * w / (n - 1), h - 3 - t * (h - 8)));
            }
        }
        var color = (stroke as SolidColorBrush)?.Color ?? Colors.RoyalBlue;
        var area = new LinearGradientBrush(Color.FromArgb(70, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B), 90);
        dc.DrawGeometry(area, null, ChartKit.Smooth(pts, true, h));
        dc.DrawGeometry(null, new Pen(stroke, 2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, ChartKit.Smooth(pts, false, h));
    }
}

/// <summary>Compact bar chart with month labels; the last bar is highlighted.</summary>
public sealed class MiniBars : FrameworkElement
{
    private double[] _values = Array.Empty<double>();
    private string[] _labels = Array.Empty<string>();
    public double[] Values { get => _values; set { _values = value ?? Array.Empty<double>(); InvalidateVisual(); } }
    public string[] Labels { get => _labels; set { _labels = value ?? Array.Empty<string>(); InvalidateVisual(); } }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 10 || h < 10 || _values.Length == 0) return;
        var blue = ChartKit.Res(this, "BlueBrush", Brushes.RoyalBlue);
        var light = ChartKit.Res(this, "BlueLightBrush", Brushes.LightBlue);
        var muted = ChartKit.Res(this, "MutedBrush", Brushes.Gray);
        var labelH = _labels.Length > 0 ? 13 : 0;
        var plotH = h - labelH - 2;
        var max = Math.Max(1, _values.Max());
        var n = _values.Length; var slot = w / n; var bw = Math.Min(slot * 0.6, 22);
        for (var i = 0; i < n; i++)
        {
            var bh = Math.Max(3, _values[i] / max * (plotH - 2));
            var x = i * slot + (slot - bw) / 2;
            var last = i == n - 1;
            dc.DrawRoundedRectangle(last ? blue : ChartKit.WithAlpha(blue, 90), null, new Rect(x, plotH - bh, bw, bh), 3, 3);
            if (i < _labels.Length)
            {
                var t = ChartKit.Text(this, _labels[i], 9, muted);
                dc.DrawText(t, new Point(i * slot + (slot - t.Width) / 2, h - t.Height));
            }
        }
    }
}

public sealed record DonutSlice(string Label, double Value, Brush Color);

/// <summary>Ring chart; hovering a slice highlights it and shows its share in the middle.</summary>
public sealed class DonutChart : FrameworkElement
{
    private List<DonutSlice> _slices = new();
    private int _hover = -1;
    public List<DonutSlice> Slices { get => _slices; set { _slices = value ?? new(); _hover = -1; InvalidateVisual(); } }
    public string CenterTitle { get; set; } = "";
    public string CenterSub { get; set; } = "";

    private (Point Center, double Radius, double Thickness) Geo()
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        return (new Point(ActualWidth / 2, ActualHeight / 2), size / 2 - 6, Math.Max(12, size * 0.2));
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        var (c, r, th) = Geo();
        if (r < 20) return;
        var total = _slices.Sum(s => s.Value);
        var ink = ChartKit.Res(this, "InkBrush", Brushes.Black);
        var muted = ChartKit.Res(this, "MutedBrush", Brushes.Gray);
        var line = ChartKit.Res(this, "LineBrush", Brushes.LightGray);
        var mid = r - th / 2;

        if (total <= 0)
        {
            dc.DrawEllipse(null, new Pen(line, th), c, mid, mid);
            var t = ChartKit.Text(this, "No data yet", 12, muted); dc.DrawText(t, new Point(c.X - t.Width / 2, c.Y - t.Height / 2));
            return;
        }

        var angle = -90.0;
        for (var i = 0; i < _slices.Count; i++)
        {
            var s = _slices[i];
            if (s.Value <= 0) continue;
            var sweep = s.Value / total * 360.0;
            var pen = new Pen(s.Color, i == _hover ? th + 6 : th) { LineJoin = PenLineJoin.Round };
            if (sweep >= 359.99) dc.DrawEllipse(null, pen, c, mid, mid);
            else
            {
                var gap = _slices.Count(x => x.Value > 0) > 1 && sweep > 4 ? 1.6 : 0;
                var a0 = (angle + gap / 2) * Math.PI / 180; var a1 = (angle + sweep - gap / 2) * Math.PI / 180;
                var p0 = new Point(c.X + mid * Math.Cos(a0), c.Y + mid * Math.Sin(a0));
                var p1 = new Point(c.X + mid * Math.Cos(a1), c.Y + mid * Math.Sin(a1));
                var g = new StreamGeometry();
                using (var ctx = g.Open()) { ctx.BeginFigure(p0, false, false); ctx.ArcTo(p1, new Size(mid, mid), 0, sweep > 180, SweepDirection.Clockwise, true, false); }
                g.Freeze();
                dc.DrawGeometry(null, pen, g);
            }
            angle += sweep;
        }

        string top = CenterTitle, sub = CenterSub;
        if (_hover >= 0 && _hover < _slices.Count)
        {
            var s = _slices[_hover];
            top = $"{Math.Round(s.Value / total * 100)}%"; sub = $"{s.Label} · {s.Value:0}";
        }
        var tt = ChartKit.Text(this, top, 22, ink, true); var ts = ChartKit.Text(this, sub, 11, muted);
        dc.DrawText(tt, new Point(c.X - tt.Width / 2, c.Y - tt.Height / 2 - 6));
        dc.DrawText(ts, new Point(c.X - ts.Width / 2, c.Y + tt.Height / 2 - 6));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var (c, r, th) = Geo();
        var p = e.GetPosition(this); var dx = p.X - c.X; var dy = p.Y - c.Y; var dist = Math.Sqrt(dx * dx + dy * dy);
        var idx = -1;
        if (dist >= r - th - 4 && dist <= r + 6)
        {
            var deg = (Math.Atan2(dy, dx) * 180 / Math.PI + 90 + 360) % 360;
            var total = _slices.Sum(s => s.Value); double acc = 0;
            for (var i = 0; i < _slices.Count && total > 0; i++)
            {
                acc += _slices[i].Value / total * 360;
                if (deg <= acc && _slices[i].Value > 0) { idx = i; break; }
            }
        }
        if (idx != _hover) { _hover = idx; InvalidateVisual(); }
    }

    protected override void OnMouseLeave(MouseEventArgs e) { if (_hover != -1) { _hover = -1; InvalidateVisual(); } }
}

public sealed record LineSeries(string Name, double[] Values, Brush Brush);

/// <summary>Multi-series line chart with grid, axis labels and a hover tooltip.</summary>
public sealed class LineChart : FrameworkElement
{
    private List<LineSeries> _series = new();
    private string[] _labels = Array.Empty<string>();
    private int _hover = -1;
    public List<LineSeries> Series { get => _series; set { _series = value ?? new(); InvalidateVisual(); } }
    public string[] Labels { get => _labels; set { _labels = value ?? Array.Empty<string>(); InvalidateVisual(); } }

    private const double L = 34, R = 12, T = 10, B = 24;

    private double X(int i, double w) => _labels.Length < 2 ? L + (w - L - R) / 2 : L + i * (w - L - R) / (_labels.Length - 1);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        if (w < 80 || h < 60 || _labels.Length == 0) return;
        var muted = ChartKit.Res(this, "MutedBrush", Brushes.Gray);
        var line = ChartKit.Res(this, "LineBrush", Brushes.LightGray);
        var ink = ChartKit.Res(this, "InkBrush", Brushes.Black);

        var dataMax = _series.Count == 0 ? 0 : _series.Max(s => s.Values.Length == 0 ? 0 : s.Values.Max());
        var (max, step) = ChartKit.NiceScale(dataMax);
        double Y(double v) => T + (1 - v / max) * (h - T - B);

        for (double v = 0; v <= max + 1e-9; v += step)
        {
            var y = Y(v);
            dc.DrawLine(new Pen(line, 1), new Point(L, y), new Point(w - R, y));
            var t = ChartKit.Text(this, v.ToString("0"), 10, muted);
            dc.DrawText(t, new Point(L - 8 - t.Width, y - t.Height / 2));
        }
        for (var i = 0; i < _labels.Length; i++)
        {
            var t = ChartKit.Text(this, _labels[i], 10, muted);
            dc.DrawText(t, new Point(X(i, w) - t.Width / 2, h - B + 6));
        }

        for (var si = 0; si < _series.Count; si++)
        {
            var s = _series[si];
            var pts = s.Values.Select((v, i) => new Point(X(i, w), Y(v))).ToList();
            if (pts.Count == 0) continue;
            if (si == 0)
            {
                var col = (s.Brush as SolidColorBrush)?.Color ?? Colors.RoyalBlue;
                var area = new LinearGradientBrush(Color.FromArgb(60, col.R, col.G, col.B), Color.FromArgb(0, col.R, col.G, col.B), 90);
                dc.DrawGeometry(area, null, ChartKit.Smooth(pts, true, Y(0)));
            }
            dc.DrawGeometry(null, new Pen(s.Brush, 2.5) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, ChartKit.Smooth(pts, false, Y(0)));
            foreach (var p in pts) dc.DrawEllipse(Brushes.White, new Pen(s.Brush, 2), p, 3.5, 3.5);
        }

        if (_hover >= 0 && _hover < _labels.Length)
        {
            var x = X(_hover, w);
            dc.DrawLine(new Pen(ChartKit.WithAlpha(ink, 60), 1) { DashStyle = DashStyles.Dash }, new Point(x, T), new Point(x, h - B));
            foreach (var s in _series.Where(s => _hover < s.Values.Length))
                dc.DrawEllipse(s.Brush, new Pen(Brushes.White, 2), new Point(x, Y(s.Values[_hover])), 5, 5);

            var lines = new List<(string Text, Brush Dot)> { (_labels[_hover], Brushes.Transparent) };
            lines.AddRange(_series.Where(s => _hover < s.Values.Length).Select(s => ($"{s.Name}: {s.Values[_hover]:0.##}", s.Brush)));
            var texts = lines.Select((l, i) => ChartKit.Text(this, l.Text, 11.5, i == 0 ? ink : ink, i == 0)).ToList();
            var bw = texts.Max(t => t.Width) + 32; var bh = texts.Sum(t => t.Height) + 16;
            var bx = Math.Min(Math.Max(x + 12, L), w - R - bw); if (x + 12 + bw > w - R) bx = x - 12 - bw;
            bx = Math.Max(L, bx);
            var by = T + 4;
            dc.DrawRoundedRectangle(Brushes.White, new Pen(line, 1), new Rect(bx, by, bw, bh), 8, 8);
            var yy = by + 8;
            for (var i = 0; i < texts.Count; i++)
            {
                if (i > 0) dc.DrawEllipse(lines[i].Dot, null, new Point(bx + 13, yy + texts[i].Height / 2), 4, 4);
                dc.DrawText(texts[i], new Point(bx + (i == 0 ? 12 : 24), yy));
                yy += texts[i].Height;
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_labels.Length == 0) return;
        var x = e.GetPosition(this).X; var best = -1; var bestD = double.MaxValue;
        for (var i = 0; i < _labels.Length; i++) { var d = Math.Abs(X(i, ActualWidth) - x); if (d < bestD) { bestD = d; best = i; } }
        if (best != _hover) { _hover = best; InvalidateVisual(); }
    }

    protected override void OnMouseLeave(MouseEventArgs e) { if (_hover != -1) { _hover = -1; InvalidateVisual(); } }
}

/// <summary>Colours shared by the charts, and the grade legend used on the dashboard and the Reports page.</summary>
internal static class Palette
{
    private static SolidColorBrush B(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    public static readonly Brush[] Grade = { B("#0B5FF0"), B("#19B58A"), B("#6AA9FF"), B("#7B61FF"), B("#F2A900") };
    public static readonly string[] GradeRange = { "90% +", "80–89%", "70–79%", "60–69%", "Below 60%" };
    private static readonly (Brush Bg, Brush Fg)[] Avatars =
    {
        (B("#E3EDFF"), B("#0B5FF0")), (B("#DDF6EC"), B("#0F9D58")), (B("#EBE6FF"), B("#6B4FE8")),
        (B("#FFF1D6"), B("#C77D00")), (B("#FFE4EA"), B("#D63C63")), (B("#DDF3F8"), B("#0E8AA3")),
    };
    /// <summary>A stable colour pair per name, so each student keeps the same avatar colour.</summary>
    public static (Brush Bg, Brush Fg) Avatar(string name)
    {
        var h = 17; foreach (var ch in name) h = h * 31 + ch;
        return Avatars[(h & 0x7fffffff) % Avatars.Length];
    }
    public static readonly Brush Passed = B("#19B58A");
    public static readonly Brush Failed = B("#E5484D");

    public static List<DonutSlice> GradeSlices(int[] counts) =>
        counts.Select((c, i) => new DonutSlice("Grade " + Services.ReportService.GradeLabels[i], c, Grade[i])).ToList();

    public static void BuildGradeLegend(System.Windows.Controls.Panel host, int[] counts, FrameworkElement owner)
    {
        host.Children.Clear();
        var total = Math.Max(1, counts.Sum());
        var ink = ChartKit.Res(owner, "InkBrush", Brushes.Black);
        var muted = ChartKit.Res(owner, "MutedBrush", Brushes.Gray);
        for (var i = 0; i < counts.Length; i++)
        {
            var row = new System.Windows.Controls.Grid { Margin = new Thickness(0, 0, 0, 9) };
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            var dot = new System.Windows.Shapes.Ellipse { Width = 10, Height = 10, Fill = Grade[i], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            var name = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            name.Children.Add(new System.Windows.Controls.TextBlock { Text = Services.ReportService.GradeLabels[i], FontWeight = FontWeights.SemiBold, Foreground = ink, Width = 16 });
            name.Children.Add(new System.Windows.Controls.TextBlock { Text = GradeRange[i], Foreground = muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            var val = new System.Windows.Controls.TextBlock { Text = $"{counts[i]}  ·  {Math.Round(counts[i] * 100.0 / total)}%", Foreground = ink, FontWeight = FontWeights.SemiBold, FontSize = 12.5, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            System.Windows.Controls.Grid.SetColumn(name, 1); System.Windows.Controls.Grid.SetColumn(val, 2);
            row.Children.Add(dot); row.Children.Add(name); row.Children.Add(val);
            host.Children.Add(row);
        }
    }
}
