using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ExamBox.Admin;

public partial class OnlineView : UserControl
{
    public sealed record OnlineVm(int Id, string Name, string Code, string Class, string Doing, string Seen);

    private DispatcherTimer? _timer;

    public OnlineView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Reload();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timer.Tick += (_, _) => Reload();
            _timer.Start();
        };
        Unloaded += (_, _) => _timer?.Stop();
    }

    private void Reload()
    {
        var list = App.Presence.Online();
        Table.ItemsSource = list.Select(o => new OnlineVm(o.StudentId, o.Name, o.Code, o.Class ?? "-",
            o.Exam == null ? "Browsing the portal" : o.Paused ? $"{o.Exam} (paused)" : $"Sitting {o.Exam}", Ui.Ago(o.LastSeenUtc))).ToList();
        Empty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Table.Visibility = list.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        var sitting = list.Count(o => o.Exam != null);
        Sub.Text = list.Count == 0 ? "No students on the portal right now." : $"{list.Count} student(s) online, {sitting} sitting an exam.";
    }

    private void Out_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not OnlineVm row) return;
        if (!Ui.Confirm($"Sign {row.Name} out now?")) return;
        var r = App.Students.ForceLogout(row.Id);
        if (!r.Ok) Ui.Error(r.Error!);
        Reload();
    }

    private void All_Click(object sender, RoutedEventArgs e)
    {
        if (!Ui.Confirm("Sign out ALL students on every device?\n\nStudents in an exam are sent to the sign-in page. Answers they typed are kept.")) return;
        var n = App.Students.ForceLogoutAll();
        Ui.Info($"All {n} student(s) have been signed out.");
        Reload();
    }
}
