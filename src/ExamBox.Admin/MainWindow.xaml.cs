using System.Windows;
using System.Windows.Controls;
using ExamBox.Models;

namespace ExamBox.Admin;

public partial class MainWindow : Window
{
    public static MainWindow Current { get; private set; } = null!;

    public MainWindow()
    {
        InitializeComponent();
        Current = this;
        // Maximise once the native window exists (setting it in XAML together with CenterScreen can size the
        // window to the whole monitor, hiding the bottom of the UI under the taskbar).
        SourceInitialized += (_, _) => WindowState = WindowState.Maximized;
        ShowAuth();
    }

    private void ShowAuth()
    {
        Shell.Visibility = Visibility.Collapsed;
        Page.Content = null;
        AuthHost.Content = new AuthView();
        AuthHost.Visibility = Visibility.Visible;
    }

    public void SignedIn(User user)
    {
        App.CurrentUser = user;
        UserName.Text = user.FullName;
        ReportsBadge.Visibility = App.Settings.SeenReports ? Visibility.Collapsed : Visibility.Visible;
        SettingsBadge.Visibility = App.Settings.SeenSettings ? Visibility.Collapsed : Visibility.Visible;
        UserInitial.Text = string.IsNullOrEmpty(user.FullName) ? "?" : user.FullName[..1].ToUpperInvariant();
        AuthHost.Content = null;
        AuthHost.Visibility = Visibility.Collapsed;
        Shell.Visibility = Visibility.Visible;
        Go("dashboard");
    }

    /// <summary>Navigate to a top-level page and highlight it in the sidebar.</summary>
    public void Go(string page, bool openAdd = false)
    {
        var (radio, view) = page switch
        {
            "students" => (NavStudents, (UserControl)new StudentsView(openAdd)),
            "exams" => (NavExams, new ExamsView(openAdd)),
            "reports" => (NavReports, new ReportsView()),
            "settings" => (NavSettings, new SettingsView()),
            "server" => (NavServer, new ServerView()),
            "account" => (NavAccount, new AccountView()),
            _ => (NavDashboard, new DashboardView()),
        };
        radio.IsChecked = true;
        if (page == "reports" && !App.Settings.SeenReports) { App.Settings.SeenReports = true; App.Settings.Save(); ReportsBadge.Visibility = Visibility.Collapsed; }
        if (page == "settings" && !App.Settings.SeenSettings) { App.Settings.SeenSettings = true; App.Settings.Save(); SettingsBadge.Visibility = Visibility.Collapsed; }
        ShowPage(view);
    }

    /// <summary>Swap the page with a short fade/slide-in.</summary>
    private void ShowPage(UserControl view)
    {
        Page.Content = view;
        var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260));
        view.RenderTransform = new System.Windows.Media.TranslateTransform(0, 14);
        view.BeginAnimation(UIElement.OpacityProperty, fade);
        view.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new System.Windows.Media.Animation.DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
    }

    /// <summary>Open one exam's detail page (keeps "Exams" highlighted).</summary>
    public void OpenExam(int id)
    {
        NavExams.IsChecked = true;
        ShowPage(new ExamDetailView(id));
    }

    private void Nav_Click(object sender, RoutedEventArgs e) => Go((string)((RadioButton)sender).Tag);

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        App.CurrentUser = null;
        ShowAuth();
    }
}
