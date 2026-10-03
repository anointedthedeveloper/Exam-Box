using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExamBox.Services;

namespace ExamBox.Admin;

public partial class DashboardView : UserControl
{
    public sealed record RecentRow(string Student, string Exam, string Score);
    public sealed record NewRow(string Name, string StudentId);

    public DashboardView()
    {
        InitializeComponent();
        Loaded += (_, _) => { App.Portal.Changed += RenderPortal; Reload(); };
        Unloaded += (_, _) => App.Portal.Changed -= RenderPortal;
    }

    private void Reload()
    {
        Greeting.Text = $"Welcome back, {App.CurrentUser?.Username}";
        DateText.Text = DateTime.Now.ToString("dddd, d MMMM yyyy");
        var d = App.Dashboard.Get();
        StudentsN.Text = d.Students.ToString();
        StudentsSub.Text = $"{d.ActiveStudents} active";
        ExamsN.Text = d.Exams.ToString();
        ExamsSub.Text = $"{d.PublishedExams} published";
        DoneN.Text = d.Completed.ToString();
        DoneSub.Text = $"{d.Attempts - d.Completed} in progress";
        AvgN.Text = d.Completed == 0 ? "—" : $"{d.AvgPercent}%";
        RecentGrid.ItemsSource = d.Recent.Select(a => new RecentRow(a.Student!.FullName, a.Exam!.Title, $"{a.Percent}%")).ToList();
        NewGrid.ItemsSource = d.NewStudents.Select(s => new NewRow(s.FullName, s.Username)).ToList();
        RenderPortal();
    }

    private void RenderPortal()
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        if (App.Portal.Running)
        {
            var url = PortalHost.ReachableUrls(App.Portal.Port).First();
            text.Inlines.Add(new System.Windows.Documents.Run("● Student portal running  ") { Foreground = (Brush)FindResource("OkBrush"), FontWeight = FontWeights.Bold });
            text.Inlines.Add(new System.Windows.Documents.Run($"Students open {url} in their browser."));
        }
        else
        {
            text.Inlines.Add(new System.Windows.Documents.Run("● Student portal stopped  ") { Foreground = (Brush)FindResource("ErrBrush"), FontWeight = FontWeights.Bold });
            text.Inlines.Add(new System.Windows.Documents.Run(App.Portal.Error ?? "Students can't sign in until you start it (Student portal page)."));
        }
        PortalBanner.Child = text;
    }

    private void AddStudent_Click(object sender, RoutedEventArgs e) => MainWindow.Current.Go("students", openAdd: true);
    private void NewExam_Click(object sender, RoutedEventArgs e) => MainWindow.Current.Go("exams", openAdd: true);
}
