using System.Windows;
using System.Windows.Controls;

namespace ExamBox.Admin;

public partial class ExamsView : UserControl
{
    public sealed record ExamRow(int Id, string Title, int Questions, string Duration, string PassMark, int Submissions, string Status);

    private readonly bool _openNew;

    public ExamsView(bool openNew = false)
    {
        _openNew = openNew;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Reload();
            if (_openNew) New_Click(this, new RoutedEventArgs());
        };
        OpenBtn.IsEnabled = false;
    }

    private ExamRow? Selected => Table.SelectedItem as ExamRow;

    private void Reload()
    {
        var list = App.Exams.List();
        Table.ItemsSource = list.Select(e => new ExamRow(e.Id, e.Title, e.Questions.Count, $"{e.DurationMinutes} min", $"{e.PassMarkPercent}%",
            e.Attempts.Count(a => a.SubmittedAt != null), e.IsPublished ? "Published" : "Draft")).ToList();
        Empty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Count.Text = list.Count == 0 ? "Create exams, add questions and publish them to students." : $"{list.Count} exam(s) · {list.Count(e => e.IsPublished)} published";
        OpenBtn.IsEnabled = Selected != null;
    }

    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e) => OpenBtn.IsEnabled = Selected != null;
    private void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (Selected != null) Open_Click(sender, e); }
    private void Open_Click(object sender, RoutedEventArgs e) { if (Selected != null) MainWindow.Current.OpenExam(Selected.Id); }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ExamDialog(null) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true && dlg.Saved != null) MainWindow.Current.OpenExam(dlg.Saved.Id);
    }
}
