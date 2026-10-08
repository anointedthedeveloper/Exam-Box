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
            PassLabel.Text = "New password (leave empty to keep the current one)";
            PassHint.Text = "The student simply uses the new password the next time they sign in.";
            FullName.Text = existing.FullName; StudentId.Text = existing.Username;
            Department.Text = existing.Department; Email.Text = existing.Email; Active.IsChecked = existing.IsActive;
        }
        Loaded += (_, _) => FullName.Focus();
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => Pass.Text = ExamBox.Services.Passwords.Generate();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var input = new StudentInput(FullName.Text, StudentId.Text, Email.Text, Department.Text, Active.IsChecked == true, Pass.Text);
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
