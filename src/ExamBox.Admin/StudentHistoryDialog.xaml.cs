using System.Windows;
using ExamBox.Models;

namespace ExamBox.Admin;

public partial class StudentHistoryDialog : Window
{
    public sealed record Row(string Exam, string Result, string Outcome);

    public StudentHistoryDialog(User student)
    {
        InitializeComponent();
        Heading.Text = $"{student.FullName} · {student.Username}";
        var rows = student.Attempts.OrderByDescending(a => a.StartedAt).Select(a => a.SubmittedAt == null
            ? new Row(a.Exam!.Title, "In progress", "—")
            : new Row(a.Exam!.Title, $"{a.Score} / {a.TotalMarks} ({a.Percent}%)", a.Percent >= a.Exam.PassMarkPercent ? "Pass" : "Fail")).ToList();
        Table.ItemsSource = rows;
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
