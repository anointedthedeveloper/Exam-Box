using System.Windows;
using System.Windows.Controls;

namespace ExamBox.Admin;

public partial class ClassesView : UserControl
{
    public sealed record ClassVm(int Id, string Name, int Students, int Exams, string Created);

    public ClassesView()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
    }

    private static ClassVm? RowOf(object sender) => (sender as FrameworkElement)?.DataContext as ClassVm;

    private List<ExamBox.Services.ClassRow> _all = new();

    private void Reload()
    {
        _all = App.Classes.List();
        StatClasses.Text = _all.Count.ToString();
        StatPlaced.Text = _all.Sum(c => c.Students).ToString();
        StatNone.Text = App.Classes.Unassigned().ToString();
        StatExams.Text = _all.Sum(c => c.Exams).ToString();
        ApplyFilter();
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = Search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (IsLoaded) ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = Search.Text.Trim();
        var list = q.Length == 0 ? _all : _all.Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        Table.ItemsSource = list.Select(c => new ClassVm(c.Id, c.Name, c.Students, c.Exams, Ui.Ago(c.CreatedUtc))).ToList();
        Empty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Table.Visibility = list.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        TableCard.MinHeight = list.Count == 0 ? 340 : 0;
        Count.Text = _all.Count == 0 ? "" : $"Showing {list.Count} of {_all.Count}";
        Sub.Text = "Group your students and target exams at a class. Students and exams use these names.";
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (new ClassDialog(null) { Owner = Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void Edit(ClassVm? row)
    {
        if (row == null) return;
        if (new ClassDialog(row) { Owner = Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void Rename_Click(object sender, RoutedEventArgs e) => Edit(RowOf(sender));
    private void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => Edit(Table.SelectedItem as ClassVm);

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        if (!Ui.Confirm($"Delete the class {row.Name}?" + (row.Exams > 0 ? $"\n\n{row.Exams} exam(s) for this class become open to every student." : ""))) return;
        var r = App.Classes.Delete(row.Id);
        if (!r.Ok) { Ui.Error(r.Error!); return; }
        App.Log.Write("admin", "Admin", "Class deleted", row.Name);
        Reload();
    }
}
