using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace WindowsTool.Controls;

/// <summary>
/// 星野粒子背景 —— 对标 linusonn.com 的 canvas 星空：
/// 星点缓慢漂移 + 呼吸闪烁，外加两团星云光晕和一层细网格（无鼠标跟随）。
///
/// 性能约束（2026-10-24 改）：
/// - 原来每帧给每颗星 new SolidColorBrush，60fps × 600 颗 ≈ 每秒 3.6 万次分配，
///   实测常驻 96% 单核、WS 315MB。现在所有画笔预生成并 Freeze，按透明度分档查表。
/// - 帧率限到 ~30fps；窗口最小化或不可见时完全停画。
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

    // ---- 预生成并 Freeze 的静态资源：每帧零分配 ----
    private const int AlphaLevels = 32;
    private static readonly SolidColorBrush Background = Freeze(new SolidColorBrush(Color.FromRgb(0x03, 0x04, 0x0A)));
    private static readonly Pen GridPen = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(12, 0x22, 0xD3, 0xEE))), 1));
    private static readonly SolidColorBrush[] StarBrushes = new SolidColorBrush[4 * AlphaLevels];
    private static readonly SolidColorBrush[] GlowBrushes = new SolidColorBrush[4 * AlphaLevels];
    private static readonly Brush[] Nebula =
    {
        MakeNebula(Cyan, 0.16),
        MakeNebula(Purple, 0.15),
        MakeNebula(Pink, 0.05),
    };

    static StarfieldBackground()
    {
        var tints = new[] { White, Cyan, Purple, Pink };
        for (var t = 0; t < 4; t++)
        {
            for (var a = 0; a < AlphaLevels; a++)
            {
                var alpha = (byte)(255 * a / (AlphaLevels - 1));
                StarBrushes[t * AlphaLevels + a] =
                    Freeze(new SolidColorBrush(Color.FromArgb(alpha, tints[t].R, tints[t].G, tints[t].B)));
                GlowBrushes[t * AlphaLevels + a] =
                    Freeze(new SolidColorBrush(Color.FromArgb((byte)(alpha / 5), tints[t].R, tints[t].G, tints[t].B)));
            }
        }
    }

    public StarfieldBackground()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => { EnsureStars(); CompositionTarget.Rendering += OnFrame; };
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
        SizeChanged += (_, _) => EnsureStars();
    }

    private static T Freeze<T>(T f) where T : Freezable
    {
        if (f.CanFreeze) f.Freeze();
        return f;
    }

    private static Brush MakeNebula(Color color, double strength) => Freeze(new RadialGradientBrush
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
    });

    private DrawingGroup? _staticLayer;
    private double _staticW, _staticH;

    /// <summary>底色 / 星云 / 网格都是静态的，按尺寸烘焙成一条 DrawingGroup，每帧只画一次。</summary>
    private DrawingGroup BuildStaticLayer(double w, double h)
    {
        var g = new DrawingGroup();
        using (var dc = g.Open())
        {
            dc.DrawRectangle(Background, null, new Rect(0, 0, w, h));
            dc.DrawEllipse(Nebula[0], null, new Point(w * 0.18, h * 0.16), w * 0.62, w * 0.62);
            dc.DrawEllipse(Nebula[1], null, new Point(w * 0.86, h * 0.88), w * 0.70, w * 0.70);
            dc.DrawEllipse(Nebula[2], null, new Point(w * 0.62, h * 0.42), w * 0.42, w * 0.42);
            const double step = 58;
            for (double x = 0; x < w; x += step) dc.DrawLine(GridPen, new Point(x, 0), new Point(x, h));
            for (double y = 0; y < h; y += step) dc.DrawLine(GridPen, new Point(0, y), new Point(w, y));
        }
        g.Freeze();
        _staticW = w; _staticH = h;
        return g;
    }

    private void EnsureStars()
    {
        var w = ActualWidth <= 0 ? 900 : ActualWidth;
        var h = ActualHeight <= 0 ? 600 : ActualHeight;
        var count = (int)Math.Clamp(w * h / 2600.0, 140, 420);
        var stars = new Star[count];
        for (var i = 0; i < count; i++)
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

    private double _t;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrameMs = -1000;

    private void OnFrame(object? sender, EventArgs e)
    {
        // 最小化 / 不可见时彻底不画
        if (!IsVisible) return;
        if (Window.GetWindow(this) is { WindowState: WindowState.Minimized }) return;

        // 限帧 ~30fps
        var ms = _clock.Elapsed.TotalMilliseconds;
        if (ms - _lastFrameMs < 50) return;
        _t += (ms - _lastFrameMs) / 1000.0;
        _lastFrameMs = ms;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 1 || h <= 1) return;

        // 静态层（底色 + 星云 + 网格）只画一次
        if (_staticLayer == null || Math.Abs(_staticW - w) > 0.5 || Math.Abs(_staticH - h) > 0.5)
            _staticLayer = BuildStaticLayer(w, h);
        dc.DrawDrawing(_staticLayer);

        // 星点（画笔查表，零分配）
        foreach (var s in _stars)
        {
            var drift = _t * s.Speed * (0.6 + s.Z);
            var x = s.X - drift * 2.2;
            var y = s.Y - drift * 0.5;
            x = ((x % w) + w) % w;
            y = ((y % h) + h) % h;

            var tw = 0.55 + 0.45 * Math.Sin(_t * (0.6 + s.Z) + s.Phase);
            var alpha = Math.Clamp((int)((40 + s.Z * 185) * tw), 0, 255);
            var idx = s.Tint * AlphaLevels + alpha * (AlphaLevels - 1) / 255;
            var p = new Point(x, y);

            dc.DrawEllipse(StarBrushes[idx], null, p, s.Size, s.Size);

            if (s.Z > 0.82)
                dc.DrawEllipse(GlowBrushes[idx], null, p, s.Size * 3.4, s.Size * 3.4);
        }
    }
}
