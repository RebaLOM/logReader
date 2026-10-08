using System.Collections.Concurrent;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace logReader.UI.Icons;

/// <summary>Lucide vector geometry, rendered with GDI+ at the control's actual DPI.</summary>
/// <remarks>
/// Source: https://github.com/lucide-icons/lucide/tree/b56741cf30c08fc7248bf41ddd3a261367aa9579/icons
/// Copyright (c) 2026 Lucide Icons and Contributors. ISC; inherited Feather icons MIT.
/// Full notices are in THIRD-PARTY-NOTICES.md. No fonts, raster assets or UI packages required.
/// </remarks>
public static class IconPainter
{
    private static readonly ConcurrentDictionary<IconKind, GraphicsPath> Paths = new();

    public static void Draw(Graphics graphics, IconKind icon, Rectangle bounds, Color color, float strokeWidth = 2f)
    {
        if (icon == IconKind.None || bounds.Width <= 0 || bounds.Height <= 0) return;
        var state = graphics.Save();
        try
        {
            float size = Math.Min(bounds.Width, bounds.Height);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(bounds.X + (bounds.Width - size) / 2f, bounds.Y + (bounds.Height - size) / 2f);
            graphics.ScaleTransform(size / 24f, size / 24f);
            using var pen = new Pen(color, strokeWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            graphics.DrawPath(pen, Paths.GetOrAdd(icon, BuildPath));
        }
        finally { graphics.Restore(state); }
    }

    private static GraphicsPath BuildPath(IconKind kind)
    {
        var result = new GraphicsPath();
        var svg = XElement.Parse("<svg>" + Geometry(kind) + "</svg>");
        foreach (var element in svg.Elements())
        {
            result.StartFigure();
            float Number(string name, float fallback = 0) =>
                float.TryParse((string?)element.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
            switch (element.Name.LocalName)
            {
                case "path":
                    using (var path = SvgPath.Parse((string?)element.Attribute("d") ?? "")) result.AddPath(path, false);
                    break;
                case "circle":
                    float r = Number("r");
                    result.AddEllipse(Number("cx") - r, Number("cy") - r, r * 2, r * 2);
                    break;
                case "rect":
                    using (var path = PaintGeometry.Rounded(new RectangleF(Number("x"), Number("y"), Number("width"), Number("height")), Number("rx")))
                        result.AddPath(path, false);
                    break;
            }
        }
        return result;
    }

    // These are the upstream path/shape definitions; names map to application actions.
    private static string Geometry(IconKind kind) => kind switch
    {
        IconKind.Activity or IconKind.Logo => """
            <path d="M22 12h-2.48a2 2 0 0 0-1.93 1.46l-2.35 8.36a.25.25 0 0 1-.48 0L9.24 2.18a.25.25 0 0 0-.48 0l-2.35 8.36A2 2 0 0 1 4.49 12H2" />
            """,
        IconKind.File => """
            <path d="M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z" />
            <path d="M14 2v5a1 1 0 0 0 1 1h5" />
            """,
        IconKind.Folder => """
            <path d="M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z" />
            """,
        IconKind.Devices => """
            <path d="M12 20v2" /><path d="M12 2v2" /><path d="M17 20v2" /><path d="M17 2v2" />
            <path d="M2 12h2" /><path d="M2 17h2" /><path d="M2 7h2" /><path d="M20 12h2" /><path d="M20 17h2" /><path d="M20 7h2" />
            <path d="M7 20v2" /><path d="M7 2v2" /><rect x="4" y="4" width="16" height="16" rx="2" /><rect x="8" y="8" width="8" height="8" rx="1" />
            """,
        IconKind.Sliders => """
            <path d="M10 5H3" /><path d="M12 19H3" /><path d="M14 3v4" /><path d="M16 17v4" /><path d="M21 12h-9" />
            <path d="M21 19h-5" /><path d="M21 5h-7" /><path d="M8 10v4" /><path d="M8 12H3" />
            """,
        IconKind.Save => """
            <path d="M15.2 3a2 2 0 0 1 1.4.6l3.8 3.8a2 2 0 0 1 .6 1.4V19a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z" />
            <path d="M17 21v-7a1 1 0 0 0-1-1H8a1 1 0 0 0-1 1v7" /><path d="M7 3v4a1 1 0 0 0 1 1h7" />
            """,
        IconKind.Plus => """<path d="M5 12h14" /><path d="M12 5v14" />""",
        IconKind.Edit => """
            <path d="M12 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
            <path d="M18.375 2.625a1 1 0 0 1 3 3l-9.013 9.014a2 2 0 0 1-.853.505l-2.873.84a.5.5 0 0 1-.62-.62l.84-2.873a2 2 0 0 1 .506-.852z" />
            """,
        IconKind.Trash => """
            <path d="M3 6h18" /><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" />
            <path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" /><path d="M10 11v6" /><path d="M14 11v6" />
            """,
        IconKind.Search => """<path d="m21 21-4.34-4.34" /><circle cx="11" cy="11" r="8" />""",
        IconKind.Filter => """<path d="M2 5h20" /><path d="M6 12h12" /><path d="M9 19h6" />""",
        IconKind.Help => """<circle cx="12" cy="12" r="10" /><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" /><path d="M12 17h.01" />""",
        IconKind.ArrowRight => """<path d="M5 12h14" /><path d="m12 5 7 7-7 7" />""",
        IconKind.Play => """<path d="M5 5a2 2 0 0 1 3.008-1.728l11.997 6.998a2 2 0 0 1 .003 3.458l-12 7A2 2 0 0 1 5 19z" />""",
        IconKind.Check => """<path d="M20 6 9 17l-5-5" />""",
        IconKind.Warning => """<path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3" /><path d="M12 9v4" /><path d="M12 17h.01" />""",
        IconKind.Close or IconKind.Clear => """<path d="M18 6 6 18" /><path d="m6 6 12 12" />""",
        IconKind.Convert => """<path d="m16 3 4 4-4 4" /><path d="M20 7H4" /><path d="m8 21-4-4 4-4" /><path d="M4 17h16" />""",
        IconKind.List => """<path d="M3 5h.01" /><path d="M3 12h.01" /><path d="M3 19h.01" /><path d="M8 5h13" /><path d="M8 12h13" /><path d="M8 19h13" />""",
        IconKind.Copy => """<rect width="14" height="14" x="8" y="8" rx="2" ry="2" /><path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2" />""",
        IconKind.ChevronDown => """<path d="m6 9 6 6 6-6" />""",
        IconKind.Info => """<circle cx="12" cy="12" r="10" /><path d="M12 16v-4" /><path d="M12 8h.01" />""",
        IconKind.Settings => """
            <path d="M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915" />
            <circle cx="12" cy="12" r="3" />
            """,
        IconKind.Signal => """
            <path d="M16.247 7.761a6 6 0 0 1 0 8.478" /><path d="M19.075 4.933a10 10 0 0 1 0 14.134" />
            <path d="M4.925 19.067a10 10 0 0 1 0-14.134" /><path d="M7.753 16.239a6 6 0 0 1 0-8.478" /><circle cx="12" cy="12" r="2" />
            """,
        IconKind.Download => """<path d="M12 15V3" /><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" /><path d="m7 10 5 5 5-5" />""",
        IconKind.ExternalLink => """<path d="M15 3h6v6" /><path d="M10 14 21 3" /><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />""",
        IconKind.ChevronRight => """<path d="m9 18 6-6-6-6" />""",
        IconKind.ChevronLeft => """<path d="m15 18-6-6 6-6" />""",
        IconKind.ArrowUp => """<path d="m5 12 7-7 7 7" /><path d="M12 19V5" />""",
        IconKind.ArrowDown => """<path d="M12 5v14" /><path d="m19 12-7 7-7-7" />""",
        IconKind.Table => """<path d="M3 9h18" /><path d="M9 3v18" /><rect x="3" y="3" width="18" height="18" rx="2" />""",
        IconKind.Clock => """<circle cx="12" cy="12" r="10" /><path d="M12 6v6l4 2" />""",
        _ => ""
    };

    private static class SvgPath
    {
        private static readonly Regex Tokens = new("[a-zA-Z]|[-+]?(?:[0-9]*\\.[0-9]+|[0-9]+\\.?[0-9]*)(?:[eE][-+]?[0-9]+)?", RegexOptions.Compiled);
        public static GraphicsPath Parse(string data)
        {
            string[] tokens = Tokens.Matches(data).Select(m => m.Value).ToArray();
            var path = new GraphicsPath();
            PointF current = default, start = default;
            int i = 0;
            char command = 'M';
            float Next() => float.Parse(tokens[i++], CultureInfo.InvariantCulture);
            PointF Point(bool relative)
            {
                var p = new PointF(Next(), Next());
                return relative ? new PointF(p.X + current.X, p.Y + current.Y) : p;
            }
            while (i < tokens.Length)
            {
                if (char.IsLetter(tokens[i][0])) command = tokens[i++][0];
                bool relative = char.IsLower(command);
                PointF end;
                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                        current = start = Point(relative); path.StartFigure(); command = relative ? 'l' : 'L'; break;
                    case 'L':
                        end = Point(relative); path.AddLine(current, end); current = end; break;
                    case 'H':
                        end = new PointF(Next() + (relative ? current.X : 0), current.Y); path.AddLine(current, end); current = end; break;
                    case 'V':
                        end = new PointF(current.X, Next() + (relative ? current.Y : 0)); path.AddLine(current, end); current = end; break;
                    case 'C':
                        PointF one = Point(relative), two = Point(relative); end = Point(relative);
                        path.AddBezier(current, one, two, end); current = end; break;
                    case 'A':
                        float rx = Next(), ry = Next(), rotation = Next(); bool large = Next() != 0, sweep = Next() != 0;
                        end = Point(relative); AddArc(path, current, end, rx, ry, rotation, large, sweep); current = end; break;
                    case 'Z':
                        path.CloseFigure(); current = start; break;
                    default: throw new FormatException($"Unsupported Lucide path command: {command}");
                }
            }
            return path;
        }

        // SVG endpoint arc conversion. Cubic segments stay within a quarter circle.
        private static void AddArc(GraphicsPath path, PointF from, PointF to, double rx, double ry, double rotation, bool large, bool sweep)
        {
            rx = Math.Abs(rx); ry = Math.Abs(ry);
            if (rx == 0 || ry == 0) { path.AddLine(from, to); return; }
            if (from == to) return;
            double phi = rotation * Math.PI / 180, cos = Math.Cos(phi), sin = Math.Sin(phi);
            double dx = (from.X - to.X) / 2d, dy = (from.Y - to.Y) / 2d;
            double x = cos * dx + sin * dy, y = -sin * dx + cos * dy;
            double scale = x * x / (rx * rx) + y * y / (ry * ry);
            if (scale > 1) { rx *= Math.Sqrt(scale); ry *= Math.Sqrt(scale); }
            double denominator = rx * rx * y * y + ry * ry * x * x;
            double factor = denominator == 0 ? 0 : Math.Sqrt(Math.Max(0, (rx * rx * ry * ry - denominator) / denominator));
            if (large == sweep) factor = -factor;
            double cx1 = factor * rx * y / ry, cy1 = -factor * ry * x / rx;
            double cx = cos * cx1 - sin * cy1 + (from.X + to.X) / 2d;
            double cy = sin * cx1 + cos * cy1 + (from.Y + to.Y) / 2d;
            double first = Math.Atan2((y - cy1) / ry, (x - cx1) / rx);
            double last = Math.Atan2((-y - cy1) / ry, (-x - cx1) / rx);
            double delta = last - first;
            if (sweep && delta < 0) delta += Math.PI * 2;
            if (!sweep && delta > 0) delta -= Math.PI * 2;
            int segments = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2)));
            double step = delta / segments;
            PointF Transform(double px, double py) => new((float)(cx + rx * cos * px - ry * sin * py), (float)(cy + rx * sin * px + ry * cos * py));
            for (int segment = 0; segment < segments; segment++)
            {
                double a = first + segment * step, b = a + step, alpha = 4d / 3 * Math.Tan(step / 4);
                var p1 = Transform(Math.Cos(a) - alpha * Math.Sin(a), Math.Sin(a) + alpha * Math.Cos(a));
                var p2 = Transform(Math.Cos(b) + alpha * Math.Sin(b), Math.Sin(b) - alpha * Math.Cos(b));
                var end = segment == segments - 1 ? to : Transform(Math.Cos(b), Math.Sin(b));
                path.AddBezier(from, p1, p2, end); from = end;
            }
        }
    }
}
