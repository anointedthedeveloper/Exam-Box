using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExamBox.Models;
using ExamBox.Services;

namespace ExamBox.Admin;

/// <summary>Shows one student's typed answers next to the marking guide and collects marks and comments.</summary>
public partial class MarkingDialog : Window
{
    private sealed record Row(Question Q, TextBox Marks, TextBox Comment);

    private readonly int _attemptId;
    private readonly MarkingSheet _sheet;
    private readonly List<Row> _rows = new();

    public MarkingDialog(MarkingSheet sheet)
    {
        _sheet = sheet; _attemptId = sheet.Attempt.Id;
        InitializeComponent();
        Title = Heading.Text = $"Mark: {sheet.Student.FullName}";
        var a = sheet.Attempt;
        Sub.Text = $"{sheet.Exam.Title} · student ID {sheet.Student.Username} · objective score {a.ObjectiveScore}. " +
                   (a.PendingMarking ? "Give a mark (0 is allowed) for every question, then finish." : "Already marked. You can still adjust the marks.");
        FinishBtn.Content = a.PendingMarking ? "Finish marking" : "Save changes";
        foreach (var (q, ans) in sheet.Items) Items.Children.Add(Build(q, ans));
        Recalc();
    }

    private UIElement Build(Question q, Answer ans)
    {
        var label = MarkingService.Label(q);
        var card = new Border { Background = new SolidColorBrush(Color.FromArgb(0x99, 255, 255, 255)), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 12),
            BorderBrush = (Brush)FindResource("LineBrush"), BorderThickness = new Thickness(1) };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = $"{label}.  {q.Text}", FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (q.HasImage)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.StreamSource = new MemoryStream(q.ImageData!); bmp.EndInit(); bmp.Freeze();
                sp.Children.Add(new Image { Source = bmp, MaxHeight = 180, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) });
            }
            catch { /* unreadable picture: skip */ }
        }
        sp.Children.Add(new TextBlock { Text = "STUDENT'S ANSWER", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 10, 0, 4) });
        var typed = string.IsNullOrWhiteSpace(ans.Text);
        sp.Children.Add(new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 10), BorderBrush = (Brush)FindResource("LineBrush"), BorderThickness = new Thickness(1),
            Child = new TextBlock { Text = typed ? "(no answer)" : ans.Text, TextWrapping = TextWrapping.Wrap, FontStyle = typed ? FontStyles.Italic : FontStyles.Normal, Foreground = typed ? (Brush)FindResource("MutedBrush") : (Brush)FindResource("InkBrush") },
        });
        if (!string.IsNullOrWhiteSpace(q.ModelAnswer))
        {
            sp.Children.Add(new TextBlock { Text = "MARKING GUIDE", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("OkBrush"), Margin = new Thickness(0, 10, 0, 4) });
            sp.Children.Add(new TextBlock { Text = q.ModelAnswer, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("OkBrush") });
        }
        var line = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var marks = new TextBox { Width = 70, Text = ans.Marks?.ToString() ?? "", ToolTip = $"0 to {q.Marks}" };
        marks.TextChanged += (_, _) => Recalc();
        var comment = new TextBox { Text = ans.Comment ?? "", MaxLength = 1000, Margin = new Thickness(12, 0, 0, 0), ToolTip = "Comment for the student (optional)" };
        DockPanel.SetDock(marks, Dock.Left);
        var of = new TextBlock { Text = $"/ {q.Marks}", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), FontWeight = FontWeights.SemiBold };
        DockPanel.SetDock(of, Dock.Left);
        line.Children.Add(marks); line.Children.Add(of); line.Children.Add(comment);
        sp.Children.Add(line);
        card.Child = sp;
        _rows.Add(new Row(q, marks, comment));
        if (typed && marks.Text.Length == 0) marks.Text = "0";
        return card;
    }

    private void Recalc()
    {
        if (Total == null) return;
        var sum = 0; var max = 0;
        foreach (var r in _rows) { max += r.Q.Marks; if (Ui.TryInt(r.Marks.Text, out var m)) sum += m; }
        Total.Text = $"Theory: {sum} / {max}   ·   Total: {_sheet.Attempt.ObjectiveScore + sum} / {_sheet.Attempt.TotalMarks}";
    }

    private bool Collect(out List<MarkEntry> entries)
    {
        entries = new();
        foreach (var r in _rows)
        {
            int? marks = null;
            if (r.Marks.Text.Trim().Length > 0)
            {
                if (!Ui.TryInt(r.Marks.Text, out var m)) { Fail($"Marks for {MarkingService.Label(r.Q)} must be a whole number."); return false; }
                marks = m;
            }
            entries.Add(new MarkEntry(r.Q.Id, marks, r.Comment.Text));
        }
        return true;
    }

    private void Fail(string m) { ErrorText.Text = m; ErrorText.Visibility = Visibility.Visible; }

    private void Commit(bool finish)
    {
        if (!Collect(out var entries)) return;
        var r = App.Marking.Save(_attemptId, entries, finish);
        if (!r.Ok) { Fail(r.Error!); return; }
        MainWindow.Current.RefreshMarkingBadge();
        DialogResult = true;
    }

    private void SaveProgress_Click(object sender, RoutedEventArgs e) => Commit(false);
    private void Finish_Click(object sender, RoutedEventArgs e) => Commit(true);
}
