using System.Windows;
using System.Windows.Media;

namespace WindowsTool.Controls;

/// <summary>
/// 星野粒子背景 —— 对标 linusonn.com 的 canvas 星空：
/// 星点缓慢漂移 + 呼吸闪烁，外加两团星云光晕和一层细网格（无鼠标跟随）。
/// </summary>
public class StarfieldBackground : FrameworkElement
{
    private record struct Star(double X, double Y, double Z, double Size, double Phase, double Speed, byte Tint);

    private Star[] _stars = Array.Empty<Star>();
    private readonly Random _rng = new(20260925);

    private static readonly Color Cyan = Color.FromRgb(0x22, 0xD3, 0xEE);
    private static readonly Color Purple = Color.FromRgb(0xA7, 0x8B, 0xFA);
    private static readonly Color White = Color.FromRgb(0xE8, 0xED, 0xF5);
    private static readonly Color Pink = Color.FromRgb(0xF0, 0xAB, 0xFC);

    public StarfieldBackground()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => { EnsureStars(); CompositionTarget.Rendering += OnFrame; };
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
        SizeChanged += (_, _) => EnsureStars();
    }

    private void EnsureStars()
    {
        var w = ActualWidth <= 0 ? 900 : ActualWidth;
        var h = ActualHeight <= 0 ? 600 : ActualHeight;
        var count = (int)Math.Clamp(w * h / 1600.0, 260, 900);
        var stars = new Star[count];
        for (int i = 0; i < count; i++)
        {
            // Z: 0=远(小暗慢) 1=近(大亮快)
            var z = _rng.NextDouble();
            stars[i] = new Star(
                X: _rng.NextDouble() * w,
                Y: _rng.NextDouble() * h,
                Z: z,
                Size: 0.45 + z * 1.5,
                Phase: _rng.NextDouble() * Math.PI * 2,
                Speed: 0.15 + z * 0.9,
                Tint: (byte)_rng.Next(0, 4)
            );
        }
        _stars = stars;
    }

    private static double _t;

    private void OnFrame(object? sender, EventArgs e)
    {
        _t += 0.016;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 1 || h <= 1) return;

        // 底色
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x03, 0x04, 0x0A)), null, new Rect(0, 0, w, h));

        // 星云：左上青、右下紫
        DrawNebula(dc, new Point(w * 0.18, h * 0.16), w * 0.62, Cyan, 0.16);
        DrawNebula(dc, new Point(w * 0.86, h * 0.88), w * 0.70, Purple, 0.15);
        DrawNebula(dc, new Point(w * 0.62, h * 0.42), w * 0.42, Pink, 0.05);

        // 细网格
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(12, 0x22, 0xD3, 0xEE)), 1);
        gridPen.Freeze();
        const double step = 58;
        for (double x = 0; x < w; x += step) dc.DrawLine(gridPen, new Point(x, 0), new Point(x, h));
        for (double y = 0; y < h; y += step) dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));

        // 星点
        foreach (var s in _stars)
        {
            var drift = _t * s.Speed * (0.6 + s.Z);
            var x = s.X - drift * 2.2;
            var y = s.Y - drift * 0.5;
            x = ((x % w) + w) % w;
            y = ((y % h) + h) % h;

            var tw = 0.55 + 0.45 * Math.Sin(_t * (0.6 + s.Z) + s.Phase);
            var alpha = (byte)Math.Clamp((40 + s.Z * 185) * tw, 0, 255);

            var baseColor = s.Tint switch
            {
                0 => White,
                1 => Cyan,
                2 => Purple,
                _ => Pink,
            };
            var col = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
            var brush = new SolidColorBrush(col);
            brush.Freeze();
            dc.DrawEllipse(brush, null, new Point(x, y), s.Size, s.Size);

            // 近处星星加一点辉光
            if (s.Z > 0.82)
            {
                var glow = new SolidColorBrush(Color.FromArgb((byte)(alpha / 5), baseColor.R, baseColor.G, baseColor.B));
                glow.Freeze();
                dc.DrawEllipse(glow, null, new Point(x, y), s.Size * 3.4, s.Size * 3.4);
            }
        }
    }

    private static void DrawNebula(DrawingContext dc, Point center, double radius, Color color, double strength)
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5,
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(255 * strength), color.R, color.G, color.B), 0),
                new GradientStop(Color.FromArgb((byte)(255 * strength * 0.35), color.R, color.G, color.B), 0.45),
                new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1),
            }
        };
        brush.Freeze();
        dc.DrawEllipse(brush, null, center, radius, radius);
    }
}
