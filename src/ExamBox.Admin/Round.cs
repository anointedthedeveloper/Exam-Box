using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExamBox.Admin;

/// <summary>Attached property: clips a Border's content to its rounded corners (e.g. a DataGrid inside a card).</summary>
public static class Round
{
    public static readonly DependencyProperty ClipProperty =
        DependencyProperty.RegisterAttached("Clip", typeof(bool), typeof(Round), new PropertyMetadata(false, OnChanged));

    public static bool GetClip(DependencyObject o) => (bool)o.GetValue(ClipProperty);
    public static void SetClip(DependencyObject o, bool v) => o.SetValue(ClipProperty, v);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Border b) return;
        b.SizeChanged -= Update;
        if ((bool)e.NewValue) { b.SizeChanged += Update; Update(b, null); }
        else b.Clip = null;
    }

    private static void Update(object sender, SizeChangedEventArgs? e)
    {
        var b = (Border)sender;
        var r = b.CornerRadius.TopLeft;
        b.Clip = new RectangleGeometry(new Rect(0, 0, b.ActualWidth, b.ActualHeight), r, r);
    }
}
