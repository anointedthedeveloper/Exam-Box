using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ExamBox.Models;
using ExamBox.Services;
using Microsoft.Win32;

namespace ExamBox.Admin;

public partial class ExamDetailView : UserControl
{
    public sealed record QuestionRow(int Id, string No, string Kind, string Text, string Details, int Marks, string Picture);
    public sealed record ResultRow(int AttemptId, string Student, string StudentId, string Objective, string Theory, string Score, string Percent, string Outcome, string Submitted);

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
    private ResultRow? SelectedR => RTable.SelectedItem as ResultRow;

    private void Reload()
    {
        App.Attempts.CollectExpired();
        var e = App.Exams.Get(_id);
        if (e == null) { MainWindow.Current.Go("exams"); return; }
        _exam = e;
        _locked = e.Attempts.Count > 0;
        var state = e.StateAt(DateTime.UtcNow);

        TitleText.Text = e.Title;
        var total = e.Questions.Sum(q => q.Marks);
        Meta.Text = $"{e.DurationMinutes} min · pass mark {e.PassMarkPercent}% · {e.Questions.Count(q => q.Marks > 0)} question(s), {total} mark(s)" +
                    (string.IsNullOrWhiteSpace(e.ForDepartment) ? " · for every student" : $" · for class {e.ForDepartment}");
        WindowInfo.Text = state switch
        {
            ExamState.Scheduled => $"Opens {Ui.Local(e.OpensAt)}" + (e.ClosesAt != null ? $" · closes {Ui.Local(e.ClosesAt)}" : ""),
            ExamState.Open => e.ClosesAt != null ? $"Open now · closes {Ui.Local(e.ClosesAt)}" : "Open now · stays open until you close it",
            ExamState.Closed => $"Closed {Ui.Local(e.ClosesAt)}",
            _ => "Draft: students cannot see it yet. Launch it when it is time.",
        };
        var (txt, fg, bg) = state switch
        {
            ExamState.Open => ("Live", "OkBrush", "OkBgBrush"),
            ExamState.Scheduled => ("Scheduled", "BlueDarkBrush", "BlueLightBrush"),
            ExamState.Closed => ("Closed", "ErrBrush", "ErrBgBrush"),
            _ => ("Draft", "WarnBrush", "WarnBgBrush"),
        };
        StateText.Text = txt;
        LiveDot.Visibility = state == ExamState.Open ? Visibility.Visible : Visibility.Collapsed;
        LiveDot.BeginAnimation(OpacityProperty, state == ExamState.Open
            ? new System.Windows.Media.Animation.DoubleAnimation(1, 0.2, TimeSpan.FromMilliseconds(900)) { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever }
            : null);
        StateText.Foreground = (System.Windows.Media.Brush)FindResource(fg);
        StatePill.Background = (System.Windows.Media.Brush)FindResource(bg);

        LaunchBtn.Visibility = state is ExamState.Draft or ExamState.Closed ? Visibility.Visible : Visibility.Collapsed;
        StopBtn.Visibility = state == ExamState.Open ? Visibility.Visible : Visibility.Collapsed;
        TakeDownBtn.Visibility = state is ExamState.Open or ExamState.Scheduled ? Visibility.Visible : Visibility.Collapsed;
        ((TextBlock)((StackPanel)LaunchBtn.Content).Children[1]).Text = state == ExamState.Closed ? "Launch again" : "Launch";
        LockedNote.Visibility = _locked ? Visibility.Visible : Visibility.Collapsed;

        var missing = e.Questions.Count(q => q.MissingImage);
        PicNote.Visibility = missing > 0 ? Visibility.Visible : Visibility.Collapsed;
        PicNoteText.Text = $"{missing} question(s) are waiting for a picture. Select a row marked “Needed” and click Attach picture. The exam cannot be launched until they are attached.";

        AddQ.IsEnabled = ImportQ.IsEnabled = !_locked;

        var labels = QLabel(e.Questions);
        QTable.ItemsSource = e.Questions.Select(q => new QuestionRow(q.Id, labels[q.Id], q.Type == QuestionType.Theory ? (q.Marks == 0 ? "Passage" : "Theory") : "Objective",
            q.Text.Replace("\r", " ").Replace("\n", " "), Details(q), q.Marks, q.HasImage ? "Attached" : q.MissingImage ? "Needed" : "-")).ToList();
        QEmpty.Visibility = e.Questions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        QTable.Visibility = e.Questions.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        QSummary.Text = e.Questions.Count == 0 ? "" :
            $"{e.Questions.Count(q => q.Type == QuestionType.Objective)} objective · {e.Questions.Count(q => q.Type == QuestionType.Theory && q.Marks > 0)} theory · {e.Questions.Count(q => q.HasImage)} with pictures · {total} marks in total";
        UpdateButtons();

        var done = e.Attempts.Where(a => a.SubmittedAt != null).OrderByDescending(a => a.PendingMarking).ThenByDescending(a => a.Score).ToList();
        var hasTheory = e.Questions.Any(q => q.Type == QuestionType.Theory && q.Marks > 0);
        RTable.ItemsSource = done.Select(a => new ResultRow(a.Id, a.Student!.FullName, a.Student.Username,
            hasTheory ? $"{a.ObjectiveScore}" : $"{a.ObjectiveScore}", a.PendingMarking ? "-" : hasTheory ? $"{a.TheoryScore}" : "-",
            a.PendingMarking ? "-" : $"{a.Score} / {a.TotalMarks}", a.PendingMarking ? "-" : $"{a.Percent}%",
            a.PendingMarking ? "Awaiting" : a.Percent >= e.PassMarkPercent ? "Pass" : "Fail", Ui.Local(a.SubmittedAt))).ToList();
        var final = done.Where(a => !a.PendingMarking).ToList();
        var waiting = done.Count - final.Count;
        ResultSummary.Text = done.Count == 0 ? "No submissions yet."
            : $"{done.Count} submission(s)" + (final.Count > 0 ? $" · average {Math.Round(final.Average(a => a.Percent), 1)}% · {final.Count(a => a.Percent >= e.PassMarkPercent)} passed" : "")
              + (waiting > 0 ? $" · {waiting} awaiting marking" : "");
        MarkBtn.IsEnabled = false;
    }

