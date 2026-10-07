using System.IO;
using System.Windows;
using System.Windows.Media;
using ExamBox.Services;
using Microsoft.Win32;

namespace ExamBox.Admin;

public partial class StudentImportDialog : Window
{
    public sealed record IssueLine(string Text, Brush Color);

    private List<ImportedStudent> _rows = new();
    private List<CreatedStudent> _created = new();

    public StudentImportDialog()
    {
        InitializeComponent();
        Title = Heading.Text = "Import students from Excel";
        Sub.Text = "List each student's ID and name (and class if you like). ExamBox creates a temporary password for everyone and gives you a sheet with the logins.";
    }

    private void Template_Click(object sender, RoutedEventArgs e) =>
        Templates.Save("ExamBox student template.xlsx", StudentImporter.BuildTemplate(), this);

    private void Pick_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx" };
        if (dlg.ShowDialog(this) != true) return;
        FilePath.Text = dlg.FileName;
        ErrorText.Visibility = Visibility.Collapsed;
        List<ImportIssue> issues;
        try
        {
            using var fs = new FileStream(dlg.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            (_rows, issues) = StudentImporter.Parse(fs);
        }
        catch (Exception ex) { ErrorText.Text = "Could not read that file: " + ex.Message; ErrorText.Visibility = Visibility.Visible; return; }
        PreviewBox.Visibility = Visibility.Visible;
        PreviewTitle.Text = _rows.Count > 0 ? $"{_rows.Count} student(s) found" : "No students found";
        PreviewTitle.Foreground = (Brush)FindResource(_rows.Count > 0 ? "OkBrush" : "ErrBrush");
        PreviewSub.Text = "Students whose ID already exists are skipped.";
        IssueList.ItemsSource = issues.Select(i => new IssueLine(i.Message, (Brush)FindResource("ErrBrush"))).ToList();
        SaveBtn.IsEnabled = _rows.Count > 0;
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var res = App.Students.CreateMany(_rows, string.IsNullOrWhiteSpace(DefaultClass.Text) ? null : DefaultClass.Text.Trim());
        _created = res.Created;
        PreviewTitle.Text = $"{res.Created.Count} student(s) added" + (res.Skipped.Count > 0 ? $", {res.Skipped.Count} skipped" : "");
        PreviewTitle.Foreground = (Brush)FindResource(res.Created.Count > 0 ? "OkBrush" : "WarnBrush");
        PreviewSub.Text = res.Created.Count > 0 ? "Save the login sheet now: temporary passwords are not shown again." : "";
        IssueList.ItemsSource = res.Skipped.Select(i => new IssueLine($"Row {i.Row}: {i.Message}", (Brush)FindResource("WarnBrush"))).ToList();
        SaveBtn.Visibility = Visibility.Collapsed;
        CloseBtn.Content = "Done"; CloseBtn.IsCancel = false; CloseBtn.Click += (_, _) => DialogResult = _created.Count > 0;
        LoginsBtn.Visibility = res.Created.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (res.Created.Count > 0) Logins_Click(this, new RoutedEventArgs());
    }

    private void Logins_Click(object sender, RoutedEventArgs e)
    {
        var url = App.Portal.Running ? PortalHost.ReachableUrls(App.Portal.Port).First() : "";
        Templates.Save("ExamBox student logins.xlsx", StudentImporter.CredentialSheet(_created, url), this);
    }
}
