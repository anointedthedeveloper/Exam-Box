using System.Windows;
using System.Windows.Controls;

namespace ExamBox.Admin;

public partial class AccountView : UserControl
{
    public AccountView()
    {
        InitializeComponent();
        Who.Text = $"{App.CurrentUser!.FullName} · {App.CurrentUser.Username}";
        DataPath.Text = App.Db.DataDir;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        if (New.Password != Confirm.Password) { ErrorText.Text = "Passwords do not match."; ErrorText.Visibility = Visibility.Visible; return; }
        var r = App.Auth.ChangePassword(App.CurrentUser!.Id, Current.Password, New.Password);
        if (!r.Ok) { ErrorText.Text = r.Error; ErrorText.Visibility = Visibility.Visible; return; }
        Current.Clear(); New.Clear(); Confirm.Clear();
        Ui.Info("Password updated.");
    }
}
