using System.Windows;
using System.Windows.Controls;

namespace ExamBox.Admin;

public partial class AccountView : UserControl
{
    public AccountView()
    {
        InitializeComponent();
        var u = App.CurrentUser!;
        Who.Text = u.Username;
        Initial.Text = u.Username.Length > 0 ? u.Username[..1].ToUpperInvariant() : "?";
        DataPath.Text = App.Db.DataDir;
        LastIn.Text = "Last sign-in: " + Ui.Ago(u.LastLoginAt, "this session");
        Since.Text = "Account created: " + Ui.Local(u.CreatedAt);
        Loaded += (_, _) =>
        {
            var d = App.Dashboard.Get(0);
            StatStudents.Text = d.Students.ToString(); StatExams.Text = d.Exams.ToString(); StatMarking.Text = d.AwaitingMarking.ToString();
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorBox.Visibility = Visibility.Collapsed;
        if (New.Password != Confirm.Password) { ErrorText.Text = "Passwords do not match."; ErrorBox.Visibility = Visibility.Visible; return; }
        var r = App.Auth.ChangePassword(App.CurrentUser!.Id, Current.Password, New.Password);
        if (!r.Ok) { ErrorText.Text = r.Error; ErrorBox.Visibility = Visibility.Visible; return; }
        if (App.Settings.RememberedSecret != null) { App.Settings.RememberedSecret = Secrets.Protect(New.Password); App.Settings.Save(); }
        Current.Clear(); New.Clear(); Confirm.Clear();
        Ui.Info("Password updated.");
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => MainWindow.Current.Go("settings");
    private void SignOut_Click(object sender, RoutedEventArgs e) => MainWindow.Current.SignOutNow();

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(App.Db.DataDir) { UseShellExecute = true }); }
        catch { Ui.Error("Could not open the folder."); }
    }
}
