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
        ErrorBox.Visibility = Visibility.Collapsed;
        if (New.Password != Confirm.Password) { ErrorText.Text = "Passwords do not match."; ErrorBox.Visibility = Visibility.Visible; return; }
        var r = App.Auth.ChangePassword(App.CurrentUser!.Id, Current.Password, New.Password);
        if (!r.Ok) { ErrorText.Text = r.Error; ErrorBox.Visibility = Visibility.Visible; return; }
        Current.Clear(); New.Clear(); Confirm.Clear();
        Ui.Info("Password updated.");
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(App.Db.DataDir) { UseShellExecute = true }); }
        catch { Ui.Error("Could not open the folder."); }
    }
}
