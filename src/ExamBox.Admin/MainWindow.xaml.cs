using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        SizeChanged += (_, _) => ApplyCompact();
        NavItems.SizeChanged += (_, _) => MovePill(CurrentNav, animate: false);
        App.Portal.Changed += RenderPortalDot;
        ShowAuth();
    }

    private RadioButton[] NavButtons => new[] { NavDashboard, NavStudents, NavExams, NavReports, NavSettings, NavServer };
    private RadioButton? CurrentNav => NavButtons.FirstOrDefault(r => r.IsChecked == true);

    /// <summary>Narrow windows: icons only (labels move into tooltips), then hide the brand and profile text too.</summary>
    private void ApplyCompact()
    {
        var iconsOnly = ActualWidth < 1290;
        foreach (var r in NavButtons)
        {
            var kids = ((StackPanel)r.Content).Children;
            kids[1].Visibility = iconsOnly ? Visibility.Collapsed : Visibility.Visible;
            r.Padding = iconsOnly ? new Thickness(13, 10, 13, 10) : new Thickness(16, 10, 16, 10);
        }
        BrandText.Visibility = ProfileText.Visibility = ActualWidth < 1060 ? Visibility.Collapsed : Visibility.Visible;
        RefreshBadges(iconsOnly);
    }

    private void RefreshBadges(bool? iconsOnly = null)
    {
        var compact = iconsOnly ?? ActualWidth < 1290;
        ReportsBadge.Visibility = !compact && !App.Settings.SeenReports ? Visibility.Visible : Visibility.Collapsed;
        SettingsBadge.Visibility = !compact && !App.Settings.SeenSettings ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderPortalDot() =>
        PortalDot.Fill = (Brush)FindResource(App.Portal.Running ? "OkBrush" : "ErrBrush");

    /// <summary>Slides the white highlight under the active item.</summary>
    private void MovePill(RadioButton? target, bool animate = true)
    {
        if (target == null) { NavPill.Opacity = 0; return; }
        NavItems.UpdateLayout();
        var x = target.TranslatePoint(new Point(0, 0), NavItems).X;
        var w = target.ActualWidth;
        if (!animate || NavPill.Opacity == 0 || NavPill.Width == 0)
        {
            NavPill.BeginAnimation(WidthProperty, null); PillMove.BeginAnimation(TranslateTransform.XProperty, null);
            NavPill.Width = w; PillMove.X = x; NavPill.Opacity = 1;
            return;
        }
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var t = TimeSpan.FromMilliseconds(320);
        PillMove.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, t) { EasingFunction = ease });
        NavPill.BeginAnimation(WidthProperty, new DoubleAnimation(w, t) { EasingFunction = ease });
    }

    private void Profile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ProfilePopup.IsOpen) return;
        ProfilePopup.HorizontalOffset = ProfileChip.ActualWidth - 270;   // right-align the menu under the chip
        ProfilePopup.IsOpen = true;
    }

    private void MyAccount_Click(object sender, RoutedEventArgs e) { ProfilePopup.IsOpen = false; Go("account"); }

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
        var initial = string.IsNullOrEmpty(user.FullName) ? "?" : user.FullName[..1].ToUpperInvariant();
        UserName.Text = MenuName.Text = user.FullName;
        UserInitial.Text = MenuInitial.Text = initial;
        BrandName.Text = string.IsNullOrWhiteSpace(App.Settings.InstitutionName) ? "ExamBox" : App.Settings.InstitutionName;
        RenderPortalDot();
        AuthHost.Content = null;
        AuthHost.Visibility = Visibility.Collapsed;
        Shell.Visibility = Visibility.Visible;
        ApplyCompact();
        Go("dashboard");
    }

    /// <summary>Navigate to a top-level page and highlight it in the sidebar.</summary>
    public void Go(string page, bool openAdd = false)
    {
        var (radio, view) = page switch
        {
            "students" => ((RadioButton?)NavStudents, (UserControl)new StudentsView(openAdd)),
            "exams" => (NavExams, new ExamsView(openAdd)),
            "reports" => (NavReports, new ReportsView()),
            "settings" => (NavSettings, new SettingsView()),
            "server" => (NavServer, new ServerView()),
            "account" => ((RadioButton?)null, new AccountView()),
            _ => (NavDashboard, new DashboardView()),
        };
        foreach (var r in NavButtons) r.IsChecked = r == radio;
        MovePill(radio);
        if (page == "reports" && !App.Settings.SeenReports) { App.Settings.SeenReports = true; App.Settings.Save(); RefreshBadges(); }
        if (page == "settings" && !App.Settings.SeenSettings) { App.Settings.SeenSettings = true; App.Settings.Save(); RefreshBadges(); }
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
        foreach (var r in NavButtons) r.IsChecked = r == NavExams;
        MovePill(NavExams);
        ShowPage(new ExamDetailView(id));
    }

    private void Nav_Click(object sender, RoutedEventArgs e) => Go((string)((RadioButton)sender).Tag);

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        App.CurrentUser = null;
        ShowAuth();
    }
}
