using System.Windows;
using System.Windows.Controls;
using ExamBox.Models;

namespace ExamBox.Admin;

public partial class AuthView : UserControl
{
    private readonly bool _setup;
    private bool _autoTried;

    public AuthView()
    {
        InitializeComponent();
        _setup = !App.Auth.HasAdmin();
        Heading.Text = _setup ? "Welcome to ExamBox" : "Administrator sign in";
        SubHeading.Text = _setup
            ? "Create your administrator account to get started."
            : "Sign in to manage students and exams.";
        ConfirmPanel.Visibility = _setup ? Visibility.Visible : Visibility.Collapsed;
        Submit.Content = _setup ? "Create account" : "Sign in";

        if (!_setup && App.Settings.RememberedUser != null)
        {
            Username.Text = App.Settings.RememberedUser;
            RememberMe.IsChecked = true;
        }

        Loaded += (_, _) =>
        {
            if (!_setup && !_autoTried && TryAutoSignIn()) return;
            (string.IsNullOrEmpty(Username.Text) ? Username : (Control)Password).Focus();
        };
    }

    private string PasswordText => ShowPw.IsChecked == true ? PasswordVisible.Text : Password.Password;

    private void ShowPw_Changed(object sender, RoutedEventArgs e)
    {
        if (ShowPw.IsChecked == true)
        {
            PasswordVisible.Text = Password.Password;
            Password.Visibility = Visibility.Collapsed;
            PasswordVisible.Visibility = Visibility.Visible;
            PasswordVisible.Focus();
            PasswordVisible.CaretIndex = PasswordVisible.Text.Length;
        }
        else
        {
            Password.Password = PasswordVisible.Text;
            PasswordVisible.Visibility = Visibility.Collapsed;
            Password.Visibility = Visibility.Visible;
            Password.Focus();
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBox.Visibility = Visibility.Visible;
    }

    /// <summary>"Remember me": sign in automatically with the DPAPI-protected password from the last launch.</summary>
    private bool TryAutoSignIn()
    {
        _autoTried = true;
        var user = App.Settings.RememberedUser;
        var pw = Secrets.Unprotect(App.Settings.RememberedSecret);
        if (user == null || pw == null) return false;
        var r = App.Auth.Authenticate(user, pw, UserRole.Admin);
        if (r.User == null)
        {
            // Stored credentials no longer work (e.g. password changed): fall back to the normal form.
            App.Settings.RememberedSecret = null;
            App.Settings.Save();
            return false;
        }
        MainWindow.Current.SignedIn(r.User);
        return true;
    }

    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        ErrorBox.Visibility = Visibility.Collapsed;
        var username = Username.Text.Trim();
        var password = PasswordText;

        if (_setup)
        {
            if (password != Confirm.Password) { ShowError("Passwords do not match."); return; }
            var created = App.Auth.CreateAdmin(username, password);
            if (!created.Ok) { ShowError(created.Error!); return; }
        }

        var r = App.Auth.Authenticate(username, password, UserRole.Admin);
        if (r.User == null) { ShowError(r.Error ?? "Sign-in failed."); return; }

        if (RememberMe.IsChecked == true)
        {
            App.Settings.RememberedUser = r.User.Username;
            App.Settings.RememberedSecret = Secrets.Protect(password);
        }
        else
        {
            App.Settings.RememberedUser = null;
            App.Settings.RememberedSecret = null;
        }
        App.Settings.Save();
        MainWindow.Current.SignedIn(r.User);
    }
}
