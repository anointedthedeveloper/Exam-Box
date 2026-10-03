using System.Windows;
using ExamBox.Models;

namespace ExamBox.Admin;

public partial class ExamDialog : Window
{
    private readonly Exam? _existing;
    public Exam? Saved { get; private set; }

    public ExamDialog(Exam? existing)
    {
        _existing = existing;
        InitializeComponent();
        Title = Heading.Text = existing == null ? "New exam" : "Edit exam";
        SaveBtn.Content = existing == null ? "Create exam" : "Save changes";
        if (existing != null)
        {
            ExamTitle.Text = existing.Title; Description.Text = existing.Description;
            Duration.Text = existing.DurationMinutes.ToString(); PassMark.Text = existing.PassMarkPercent.ToString();
        }
        Loaded += (_, _) => ExamTitle.Focus();
    }

    private void Fail(string msg) { ErrorText.Text = msg; ErrorText.Visibility = Visibility.Visible; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Ui.TryInt(Duration.Text, out var dur)) { Fail("Duration must be a whole number of minutes."); return; }
        if (!Ui.TryInt(PassMark.Text, out var pass)) { Fail("Pass mark must be a whole number between 1 and 100."); return; }
        var r = App.Exams.Save(_existing?.Id ?? 0, ExamTitle.Text, Description.Text, dur, pass);
        if (!r.Ok) { Fail(r.Error!); return; }
        Saved = r.Value;
        DialogResult = true;
    }
}
