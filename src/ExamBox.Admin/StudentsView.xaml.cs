using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExamBox.Models;

namespace ExamBox.Admin;

public partial class StudentsView : UserControl
{
    public sealed record StudentRow(int Id, string Name, string Initial, Brush AvatarBg, Brush AvatarFg, string StudentId, string Department,
        string Email, string Status, int Exams, string Avg, string LastLogin);

    private readonly bool _openAdd;
    private List<User> _all = new();
    private bool _loading;

    public StudentsView(bool openAdd = false)
    {
        _openAdd = openAdd;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Reload();
            if (_openAdd) Add_Click(this, new RoutedEventArgs());
        };
    }

    private StudentRow? Selected => Table.SelectedItem as StudentRow;
    private static StudentRow? RowOf(object sender) => (sender as FrameworkElement)?.DataContext as StudentRow;

    /// <summary>Loads everyone once; search and the status filter then work in memory.</summary>
    private void Reload()
    {
        _all = App.Students.List();
        _loading = true;
        var keep = ClassFilter.SelectedItem as string;
        ClassFilter.Items.Clear(); ClassFilter.Items.Add("All classes");
        foreach (var c in _all.Select(s => s.Department).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().OrderBy(d => d)) ClassFilter.Items.Add(c);
        ClassFilter.SelectedItem = keep != null && ClassFilter.Items.Contains(keep) ? keep : "All classes";
        _loading = false;
        StatTotal.Text = _all.Count.ToString();
        StatActive.Text = _all.Count(s => s.IsActive).ToString();
        StatInactive.Text = _all.Count(s => !s.IsActive).ToString();
        StatNever.Text = _all.Count(s => s.LastLoginAt == null).ToString();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = Search.Text.Trim();
        IEnumerable<User> rows = _all;
        if (FilterActive.IsChecked == true) rows = rows.Where(s => s.IsActive);
        else if (FilterInactive.IsChecked == true) rows = rows.Where(s => !s.IsActive);
        if (ClassFilter.SelectedItem is string cf && cf != "All classes") rows = rows.Where(s => string.Equals(s.Department, cf, StringComparison.OrdinalIgnoreCase));
        if (q.Length > 0)
            rows = rows.Where(s => s.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) || s.Username.Contains(q, StringComparison.OrdinalIgnoreCase)
                                   || (s.Email ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) || (s.Department ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        var list = rows.ToList();

        Table.ItemsSource = list.Select(s =>
        {
            var done = s.Attempts.Where(a => a.SubmittedAt != null).ToList();
            var (bg, fg) = Palette.Avatar(s.FullName);
            return new StudentRow(s.Id, s.FullName, s.FullName.Length > 0 ? s.FullName[..1].ToUpperInvariant() : "?", bg, fg, s.Username,
                s.Department ?? "-", s.Email ?? "No email", s.IsActive ? "Active" : "Inactive", done.Count,
                done.Count == 0 ? "-" : $"{Math.Round(done.Average(a => a.Percent), 1)}%", Ui.Ago(s.LastLoginAt));
        }).ToList();

        Count.Text = _all.Count == 0 ? "" : $"Showing {list.Count} of {_all.Count}";
        var empty = list.Count == 0;
        Empty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        TableCard.MinHeight = empty ? 380 : 0;
        Table.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        var filtering = _all.Count > 0;
        EmptyTitle.Text = filtering ? "No matches" : "No students yet";
        EmptyText.Text = filtering ? "No students match your search or filter." : "Click “Add student” to create the first one.";
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = Search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (IsLoaded) ApplyFilter();
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => ApplyFilter();

    private void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Selected != null) Edit(Selected);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new StudentDialog(null) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;
        Reload();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (new StudentImportDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void Edit(StudentRow row)
    {
        var s = App.Students.Get(row.Id);
        if (s == null) { Reload(); return; }
        if (new StudentDialog(s) { Owner = Window.GetWindow(this) }.ShowDialog() == true) Reload();
    }

    private void Edit_Click(object sender, RoutedEventArgs e) { if (RowOf(sender) is { } r) Edit(r); }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        var s = App.Students.Get(row.Id);
        if (s == null) { Reload(); return; }
        new StudentHistoryDialog(s) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        if (new PasswordDialog(row.Id, row.Name, row.StudentId) { Owner = Window.GetWindow(this) }.ShowDialog() == true) Ui.Info($"New password set for {row.Name}.");
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (MoreBtn.ContextMenu == null) return;
        MoreBtn.ContextMenu.PlacementTarget = MoreBtn;
        MoreBtn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        MoreBtn.ContextMenu.IsOpen = true;
    }

    private void Template_Click(object sender, RoutedEventArgs e) =>
        ExportHelper.Save("ExamBox student template.xlsx", "Excel workbook (*.xlsx)|*.xlsx", ExamBox.Services.StudentImporter.BuildTemplate, Window.GetWindow(this));

    private void ClassFilter_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded && !_loading) ApplyFilter(); }

    private void ForceOut_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        if (!Ui.Confirm($"Sign {row.Name} out everywhere?\n\nThey are sent to the sign-in page right away on every device. Answers they already typed in an exam are kept.")) return;
        var r = App.Students.ForceLogout(row.Id);
        if (!r.Ok) { Ui.Error(r.Error!); return; }
        Ui.Info($"{row.Name} has been signed out.");
    }

    private void ForceAll_Click(object sender, RoutedEventArgs e)
    {
        if (!Ui.Confirm("Sign out ALL students on every device?\n\nEveryone, including students in the middle of an exam, is sent to the sign-in page and must sign in again. Answers already typed are kept.")) return;
        var n = App.Students.ForceLogoutAll();
        Ui.Info($"All {n} student(s) have been signed out.");
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        if (!Ui.Confirm($"Delete {row.Name} and all of their exam records?\n\nThis cannot be undone.")) return;
        var r = App.Students.Delete(row.Id);
        if (!r.Ok) Ui.Error(r.Error!);
        Reload();
    }
}
