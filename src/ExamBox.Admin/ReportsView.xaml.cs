using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExamBox.Services;
using Microsoft.Win32;

namespace ExamBox.Admin;

public partial class ReportsView : UserControl
{
    public sealed record ExamRowR(string Exam, int Attempts, string Average, double PassRate, string PassRateText, string High, string Low);
    public sealed record TopRow(int Rank, string Initial, string Name, string StudentId, int Exams, string Average);

    public ReportsView()
    {
        InitializeComponent();
        PassDot.Fill = Palette.Passed;
        FailDot.Fill = Palette.Failed;
        ExamFilter.Items.Add(new ComboBoxItem { Content = "All exams", Tag = null });
        foreach (var e in App.Exams.List()) ExamFilter.Items.Add(new ComboBoxItem { Content = e.Title, Tag = e.Id });
        ExamFilter.SelectedIndex = 0;
        Loaded += (_, _) => Reload();
    }

    private int? ExamId => (ExamFilter.SelectedItem as ComboBoxItem)?.Tag as int?;

    private DateTime? Since => PeriodFilter.SelectedIndex switch
    {
        0 => DateTime.UtcNow.AddDays(-30),
        1 => DateTime.UtcNow.AddDays(-90),
        2 => DateTime.UtcNow.AddDays(-365),
        _ => null,
    };

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) Reload(); }

    private void Reload()
    {
        var r = App.Reports.Build(ExamId, Since);
        var waiting = App.Marking.PendingCount();
        PendingBanner.Visibility = waiting > 0 ? Visibility.Visible : Visibility.Collapsed;
        PendingText.Text = $"{waiting} submission(s) are still awaiting theory marking and are not counted in these figures yet.";

        Anim.CountUp(KpiAttempts, r.Attempts);
        KpiAttemptsSub.Text = "submitted exams";
        if (r.Attempts == 0)
        {
            KpiAvg.Text = KpiPass.Text = KpiHigh.Text = "-";
            KpiAvgSub.Text = KpiPassSub.Text = KpiHighSub.Text = "no data yet";
        }
        else
        {
            Anim.CountUp(KpiAvg, r.AvgPercent, 1, "%");
            KpiAvgSub.Text = $"lowest {r.LowPercent}%";
            Anim.CountUp(KpiPass, r.PassRate, 1, "%");
            KpiPassSub.Text = "met the pass mark";
            Anim.CountUp(KpiHigh, r.HighPercent, 1, "%");
            KpiHighSub.Text = "best single result";
        }

        Donut.CenterTitle = r.Attempts.ToString();
        Donut.CenterSub = "submissions";
        Donut.Slices = Palette.GradeSlices(r.GradeCounts);
        Palette.BuildGradeLegend(Legend, r.GradeCounts, this);

        Trend.Labels = r.Trend.Select(m => m.Month.ToString("MMM")).ToArray();
        Trend.Series = new List<LineSeries>
        {
            new("Passed", r.Trend.Select(m => (double)m.Passed).ToArray(), Palette.Passed),
            new("Failed", r.Trend.Select(m => (double)m.Failed).ToArray(), Palette.Failed),
        };

        ExamsTable.ItemsSource = r.Exams.Select(e => new ExamRowR(e.Title, e.Attempts, $"{e.AvgPercent}%", e.PassRate, $"{e.PassRate}%", $"{e.HighPercent}%", $"{e.LowPercent}%")).ToList();
        ExamsEmpty.Visibility = r.Exams.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var rank = 0;
        TopTable.ItemsSource = r.TopStudents.Select(s => new TopRow(++rank, s.Name.Length > 0 ? s.Name[..1].ToUpperInvariant() : "?", s.Name, s.Username, s.Exams, $"{s.AvgPercent}%")).ToList();
        TopEmpty.Visibility = r.TopStudents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Marking_Click(object sender, RoutedEventArgs e) => MainWindow.Current.Go("marking");

    private static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var rows = App.Reports.Export(ExamId, Since);
        if (rows.Count == 0) { Ui.Info("There are no submissions to export for these filters."); return; }
        var dlg = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = $"ExamBox results {DateTime.Now:yyyy-MM-dd}.csv" };
        if (dlg.ShowDialog() != true) return;
        var sb = new StringBuilder("Student,Student ID,Exam,Score,Total marks,Percent,Outcome,Submitted\r\n");
        foreach (var r in rows)
            sb.Append(string.Join(",", new[] { r.Student, r.StudentId, r.Exam, r.Score.ToString(), r.TotalMarks.ToString(), r.Percent + "%", r.Passed ? "Pass" : "Fail", Ui.Local(r.SubmittedUtc) }.Select(Csv))).Append("\r\n");
        try { File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true)); Ui.Info($"Exported {rows.Count} result(s)."); }
        catch (Exception ex) { Ui.Error("Could not save the file:\n" + ex.Message); }
    }
}
