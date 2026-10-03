using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExamBox.Services;

namespace ExamBox.Admin;

public partial class DashboardView : UserControl
{
    public sealed record RecentRow(string Student, string Exam, string Score, string Outcome);
    public sealed record NewRow(string Name, string Initial, string Department, string StudentId);

    private bool _animated;

    public DashboardView()
    {
        InitializeComponent();
        Loaded += (_, _) => { App.Portal.Changed += RenderPortal; Reload(); };
        Unloaded += (_, _) => App.Portal.Changed -= RenderPortal;
    }

    private void Reload()
    {
        Greeting.Text = App.CurrentUser?.Username ?? "Admin";
        DateText.Text = "· " + DateTime.Now.ToString("dddd, d MMMM");
        var d = App.Dashboard.Get();
        Anim.CountUp(StatPublished, d.PublishedExams);
        StatPublishedSub.Text = $"of {d.Exams} exam(s)";
        Anim.CountUp(StatStudents, d.Students);
        StatStudentsSub.Text = $"{d.ActiveStudents} active";
        Anim.CountUp(StatCompleted, d.Completed);
        StatCompletedSub.Text = $"{d.Attempts - d.Completed} in progress";
        if (d.Completed == 0) StatAvg.Text = "—"; else Anim.CountUp(StatAvg, d.AvgPercent, 1, "%");
        StatAvgSub.Text = "across submitted exams";
        RecentGrid.ItemsSource = d.Recent.Select(a => new RecentRow(a.Student!.FullName, a.Exam!.Title, $"{a.Percent}%", a.Percent >= a.Exam.PassMarkPercent ? "Pass" : "Fail")).ToList();
        NewGrid.ItemsSource = d.NewStudents.Select(s => new NewRow(s.FullName, s.FullName.Length > 0 ? s.FullName[..1].ToUpperInvariant() : "?", s.Department ?? "No department", s.Username)).ToList();
        RecentEmpty.Visibility = d.Recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NewEmpty.Visibility = d.NewStudents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BuildChecklist(d);
        RenderPortal();
        if (!_animated)
        {
            _animated = true;
            Anim.Stagger(new FrameworkElement[] { HeroOuter, PortalBanner, Checklist, Lists });
        }
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
    private void Manage_Click(object sender, RoutedEventArgs e) => MainWindow.Current.Go("students");
    private void Results_Click(object sender, RoutedEventArgs e) => MainWindow.Current.Go("exams");

    /// <summary>Onboarding: shown until students, an exam, a published exam and the portal are all in place.</summary>
    private void BuildChecklist(DashboardStats d)
    {
        var steps = new (bool Done, string Title, string Desc, string Action, Action Go)[]
        {
            (d.Students > 0, "Add your first student", "Students sign in with their student ID.", "Add student", () => MainWindow.Current.Go("students", openAdd: true)),
            (d.Exams > 0, "Create an exam", "Set the title, timer and pass mark.", "New exam", () => MainWindow.Current.Go("exams", openAdd: true)),
            (d.PublishedExams > 0, "Add questions and publish", "Students only see published exams.", "Open exams", () => MainWindow.Current.Go("exams")),
            (App.Portal.Running, "Share the student portal address", "Students open it in any browser on your network.", "Show address", () => MainWindow.Current.Go("server")),
        };
        var done = steps.Count(s => s.Done);
        Checklist.Visibility = done == steps.Length ? Visibility.Collapsed : Visibility.Visible;
        ChecklistProgress.Text = $"{done} of {steps.Length} done";
        ChecklistSteps.Children.Clear();
        foreach (var s in steps)
        {
            var row = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dot = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), VerticalAlignment = VerticalAlignment.Center };
            if (s.Done)
            {
                dot.Background = (Brush)FindResource("OkBrush");
                dot.Child = new TextBlock { Text = "\uE73E", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 13, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            }
            else
            {
                dot.BorderBrush = new SolidColorBrush(Color.FromRgb(0xC9, 0xD3, 0xEA));
                dot.BorderThickness = new Thickness(2);
            }
            Grid.SetColumn(dot, 0);

            var text = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = s.Title, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource(s.Done ? "MutedBrush" : "InkBrush"), TextDecorations = s.Done ? TextDecorations.Strikethrough : null });
            text.Children.Add(new TextBlock { Text = s.Desc, FontSize = 12, Foreground = (Brush)FindResource("MutedBrush") });
            Grid.SetColumn(text, 1);

            row.Children.Add(dot);
            row.Children.Add(text);
            if (!s.Done)
            {
                var go = new Button { Content = s.Action, Style = (Style)FindResource("GhostButton"), Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0) };
                var action = s.Go;
                go.Click += (_, _) => action();
                Grid.SetColumn(go, 2);
                row.Children.Add(go);
            }
            ChecklistSteps.Children.Add(row);
        }
    }
}
