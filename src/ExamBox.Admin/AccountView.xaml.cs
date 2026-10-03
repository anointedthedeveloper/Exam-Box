using System.Windows;
using System.Windows.Controls;

namespace ExamBox.Admin;

public partial class AccountView : UserControl
{
    public AccountView()
    {
        InitializeComponent();
        Who.Text = $"Signed in as {App.CurrentUser!.Username}";
        DataPath.Text = App.Db.DataDir;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        if (New.Password != Confirm.Password) { ErrorText.Text = "Passwords do not match."; ErrorText.Visibility = Visibility.Visible; return; }
        var r = App.Auth.ChangePassword(App.CurrentUser!.Id, Current.Password, New.Password);
        if (!r.Ok) { ErrorText.Text = r.Error; ErrorText.Visibility = Visibility.Visible; return; }
        if (App.Settings.RememberedSecret != null) { App.Settings.RememberedSecret = Secrets.Protect(New.Password); App.Settings.Save(); }
        Current.Clear(); New.Clear(); Confirm.Clear();
        Ui.Info("Password updated.");
    }
}
