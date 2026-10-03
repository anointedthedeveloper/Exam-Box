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
            Sub.Text = "A temporary password is generated. The student sets their own at first sign-in.";
        }
        else
        {
            Title = "Edit student"; Heading.Text = "Edit student"; SaveBtn.Content = "Save changes";
            Sub.Visibility = Visibility.Collapsed;
            FullName.Text = existing.FullName; StudentId.Text = existing.Username;
            Department.Text = existing.Department; Email.Text = existing.Email; Active.IsChecked = existing.IsActive;
        }
        Loaded += (_, _) => FullName.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var input = new StudentInput(FullName.Text, StudentId.Text, Email.Text, Department.Text, Active.IsChecked == true);
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
