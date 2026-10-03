using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExamBox.Services;

namespace ExamBox.Admin;

public partial class ServerView : UserControl
{
    private bool _loading = true;

    public ServerView()
    {
        InitializeComponent();
        PortBox.Text = App.Settings.Port.ToString();
        AutoStart.IsChecked = App.Settings.AutoStartServer;
        _loading = false;
        Loaded += (_, _) => { App.Portal.Changed += Render; Render(); };
        Unloaded += (_, _) => App.Portal.Changed -= Render;
    }

    private void Render()
    {
        var running = App.Portal.Running;
        StatusText.Text = running ? "Running" : "Stopped";
        StatusText.Foreground = StatusDot.Fill = (Brush)FindResource(running ? "OkBrush" : "ErrBrush");
        StatusPill.Background = (Brush)FindResource(running ? "OkBgBrush" : "ErrBgBrush");
        ToggleBtn.Content = running ? "Stop portal" : "Start portal";
        RestartBtn.IsEnabled = running;
        ErrorText.Visibility = App.Portal.Error != null ? Visibility.Visible : Visibility.Collapsed;
        ErrorText.Text = App.Portal.Error ?? "";
        var vis = running ? Visibility.Visible : Visibility.Collapsed;
        UrlHint.Visibility = Urls.Visibility = CopyBtn.Visibility = OpenBtn.Visibility = vis;
        Urls.Items.Clear();
        if (running)
        {
            foreach (var u in PortalHost.ReachableUrls(App.Portal.Port)) Urls.Items.Add(u);
            Urls.SelectedIndex = 0;
        }
    }

    private bool TryPort(out int port)
    {
        if (!Ui.TryInt(PortBox.Text, out port) || port is < 1 or > 65535)
        {
            Ui.Error("Enter a port number between 1 and 65535.");
            return false;
        }
        return true;
    }

    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (App.Portal.Running) { SetBusy(true, "Stopping…"); await App.Portal.StopAsync(); SetBusy(false); return; }
        if (!TryPort(out var port)) return;
        App.Settings.Port = port; App.Settings.Save();
        SetBusy(true, "Starting…");
        await App.Portal.StartAsync(port);
        SetBusy(false);
    }

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPort(out var port)) return;
        App.Settings.Port = port; App.Settings.Save();
        SetBusy(true, "Restarting…");
        await App.Portal.StartAsync(port);
        SetBusy(false);
    }

    private void SetBusy(bool on, string? text = null)
    {
        Busy.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        ToggleBtn.IsEnabled = RestartBtn.IsEnabled = !on;
        if (on && text != null) StatusText.Text = text;
        if (!on) Render();
    }

    private void Auto_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.AutoStartServer = AutoStart.IsChecked == true;
        App.Settings.Save();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (Urls.SelectedItem is not string u) return;
        try { Clipboard.SetText(u); CopyBtn.Content = "Copied"; } catch { Ui.Error("Could not access the clipboard."); }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var u = Urls.SelectedItem as string ?? $"http://localhost:{App.Portal.Port}";
        try { Process.Start(new ProcessStartInfo(u) { UseShellExecute = true }); } catch { Ui.Error("Could not open the browser."); }
    }
}
