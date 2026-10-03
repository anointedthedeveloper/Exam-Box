using System.Windows;
using System.Windows.Controls;
using ExamBox.Models;

namespace ExamBox.Admin;

public partial class QuestionDialog : Window
{
    private readonly int _examId;
    private readonly Question? _existing;

    public QuestionDialog(int examId, Question? existing)
    {
        _examId = examId;
        _existing = existing;
        InitializeComponent();
        Title = Heading.Text = existing == null ? "Add question" : "Edit question";
        SaveBtn.Content = existing == null ? "Add question" : "Save question";
        if (existing != null)
        {
            QText.Text = existing.Text; OptA.Text = existing.OptionA; OptB.Text = existing.OptionB;
            OptC.Text = existing.OptionC; OptD.Text = existing.OptionD; Marks.Text = existing.Marks.ToString();
            Correct.SelectedIndex = Math.Max(0, "ABCD".IndexOf(existing.CorrectOption, StringComparison.Ordinal));
        }
        Loaded += (_, _) => QText.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Ui.TryInt(Marks.Text, out var marks)) { ErrorText.Text = "Marks must be a whole number."; ErrorText.Visibility = Visibility.Visible; return; }
        var correct = ((ComboBoxItem)Correct.SelectedItem).Content?.ToString() ?? "A";
        var r = App.Exams.SaveQuestion(_examId, _existing?.Id ?? 0, QText.Text, OptA.Text, OptB.Text, OptC.Text, OptD.Text, correct, marks);
        if (!r.Ok) { ErrorText.Text = r.Error; ErrorText.Visibility = Visibility.Visible; return; }
        DialogResult = true;
    }
}
