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
            "server" => (NavServer, new ServerView()),
            "account" => (NavAccount, new AccountView()),
            _ => (NavDashboard, new DashboardView()),
        };
        radio.IsChecked = true;
        Page.Content = view;
    }

    /// <summary>Open one exam's detail page (keeps "Exams" highlighted).</summary>
    public void OpenExam(int id)
    {
        NavExams.IsChecked = true;
        Page.Content = new ExamDetailView(id);
    }

    private void Nav_Click(object sender, RoutedEventArgs e) => Go((string)((RadioButton)sender).Tag);

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        App.CurrentUser = null;
        // Signing out ends "remember me" auto-sign-in (the username stays pre-filled).
        App.Settings.RememberedSecret = null;
        App.Settings.Save();
        ShowAuth();
    }
}
