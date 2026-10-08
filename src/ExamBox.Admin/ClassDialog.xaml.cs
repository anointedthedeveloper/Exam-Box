using System.Windows;

namespace ExamBox.Admin;

public partial class ClassDialog : Window
{
    private readonly ClassesView.ClassVm? _existing;
    public string? CreatedName { get; private set; }

    public ClassDialog(ClassesView.ClassVm? existing)
    {
        _existing = existing;
        InitializeComponent();
        Title = Heading.Text = existing == null ? "New class" : "Rename class";
        SaveBtn.Content = existing == null ? "Create class" : "Save name";
        Sub.Text = existing == null ? "For example SS1, JSS2 or Primary 4." : $"Students and exams in {existing.Name} follow the new name.";
        if (existing != null) NameBox.Text = existing.Name;
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string? error;
        if (_existing == null)
        {
            var r = App.Classes.Create(NameBox.Text);
            error = r.Ok ? null : r.Error;
            if (r.Ok) { CreatedName = r.Value!.Name; App.Log.Write("admin", "Admin", "Class created", CreatedName); }
        }
        else
        {
            var r = App.Classes.Rename(_existing.Id, NameBox.Text);
            error = r.Ok ? null : r.Error;
            if (r.Ok) App.Log.Write("admin", "Admin", "Class renamed", $"{_existing.Name} to {NameBox.Text.Trim()}");
        }
        if (error != null) { ErrorText.Text = error; ErrorText.Visibility = Visibility.Visible; return; }
        DialogResult = true;
    }
}
