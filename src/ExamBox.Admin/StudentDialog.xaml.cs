using System.Windows;
using ExamBox.Models;
using ExamBox.Services;

namespace ExamBox.Admin;

public partial class StudentDialog : Window
{
    private readonly User? _existing;
    public CreatedStudent? Created { get; private set; }

    public StudentDialog(User? existing)
    {
        _existing = existing;
        InitializeComponent();
        if (existing == null)
        {
            Title = "Add student"; Heading.Text = "Add student"; SaveBtn.Content = "Create student";
            Sub.Text = "You choose the password. Give the student their ID and this password to sign in.";
            PassLabel.Text = "Password"; Pass.Text = ExamBox.Services.Passwords.Generate();
            PassHint.Text = "Type your own or use the generated one. Write it down: it is not shown again after you save.";
        }
        else
        {
            Title = "Edit student"; Heading.Text = "Edit student"; SaveBtn.Content = "Save changes";
            Sub.Visibility = Visibility.Collapsed;
            var known = App.Students.RevealPassword(existing.Id);
            PassLabel.Text = "Password"; Pass.Text = known ?? "";
            PassHint.Text = known != null ? "The student can see this on their My account page. Change it and save to give them a new one." : "Not stored for this older account. Type a new password to set one, or leave empty to keep the current one.";
            var parts = existing.FullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            FirstName.Text = parts.Length > 0 ? parts[0] : ""; LastName.Text = parts.Length > 1 ? parts[1] : ""; StudentId.Text = existing.Username;
            Email.Text = existing.Email; Active.IsChecked = existing.IsActive;
        }
        FillClasses(existing?.Department);
        Loaded += (_, _) => FirstName.Focus();
    }

    private const string NoClass = "No class";
    private string? SelectedClass => ClassBox.SelectedItem is string s && s != NoClass ? s : null;

    private void FillClasses(string? select)
    {
        ClassBox.Items.Clear(); ClassBox.Items.Add(NoClass);
        foreach (var c in App.Classes.Names()) ClassBox.Items.Add(c);
        var match = select == null ? null : ClassBox.Items.Cast<string>().FirstOrDefault(c => string.Equals(c, select, StringComparison.OrdinalIgnoreCase));
        ClassBox.SelectedItem = match ?? NoClass;
    }

    private void NewClass_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ClassDialog(null) { Owner = this };
        if (dlg.ShowDialog() == true) FillClasses(dlg.CreatedName);
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => Pass.Text = ExamBox.Services.Passwords.Generate();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var input = new StudentInput((FirstName.Text.Trim() + " " + LastName.Text.Trim()).Trim(), StudentId.Text, Email.Text, SelectedClass, Active.IsChecked == true, Pass.Text);
        string? error;
        if (_existing == null)
        {
            var r = App.Students.Create(input);
            Created = r.Value;
            error = r.Ok ? null : r.Error;
        }
        else
        {
            var r = App.Students.Update(_existing.Id, input);
            error = r.Ok ? null : r.Error;
        }
        if (error != null) { ErrorText.Text = error; ErrorText.Visibility = Visibility.Visible; return; }
        DialogResult = true;
    }
}
