using System.Text;
using System.Windows;
using System.Windows.Controls;
using ExamBox.Services;
using Microsoft.Win32;

namespace ExamBox.Admin;

/// <summary>Exports results for the chosen subjects, limited to the chosen classes (PDF by default).</summary>
public partial class ExportResultsDialog : Window
{
    public ExportResultsDialog()
    {
        InitializeComponent();
        foreach (var c in App.Classes.Names()) ClassList.Children.Add(new CheckBox { Content = c, IsChecked = true, Margin = new Thickness(0, 3, 0, 3) });
        foreach (var s in App.Reports.Subjects()) SubjectList.Children.Add(new CheckBox { Content = s, IsChecked = true, Margin = new Thickness(0, 3, 0, 3) });
    }

    private static List<string> Picked(Panel p) => p.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Content).ToList();
    private static void Toggle(Panel p) { var all = p.Children.OfType<CheckBox>().ToList(); var on = all.Any(c => c.IsChecked != true); foreach (var c in all) c.IsChecked = on; }
    private void AllClasses_Click(object sender, RoutedEventArgs e) => Toggle(ClassList);
    private void AllSubjects_Click(object sender, RoutedEventArgs e) => Toggle(SubjectList);

    private static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var classes = Picked(ClassList); var subjects = Picked(SubjectList);
        if (classes.Count == 0 || subjects.Count == 0) { ErrorText.Text = "Pick at least one class and one subject."; ErrorText.Visibility = Visibility.Visible; return; }
        var rows = App.Reports.ExportRows(null, null, classes, subjects);
        if (rows.Count == 0) { ErrorText.Text = "No submitted results match those classes and subjects."; ErrorText.Visibility = Visibility.Visible; return; }
        var title = "Results: " + string.Join(", ", subjects.Take(3)) + (subjects.Count > 3 ? "..." : "");
        var sub = "Classes: " + string.Join(", ", classes) + ", exported " + DateTime.Now.ToString("f");
        var stamp = DateTime.Now.ToString("yyyy-MM-dd");
        if (FmtPdf.IsChecked == true)
        {
            var pass = rows.Count(r => r.Outcome == "Pass");
            var pcts = rows.Select(r => double.TryParse(r.Percent.TrimEnd('%'), out var v) ? v : 0).ToList();
            var summary = new List<(string, string)> { ("Results", rows.Count.ToString()), ("Average", Math.Round(pcts.Average(), 1) + "%"), ("Pass rate", Math.Round(pass * 100.0 / rows.Count, 1) + "%"), ("Highest", pcts.Max() + "%") };
            ExportHelper.Save($"ExamBox results {stamp}.pdf", "PDF file (*.pdf)|*.pdf", () => ResultsExporter.Pdf(App.Settings.InstitutionName ?? "", title, sub, summary, rows, true), this);
        }
        else if (FmtXlsx.IsChecked == true)
            ExportHelper.Save($"ExamBox results {stamp}.xlsx", "Excel workbook (*.xlsx)|*.xlsx", () => ResultsExporter.Xlsx(title, rows, true), this);
        else
        {
            var dlg = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = $"ExamBox results {stamp}.csv" };
            if (dlg.ShowDialog(this) != true) return;
            var sb = new StringBuilder("Student,Student ID,Class,Exam,Total,Percent,Grade,Outcome,Submitted\r\n");
            foreach (var r in rows) sb.Append(string.Join(",", new[] { r.Student, r.StudentId, r.Class, r.Exam, r.Total, r.Percent, r.Grade, r.Outcome, r.Submitted }.Select(Csv))).Append("\r\n");
            try { System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true)); Ui.Info($"Exported {rows.Count} result(s)."); }
            catch (Exception ex) { Ui.Error("Could not save the file:\n" + ex.Message); return; }
        }
        DialogResult = true;
    }
}
