using System.Windows;
using System.Windows.Controls;
using ExamBox.Models;

namespace ExamBox.Admin;

public partial class AuthView : UserControl
{
    private readonly bool _setup;

    public AuthView()
    {
        InitializeComponent();
        _setup = !App.Auth.HasAdmin();
        Heading.Text = _setup ? "Welcome to ExamBox" : "Administrator sign in";
        SubHeading.Text = _setup
            ? "Create the first administrator account to get started."
            : "Sign in to manage students and exams.";
        NamePanel.Visibility = ConfirmPanel.Visibility = _setup ? Visibility.Visible : Visibility.Collapsed;
        Submit.Content = _setup ? "Create administrator" : "Sign in";
        Loaded += (_, _) => (_setup ? FullName : Username).Focus();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        var username = Username.Text.Trim();
        var password = Password.Password;

        if (_setup)
        {
            if (password != Confirm.Password) { ShowError("Passwords do not match."); return; }
            var created = App.Auth.CreateAdmin(FullName.Text, username, Email.Text, password);
            if (!created.Ok) { ShowError(created.Error!); return; }
        }

        var r = App.Auth.Authenticate(username, password, UserRole.Admin);
        if (r.User == null) { ShowError(r.Error ?? "Sign-in failed."); return; }
        MainWindow.Current.SignedIn(r.User);
    }
}
