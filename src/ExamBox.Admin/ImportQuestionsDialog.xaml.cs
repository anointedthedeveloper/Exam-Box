using System.IO;
using System.Windows;
using System.Windows.Media;
using ExamBox.Services;
using Microsoft.Win32;

namespace ExamBox.Admin;

public partial class ImportQuestionsDialog : Window
{
    public sealed record IssueLine(string Text, Brush Color);

    private readonly int _examId;
    private ImportResult? _result;

    public ImportQuestionsDialog(int examId, bool hasQuestions)
    {
        _examId = examId;
        InitializeComponent();
        Title = Heading.Text = "Import questions from Excel";
        Sub.Text = "Fill in the template (one question per row, OBJ or THEORY), save it as .xlsx and choose it below. Nothing is added until the preview looks right.";
        ModeRow.Visibility = hasQuestions ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Template_Click(object sender, RoutedEventArgs e) =>
        Templates.Save("ExamBox question template.xlsx", QuestionImporter.BuildTemplate(), this);

    private void Pick_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx" };
        if (dlg.ShowDialog(this) != true) return;
        FilePath.Text = dlg.FileName;
        Preview();
    }

    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Folder that holds the question pictures" };
        if (dlg.ShowDialog(this) != true) return;
        FolderPath.Text = dlg.FolderName;
        if (FilePath.Text.Length > 0) Preview();
    }

    private void Preview()
    {
        ErrorText.Visibility = Visibility.Collapsed;
        _result = null; SaveBtn.IsEnabled = false;
        try
        {
            using var fs = new FileStream(FilePath.Text, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _result = QuestionImporter.Parse(fs, FolderPath.Text.Length > 0 ? FolderPath.Text : null);
        }
        catch (Exception ex)
        {
            ErrorText.Text = "Could not read that file: " + ex.Message; ErrorText.Visibility = Visibility.Visible; return;
        }
        var r = _result;
        PreviewBox.Visibility = Visibility.Visible;
        var errors = r.Issues.Count(i => !i.IsWarning);
        PreviewTitle.Text = errors > 0
            ? $"{errors} row(s) need fixing before you can import"
            : $"{r.Questions.Count} question(s) ready: {r.Objective} objective, {r.Theory} theory";
        PreviewTitle.Foreground = (Brush)FindResource(errors > 0 ? "ErrBrush" : "OkBrush");
        PreviewSub.Text = errors > 0 ? "Fix the rows in Excel, save, and choose the file again." :
            r.NeedImages > 0 ? $"{r.NeedImages} question(s) still need a picture. You will attach them one by one after importing." : "Everything looks good.";
        IssueList.ItemsSource = r.Issues.OrderBy(i => i.IsWarning).ThenBy(i => i.Row)
            .Select(i => new IssueLine((i.Row > 0 ? $"Row {i.Row}: " : "") + i.Message, (Brush)FindResource(i.IsWarning ? "WarnBrush" : "ErrBrush"))).ToList();
        SaveBtn.IsEnabled = errors == 0 && r.Questions.Count > 0;
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_result == null) return;
        var replace = ModeRow.Visibility == Visibility.Visible && ModeReplace.IsChecked == true;
        if (replace && !Ui.Confirm("Replace all current questions in this exam with the imported ones?")) return;
        var r = App.Exams.AddQuestions(_examId, _result.Questions, replace);
        if (!r.Ok) { ErrorText.Text = r.Error; ErrorText.Visibility = Visibility.Visible; return; }
        DialogResult = true;
    }
}

internal static class Templates
{
    public static void Save(string fileName, byte[] bytes, Window owner)
    {
        var dlg = new SaveFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx", FileName = fileName };
        if (dlg.ShowDialog(owner) != true) return;
        try { File.WriteAllBytes(dlg.FileName, bytes); Ui.Info("Saved. Open it in Excel, fill it in, then import it."); }
        catch (Exception ex) { Ui.Error("Could not save the file:\n" + ex.Message); }
    }
}
