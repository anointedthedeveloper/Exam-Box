using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ExamBox.Models;
using Microsoft.Win32;

namespace ExamBox.Admin;

/// <summary>After an import: asks for the picture of each question marked "yes", one after another.</summary>
public partial class AttachPicturesDialog : Window
{
    private readonly int _examId;
    private readonly List<(int Id, string Label, string Text)> _todo;
    private int _at;

    /// <summary>Opens the dialog only when the exam has questions still waiting for a picture.</summary>
    public static void RunFor(int examId, Window? owner)
    {
        var exam = App.Exams.Get(examId);
        if (exam == null) return;
        var todo = new List<(int, string, string)>();
        var n = 0;
        foreach (var q in exam.Questions.OrderBy(q => q.SortOrder))
        {
            n++;
            if (q.MissingImage) todo.Add((q.Id, string.IsNullOrWhiteSpace(q.Number) ? n.ToString() : q.Number!.Trim(), q.Text));
        }
        if (todo.Count == 0) return;
        new AttachPicturesDialog(examId, todo) { Owner = owner }.ShowDialog();
    }

    private AttachPicturesDialog(int examId, List<(int, string, string)> todo)
    {
        _examId = examId; _todo = todo;
        InitializeComponent();
        Title = "Attach question pictures";
        Bar.Maximum = todo.Count;
        Show_();
    }

    private void Show_()
    {
        if (_at >= _todo.Count) { DialogResult = true; return; }
        var t = _todo[_at];
        Heading.Text = $"Select question {t.Label} image";
        Progress.Text = $"Picture {_at + 1} of {_todo.Count}. The next question appears as soon as you pick this one.";
        Bar.Value = _at;
        QuestionText.Text = t.Text;
        Thumb.Visibility = Visibility.Collapsed;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Pictures (*.png;*.jpg;*.jpeg;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.webp", Title = $"Picture for question {_todo[_at].Label}" };
        if (dlg.ShowDialog(this) != true) return;
        byte[] bytes;
        try { bytes = File.ReadAllBytes(dlg.FileName); }
        catch (Exception ex) { ErrorText.Text = "Could not read that file: " + ex.Message; ErrorText.Visibility = Visibility.Visible; return; }
        var r = App.Exams.SetImage(_examId, _todo[_at].Id, bytes, "");
        if (!r.Ok) { ErrorText.Text = r.Error; ErrorText.Visibility = Visibility.Visible; return; }
        _at++;
        Show_();
    }

    private void Skip_Click(object sender, RoutedEventArgs e) { _at++; Show_(); }
}
