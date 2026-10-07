using System.Windows;
using System.Windows.Controls;
using ExamBox.Models;

namespace ExamBox.Admin;

public partial class ExamsView : UserControl
{
    public sealed record ExamRow(int Id, string Title, string Kind, string Questions, string Duration, string Audience, string Window, int Submissions, string Status);

    private readonly bool _openNew;
    private List<Exam> _all = new();

    public ExamsView(bool openNew = false)
    {
        _openNew = openNew;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Reload();
            if (_openNew) New_Click(this, new RoutedEventArgs());
        };
    }

    private static ExamRow? RowOf(object sender) => (sender as FrameworkElement)?.DataContext as ExamRow;
    private ExamRow? Selected => Table.SelectedItem as ExamRow;

    private static string StatusOf(Exam e) => e.StateAt(DateTime.UtcNow) switch
    {
        ExamState.Open => "Live", ExamState.Scheduled => "Scheduled", ExamState.Closed => "Closed", _ => "Draft",
    };

    private static string WindowText(Exam e)
    {
        string L(DateTime d) => Ui.Local(d);
        return e.StateAt(DateTime.UtcNow) switch
        {
            ExamState.Draft => "Not launched",
            ExamState.Scheduled => $"Opens {L(e.OpensAt!.Value)}" + (e.ClosesAt != null ? $", closes {L(e.ClosesAt.Value)}" : ""),
            ExamState.Closed => $"Closed {L(e.ClosesAt!.Value)}",
            _ => e.ClosesAt != null ? $"Open until {L(e.ClosesAt.Value)}" : "Open until you close it",
        };
    }

    private static string Kind(Exam e)
    {
        var obj = e.Questions.Count(q => q.Type == QuestionType.Objective);
        var th = e.Questions.Count(q => q.Type == QuestionType.Theory && q.Marks > 0);
        if (obj > 0 && th > 0) return $"Objective + theory · {e.Questions.Sum(q => q.Marks)} marks";
        if (th > 0) return $"Theory · {e.Questions.Sum(q => q.Marks)} marks";
        if (obj > 0) return $"Objective · {e.Questions.Sum(q => q.Marks)} marks";
        return "No questions yet";
    }

    private void Reload()
    {
        _all = App.Exams.List();
        StatTotal.Text = _all.Count.ToString();
        StatLive.Text = _all.Count(e => StatusOf(e) == "Live").ToString();
        StatDrafts.Text = _all.Count(e => StatusOf(e) == "Draft").ToString();
        StatScheduled.Text = _all.Count(e => StatusOf(e) == "Scheduled").ToString();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<Exam> rows = _all;
        string? want = FDraft.IsChecked == true ? "Draft" : FScheduled.IsChecked == true ? "Scheduled" : FLive.IsChecked == true ? "Live" : FClosed.IsChecked == true ? "Closed" : null;
        if (want != null) rows = rows.Where(e => StatusOf(e) == want);
        var q = Search.Text.Trim();
        if (q.Length > 0) rows = rows.Where(e => e.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || (e.ForDepartment ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        var list = rows.ToList();
        Table.ItemsSource = list.Select(e => new ExamRow(e.Id, e.Title, Kind(e), $"{e.Questions.Count(x => x.Marks > 0)}", $"{e.DurationMinutes} min",
            e.ForDepartment ?? "Everyone", WindowText(e), e.Attempts.Count(a => a.SubmittedAt != null), StatusOf(e))).ToList();
        Count.Text = _all.Count == 0 ? "" : $"Showing {list.Count} of {_all.Count}";
        var empty = list.Count == 0;
        Empty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        Table.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        TableCard.MinHeight = empty ? 380 : 0;
        var filtering = _all.Count > 0;
        EmptyTitle.Text = filtering ? "No matches" : "No exams yet";
        EmptyText.Text = filtering ? "No exams match your search or filter." : "Click “New exam”, then import your questions from Excel.";
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = Search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (IsLoaded) ApplyFilter();
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => ApplyFilter();
    private void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (Selected != null) MainWindow.Current.OpenExam(Selected.Id); }
    private void Open_Click(object sender, RoutedEventArgs e) { if (RowOf(sender) is { } r) MainWindow.Current.OpenExam(r.Id); }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ExamDialog(null) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true && dlg.Saved != null) MainWindow.Current.OpenExam(dlg.Saved.Id);
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        var exam = App.Exams.Get(row.Id);
        if (exam == null) { Reload(); return; }
        if (new LaunchDialog(exam) { Owner = Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        var r = App.Exams.Duplicate(row.Id);
        if (!r.Ok) { Ui.Error(r.Error!); return; }
        MainWindow.Current.OpenExam(r.Value!.Id);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        if (!Ui.Confirm($"Delete “{row.Title}”, its questions and all student results?\n\nThis cannot be undone.")) return;
        var r = App.Exams.Delete(row.Id);
        if (!r.Ok) Ui.Error(r.Error!);
        Reload();
    }
}
