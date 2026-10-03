using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ExamBox.Admin;

/// <summary>Small reusable motion effects.</summary>
internal static class Anim
{
    private static readonly CubicEase EaseOut = new() { EasingMode = EasingMode.EaseOut };

    public static void FadeUp(FrameworkElement e, int delayMs = 0, double fromY = 24, int durationMs = 480)
    {
        var tt = new TranslateTransform(0, fromY);
        e.RenderTransform = tt;
        e.Opacity = 0;
        var begin = TimeSpan.FromMilliseconds(delayMs);
        var dur = TimeSpan.FromMilliseconds(durationMs);
        e.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, dur) { BeginTime = begin, EasingFunction = EaseOut });
        tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, 0, dur) { BeginTime = begin, EasingFunction = EaseOut });
    }

    /// <summary>Items rise in one after another.</summary>
    public static void Stagger(IEnumerable<FrameworkElement> items, int stepMs = 75)
    {
        var i = 0;
        foreach (var e in items) FadeUp(e, i++ * stepMs);
    }

    /// <summary>Counts a number up with an ease-out.</summary>
    public static void CountUp(TextBlock target, double value, int decimals = 0, string suffix = "", int durationMs = 900)
    {
        var start = DateTime.UtcNow;
        var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            var t = Math.Min(1.0, (DateTime.UtcNow - start).TotalMilliseconds / durationMs);
            var eased = 1 - Math.Pow(1 - t, 3);
            target.Text = Math.Round(value * eased, decimals).ToString("F" + decimals) + suffix;
            if (t >= 1) timer.Stop();
        };
        target.Text = (0.0).ToString("F" + decimals) + suffix;
        timer.Start();
    }
}
