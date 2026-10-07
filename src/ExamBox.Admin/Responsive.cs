using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ExamBox.Admin;

/// <summary>
/// Layout helpers for small windows and laptop screens.
/// <c>StackBelow</c>: a multi-column Grid collapses into one column once its width drops under the value.
/// <c>CellWidth</c>: a UniformGrid picks as many columns as fit, so cards wrap instead of squeezing.
/// </summary>
public static class Responsive
{
    public static readonly DependencyProperty StackBelowProperty = DependencyProperty.RegisterAttached(
        "StackBelow", typeof(double), typeof(Responsive), new PropertyMetadata(0.0, OnStackBelow));
    public static double GetStackBelow(DependencyObject d) => (double)d.GetValue(StackBelowProperty);
    public static void SetStackBelow(DependencyObject d, double v) => d.SetValue(StackBelowProperty, v);

    public static readonly DependencyProperty CellWidthProperty = DependencyProperty.RegisterAttached(
        "CellWidth", typeof(double), typeof(Responsive), new PropertyMetadata(0.0, OnCellWidth));
    public static double GetCellWidth(DependencyObject d) => (double)d.GetValue(CellWidthProperty);
    public static void SetCellWidth(DependencyObject d, double v) => d.SetValue(CellWidthProperty, v);

    private sealed class Saved
    {
        public List<ColumnDefinition> Cols = new();
        public Dictionary<UIElement, (int Row, int Col, int Span, Thickness Margin)> Kids = new();
        public bool Stacked;
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Grid, Saved> State = new();

    private static void OnStackBelow(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Grid g) return;
        g.SizeChanged -= GridSized; g.SizeChanged += GridSized;
        g.Loaded -= GridLoaded; g.Loaded += GridLoaded;
    }

    private static void GridLoaded(object sender, RoutedEventArgs e) => Apply((Grid)sender);
    private static void GridSized(object sender, SizeChangedEventArgs e) => Apply((Grid)sender);

    private static void Apply(Grid g)
    {
        var limit = GetStackBelow(g);
        if (limit <= 0 || g.RowDefinitions.Count > 0 && !State.TryGetValue(g, out _) || g.ActualWidth <= 0) return;
        var st = State.GetOrCreateValue(g);
        var narrow = g.ActualWidth < limit;
        if (narrow == st.Stacked) return;

        if (narrow)
        {
            st.Cols = g.ColumnDefinitions.ToList();
            st.Kids.Clear();
            foreach (UIElement k in g.Children)
                st.Kids[k] = (Grid.GetRow(k), Grid.GetColumn(k), Grid.GetColumnSpan(k), k is FrameworkElement fe ? fe.Margin : default);
            g.ColumnDefinitions.Clear();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var order = st.Kids.OrderBy(p => p.Value.Col).Select(p => p.Key).ToList();
            for (var i = 0; i < order.Count; i++)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var k = order[i];
                Grid.SetRow(k, i); Grid.SetColumn(k, 0); Grid.SetColumnSpan(k, 1);
                if (i > 0 && k is FrameworkElement fe) fe.Margin = new Thickness(fe.Margin.Left, fe.Margin.Top + 16, fe.Margin.Right, fe.Margin.Bottom);
            }
        }
        else
        {
            g.RowDefinitions.Clear();
            g.ColumnDefinitions.Clear();
            foreach (var c in st.Cols) g.ColumnDefinitions.Add(c);
            foreach (var (k, v) in st.Kids)
            {
                Grid.SetRow(k, v.Row); Grid.SetColumn(k, v.Col); Grid.SetColumnSpan(k, v.Span);
                if (k is FrameworkElement fe) fe.Margin = v.Margin;
            }
        }
        st.Stacked = narrow;
    }

    private static void OnCellWidth(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UniformGrid u) return;
        u.SizeChanged -= UniSized; u.SizeChanged += UniSized;
    }

    private static void UniSized(object sender, SizeChangedEventArgs e)
    {
        var u = (UniformGrid)sender;
        var cell = GetCellWidth(u);
        if (cell <= 0 || u.ActualWidth <= 0) return;
        var cols = Math.Clamp((int)(u.ActualWidth / cell), 1, Math.Max(1, u.Children.Count));
        if (u.Columns != cols) { u.Rows = 0; u.Columns = cols; }
    }
}
