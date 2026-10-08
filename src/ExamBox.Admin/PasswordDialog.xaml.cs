using System.Windows;
using ExamBox.Services;

namespace ExamBox.Admin;

/// <summary>The admin picks (or generates) a new password for a student.</summary>
public partial class PasswordDialog : Window
{
    private readonly int _studentId;

    public PasswordDialog(int studentId, string name, string loginId)
    {
        _studentId = studentId;
        InitializeComponent();
        Title = Heading.Text = "Set a new password";
        Sub.Text = $"{name} (student ID {loginId}). Their old password stops working immediately. Write the new one down: it is not shown again.";
        Pass.Text = Passwords.Generate();
        Loaded += (_, _) => { Pass.Focus(); Pass.SelectAll(); };
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => Pass.Text = Passwords.Generate();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(Pass.Text); CopyBtn.Content = "Copied"; } catch { /* clipboard busy */ }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(Pass.Text)) { ErrorText.Text = "Enter a password or click Generate."; ErrorText.Visibility = Visibility.Visible; return; }
        var r = App.Students.ResetPassword(_studentId, Pass.Text);
        if (!r.Ok) { ErrorText.Text = r.Error; ErrorText.Visibility = Visibility.Visible; return; }
        DialogResult = true;
    }
}
