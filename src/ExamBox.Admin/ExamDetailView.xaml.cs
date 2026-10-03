using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ExamBox.Models;
using Microsoft.Win32;

namespace ExamBox.Admin;

public partial class ExamDetailView : UserControl
{
    public sealed record QuestionRow(int Id, int No, string Text, string Options, string Correct, int Marks);
    public sealed record ResultRow(string Student, string StudentId, string Score, string Percent, string Outcome, string Submitted);

    private readonly int _id;
    private Exam? _exam;
    private bool _locked;

    public ExamDetailView(int id)
    {
        _id = id;
        InitializeComponent();
        Loaded += (_, _) => Reload();
    }

    private QuestionRow? SelectedQ => QTable.SelectedItem as QuestionRow;

    private void Reload()
    {
        var e = App.Exams.Get(_id);
        if (e == null) { MainWindow.Current.Go("exams"); return; }
        _exam = e;
        _locked = e.Attempts.Count > 0;

        TitleText.Text = e.Title;
        var total = e.Questions.Sum(q => q.Marks);
        Meta.Text = $"{e.DurationMinutes} min · pass mark {e.PassMarkPercent}% · {e.Questions.Count} question(s), {total} mark(s) · {(e.IsPublished ? "Published" : "Draft")}";
        PublishBtn.Content = e.IsPublished ? "Unpublish" : "Publish";
        LockedNote.Visibility = _locked ? Visibility.Visible : Visibility.Collapsed;
        AddQ.IsEnabled = !_locked;

        var n = 0;
        QTable.ItemsSource = e.Questions.Select(q => new QuestionRow(q.Id, ++n, q.Text,
            string.Join("   ", q.Options().Select(o => $"{o.Key}. {o.Text}")), q.CorrectOption, q.Marks)).ToList();
        UpdateQButtons();

        var done = e.Attempts.Where(a => a.SubmittedAt != null).OrderByDescending(a => a.Score).ToList();
        RTable.ItemsSource = done.Select(a => new ResultRow(a.Student!.FullName, a.Student.Username, $"{a.Score} / {a.TotalMarks}", $"{a.Percent}%",
            a.Percent >= e.PassMarkPercent ? "Pass" : "Fail", Ui.Local(a.SubmittedAt))).ToList();
        ResultSummary.Text = done.Count == 0
            ? "No submissions yet."
            : $"{done.Count} submission(s) · average {Math.Round(done.Average(a => a.Percent), 1)}% · {done.Count(a => a.Percent >= e.PassMarkPercent)} passed";
    }

    private void UpdateQButtons() => EditQ.IsEnabled = DelQ.IsEnabled = !_locked && SelectedQ != null;
    private void QTable_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateQButtons();
    private void Back_Click(object sender, RoutedEventArgs e) => MainWindow.Current.Go("exams");

    private void Publish_Click(object sender, RoutedEventArgs e)
    {
        var r = App.Exams.SetPublished(_id, !_exam!.IsPublished);
        if (!r.Ok) Ui.Error(r.Error!);
        Reload();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (new ExamDialog(_exam) { Owner = Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!Ui.Confirm($"Delete “{_exam!.Title}”, its questions and all student results?\n\nThis cannot be undone.")) return;
        var r = App.Exams.Delete(_id);
        if (!r.Ok) { Ui.Error(r.Error!); return; }
        MainWindow.Current.Go("exams");
    }

    private void AddQ_Click(object sender, RoutedEventArgs e)
    {
        if (new QuestionDialog(_id, null) { Owner = Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void EditQ_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedQ == null) return;
        var q = _exam!.Questions.FirstOrDefault(x => x.Id == SelectedQ.Id);
        if (q != null && new QuestionDialog(_id, q) { Owner = Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void DelQ_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedQ == null || !Ui.Confirm("Delete this question?")) return;
        var r = App.Exams.DeleteQuestion(_id, SelectedQ.Id);
        if (!r.Ok) Ui.Error(r.Error!);
        Reload();
    }

    private static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var rows = (RTable.ItemsSource as IEnumerable<ResultRow>)?.ToList() ?? new();
        if (rows.Count == 0) { Ui.Info("There are no submissions to export yet."); return; }
        var safe = string.Concat(_exam!.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var dlg = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = $"{safe} results.csv" };
        if (dlg.ShowDialog() != true) return;
        var sb = new StringBuilder("Student,Student ID,Score,Percent,Outcome,Submitted\r\n");
        foreach (var r in rows) sb.Append(string.Join(",", new[] { r.Student, r.StudentId, r.Score, r.Percent, r.Outcome, r.Submitted }.Select(Csv))).Append("\r\n");
        try { File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true)); Ui.Info("Results exported."); }
        catch (Exception ex) { Ui.Error("Could not save the file:\n" + ex.Message); }
    }
}
