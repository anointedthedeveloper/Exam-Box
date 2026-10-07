using System.Windows;
using System.Windows.Controls;
using ExamBox.Services;

namespace ExamBox.Admin;

public partial class MarkingView : UserControl
{
    public sealed record PendingVm(int AttemptId, string Student, string StudentId, string Exam, int Objective, int TheoryMax, string Submitted);

    public MarkingView()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
    }

    private void Reload()
    {
        App.Attempts.CollectExpired();   // unfinished attempts whose time ran out count as submitted
        var list = App.Marking.Pending();
        Table.ItemsSource = list.Select(p => new PendingVm(p.AttemptId, p.Student, p.StudentId, p.Exam, p.ObjectiveScore, p.TheoryMax, Ui.Ago(p.SubmittedUtc))).ToList();
        Empty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Table.Visibility = list.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        Sub.Text = list.Count == 0 ? "All caught up." : $"{list.Count} submission(s) waiting for your marks. Double-click a row to open it.";
        MainWindow.Current.RefreshMarkingBadge();
    }

    private void Open(PendingVm? row)
    {
        if (row == null) return;
        var sheet = App.Marking.Sheet(row.AttemptId);
        if (sheet == null) { Reload(); return; }
        if (new MarkingDialog(sheet) { Owner = Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void Mark_Click(object sender, RoutedEventArgs e) => Open((sender as FrameworkElement)?.DataContext as PendingVm);
    private void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => Open(Table.SelectedItem as PendingVm);
}
