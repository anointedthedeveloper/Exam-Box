using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExamBox.Services;
using Microsoft.Win32;

namespace ExamBox.Admin;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        InstitutionName.Text = App.Settings.InstitutionName;
        DefaultDuration.Text = App.Settings.DefaultDurationMinutes.ToString();
        DefaultPass.Text = App.Settings.DefaultPassMark.ToString();
        DataPath.Text = App.Db.DataDir;
        var v = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
        AboutText.Text = $"ExamBox {v}\nTimed exams for your students, everything runs on this computer.";
        Loaded += (_, _) => { App.Portal.Changed += RenderPortal; RenderPortal(); };
        Unloaded += (_, _) => App.Portal.Changed -= RenderPortal;
    }

    private void RenderPortal()
    {
        var running = App.Portal.Running;
        PortalDot.Fill = (Brush)FindResource(running ? "OkBrush" : "ErrBrush");
        PortalText.Text = running ? $"Running · {PortalHost.ReachableUrls(App.Portal.Port).First()}" : (App.Portal.Error ?? "Stopped");
    }

    private void Fail(string msg) { SavedText.Visibility = Visibility.Collapsed; ErrorText.Text = msg; ErrorText.Visibility = Visibility.Visible; }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        if (!Ui.TryInt(DefaultDuration.Text, out var dur) || dur is < 1 or > 600) { Fail("Default duration must be between 1 and 600 minutes."); return; }
        if (!Ui.TryInt(DefaultPass.Text, out var pass) || pass is < 1 or > 100) { Fail("Default pass mark must be between 1 and 100."); return; }

        var name = InstitutionName.Text.Trim();
        var nameChanged = !string.Equals(name, App.Settings.InstitutionName, StringComparison.Ordinal);
        App.Settings.InstitutionName = name;
        App.Settings.DefaultDurationMinutes = dur;
        App.Settings.DefaultPassMark = pass;
        App.Settings.Save();

        if (nameChanged && App.Portal.Running) await App.Portal.StartAsync(App.Settings.Port);   // restart so the website shows the new name
        SavedText.Text = "  Saved";
        SavedText.Visibility = Visibility.Visible;
    }

    private void Portal_Click(object sender, RoutedEventArgs e) => MainWindow.Current.Go("server");

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "ExamBox backup (*.db)|*.db", FileName = $"ExamBox backup {DateTime.Now:yyyy-MM-dd}.db" };
        if (dlg.ShowDialog() != true) return;
        try { App.Db.BackupTo(dlg.FileName); Ui.Info("Backup saved:\n" + dlg.FileName); }
        catch (Exception ex) { Ui.Error("Could not create the backup:\n" + ex.Message); }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(App.Db.DataDir) { UseShellExecute = true }); }
        catch { Ui.Error("Could not open the folder."); }
    }
}