    private static Dictionary<int, string> QLabel(IEnumerable<Question> qs)
    {
        var map = new Dictionary<int, string>(); var n = 0;
        foreach (var q in qs) map[q.Id] = string.IsNullOrWhiteSpace(q.Number) ? (++n).ToString() : q.Number!.Trim();
        return map;
    }

    private static string Details(Question q) => q.Type == QuestionType.Theory
        ? (q.Marks == 0 ? "Reading passage, not scored" : string.IsNullOrWhiteSpace(q.ModelAnswer) ? "Typed answer, marked by you" : "Typed answer, marking guide added")
        : $"{q.Options().Count()} options · correct {q.CorrectOption}";

    private void UpdateButtons()
    {
        var has = SelectedQ != null;
        EditQ.IsEnabled = has && !_locked;
        DelQ.IsEnabled = has && !_locked;
        PicQ.IsEnabled = has && !_locked;
        UpQ.IsEnabled = DownQ.IsEnabled = has && !_locked;
    }

    private void QTable_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();
    private void RTable_SelectionChanged(object sender, SelectionChangedEventArgs e) => MarkBtn.IsEnabled = SelectedR != null && _exam!.Questions.Any(q => q.Type == QuestionType.Theory && q.Marks > 0);
    private void Back_Click(object sender, RoutedEventArgs e) => MainWindow.Current.Go("exams");
    private void QTable_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (SelectedQ != null && !_locked) EditQ_Click(sender, e); }
    private void RTable_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (MarkBtn.IsEnabled) Mark_Click(sender, e); }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (new LaunchDialog(_exam!) { Owner = System.Windows.Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (!Ui.Confirm("Close this exam now?\n\nStudents still writing are submitted automatically with what they have typed so far.")) return;
        var r = App.Exams.CloseNow(_id);
        if (!r.Ok) Ui.Error(r.Error!);
        Reload();
    }

    private void TakeDown_Click(object sender, RoutedEventArgs e)
    {
        if (!Ui.Confirm("Take this exam off the student portal and make it a draft again?\n\nSubmitted results are kept.")) return;
        var r = App.Exams.Unpublish(_id);
        if (!r.Ok) Ui.Error(r.Error!);
        Reload();
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        var r = App.Exams.Duplicate(_id);
        if (!r.Ok) { Ui.Error(r.Error!); return; }
        Ui.Info("A copy was created as a draft. You can change it and launch it separately.");
        MainWindow.Current.OpenExam(r.Value!.Id);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (new ExamDialog(_exam) { Owner = System.Windows.Window.GetWindow(this) }.ShowDialog() == true) Reload();
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
        if (new QuestionDialog(_id, null) { Owner = System.Windows.Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (new ImportQuestionsDialog(_id, _exam!.Questions.Count > 0) { Owner = System.Windows.Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void Template_Click(object sender, RoutedEventArgs e) =>
        Templates.Save("ExamBox question template.xlsx", QuestionImporter.BuildTemplate(), System.Windows.Window.GetWindow(this)!);

    private void EditQ_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedQ == null) return;
        var q = _exam!.Questions.FirstOrDefault(x => x.Id == SelectedQ.Id);
        if (q != null && new QuestionDialog(_id, q) { Owner = System.Windows.Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void Pic_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedQ == null) return;
        var dlg = new OpenFileDialog { Filter = "Pictures (*.png;*.jpg;*.jpeg;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.webp" };
        if (dlg.ShowDialog() != true) return;
        var id = SelectedQ.Id;
        var r = App.Exams.SetImage(_id, id, File.ReadAllBytes(dlg.FileName), "");
        if (!r.Ok) Ui.Error(r.Error!);
        Reload();
        // move on to the next question that still needs one, so a stack of pictures can be attached quickly
        var next = (QTable.ItemsSource as IEnumerable<QuestionRow>)?.FirstOrDefault(x => x.Picture == "Needed");
        if (next != null) QTable.SelectedItem = next;
    }

    private void Move(int dir)
    {
        if (SelectedQ == null) return;
        var id = SelectedQ.Id;
        var r = App.Exams.MoveQuestion(_id, id, dir);
        if (!r.Ok) Ui.Error(r.Error!);
        Reload();
        QTable.SelectedItem = (QTable.ItemsSource as IEnumerable<QuestionRow>)?.FirstOrDefault(x => x.Id == id);
    }

    private void Up_Click(object sender, RoutedEventArgs e) => Move(-1);
    private void Down_Click(object sender, RoutedEventArgs e) => Move(1);

    private void DelQ_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedQ == null || !Ui.Confirm("Delete this question?")) return;
        var r = App.Exams.DeleteQuestion(_id, SelectedQ.Id);
        if (!r.Ok) Ui.Error(r.Error!);
        Reload();
    }

    private void Mark_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedR == null) return;
        var sheet = App.Marking.Sheet(SelectedR.AttemptId);
        if (sheet == null) { Reload(); return; }
        if (new MarkingDialog(sheet) { Owner = System.Windows.Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var rows = (RTable.ItemsSource as IEnumerable<ResultRow>)?.ToList() ?? new();
        if (rows.Count == 0) { Ui.Info("There are no submissions to export yet."); return; }
        var safe = string.Concat(_exam!.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var dlg = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = $"{safe} results.csv" };
        if (dlg.ShowDialog() != true) return;
        var sb = new StringBuilder("Student,Student ID,Objective,Theory,Total,Percent,Outcome,Submitted\r\n");
        foreach (var r in rows) sb.Append(string.Join(",", new[] { r.Student, r.StudentId, r.Objective, r.Theory, r.Score, r.Percent, r.Outcome, r.Submitted }.Select(Csv))).Append("\r\n");
        try { File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true)); Ui.Info("Results exported."); }
        catch (Exception ex) { Ui.Error("Could not save the file:\n" + ex.Message); }
    }
}
