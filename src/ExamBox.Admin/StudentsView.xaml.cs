using System.Windows;
using System.Windows.Controls;

namespace ExamBox.Admin;

public partial class StudentsView : UserControl
{
    public sealed record StudentRow(int Id, string Name, string StudentId, string Department, string Email, string Status, int Exams, string LastLogin);

    private readonly bool _openAdd;

    public StudentsView(bool openAdd = false)
    {
        _openAdd = openAdd;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Reload();
            if (_openAdd) Add_Click(this, new RoutedEventArgs());
        };
        UpdateButtons();
    }

    private StudentRow? Selected => Table.SelectedItem as StudentRow;

    private void Reload()
    {
        var q = Search.Text;
        var list = App.Students.List(q);
        Table.ItemsSource = list.Select(s => new StudentRow(s.Id, s.FullName, s.Username, s.Department ?? "—", s.Email ?? "—",
            s.IsActive ? "Active" : "Inactive", s.Attempts.Count(a => a.SubmittedAt != null), Ui.Local(s.LastLoginAt, "Never"))).ToList();
        Count.Text = $"{list.Count} student(s)";
        Empty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Empty.Text = string.IsNullOrWhiteSpace(q) ? "No students yet.\nClick “Add student” to create the first one." : "No students match your search.";
        UpdateButtons();
    }

    private void UpdateButtons() =>
        EditBtn.IsEnabled = HistoryBtn.IsEnabled = ResetBtn.IsEnabled = DeleteBtn.IsEnabled = Selected != null;

    private void Search_Changed(object sender, TextChangedEventArgs e) { if (IsLoaded) Reload(); }
    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();
    private void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (Selected != null) Edit_Click(sender, e); }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new StudentDialog(null) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;
        Reload();
        if (dlg.Created != null)
            new TempPasswordDialog(dlg.Created.Student.FullName, dlg.Created.Student.Username, dlg.Created.TempPassword, isNew: true) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        var s = App.Students.Get(Selected.Id);
        if (s == null) { Reload(); return; }
        if (new StudentDialog(s) { Owner = Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        var s = App.Students.Get(Selected.Id);
        if (s == null) { Reload(); return; }
        new StudentHistoryDialog(s) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        if (!Ui.Confirm($"Reset the password for {Selected.Name}?\n\nTheir current password stops working immediately.")) return;
        var r = App.Students.ResetPassword(Selected.Id);
        if (!r.Ok) { Ui.Error(r.Error!); return; }
        new TempPasswordDialog(Selected.Name, Selected.StudentId, r.Value!, isNew: false) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        if (!Ui.Confirm($"Delete {Selected.Name} and all of their exam records?\n\nThis cannot be undone.")) return;
        var r = App.Students.Delete(Selected.Id);
        if (!r.Ok) Ui.Error(r.Error!);
        Reload();
    }
}
