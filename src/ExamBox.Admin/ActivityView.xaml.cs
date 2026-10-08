using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ExamBox.Admin;

public partial class ActivityView : UserControl
{
    /// <summary>Status doubles as the pill label so the shared pill template can colour it.</summary>
    public sealed record LogVm(string Time, string Status, string Kind, string Actor, string Event, string Details);

    private DispatcherTimer? _timer;

    public ActivityView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Reload();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _timer.Tick += (_, _) => Reload();
            _timer.Start();
        };
        Unloaded += (_, _) => _timer?.Stop();
    }

    private string? KindFilter => FIn.IsChecked == true ? "signin" : FExam.IsChecked == true ? "exam" : FAdmin.IsChecked == true ? "admin" : FFail.IsChecked == true ? "failed" : null;

    private void Reload()
    {
        var kind = KindFilter;
        var list = App.Log.List(kind, Search.Text, 600);
        // Sign-in and sign-out are shown together under "Sign-ins"
        if (kind == "signin") list = App.Log.List("signin", Search.Text, 400).Concat(App.Log.List("signout", Search.Text, 400)).OrderByDescending(a => a.At).Take(600).ToList();
        Table.ItemsSource = list.Select(a => new LogVm(Ui.Local(a.At), Pretty(a.Kind), a.Kind, a.Actor ?? "", a.Subject ?? "", a.Details ?? "")).ToList();
        Empty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Sub.Text = $"Showing the latest {list.Count} entr{(list.Count == 1 ? "y" : "ies")}. Updates every 10 seconds.";
    }

    private static string Pretty(string kind) => kind switch
    {
        "signin" => "Sign-in", "signout" => "Sign-out", "failed" => "Failed", "exam" => "Exam", "admin" => "Admin", _ => kind,
    };

    private void Filter_Click(object sender, RoutedEventArgs e) => Reload();

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = Search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (IsLoaded) Reload();
    }

    private static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var rows = (Table.ItemsSource as IEnumerable<LogVm>)?.ToList() ?? new();
        if (rows.Count == 0) { Ui.Info("There is nothing to export."); return; }
        ExportHelper.Save($"ExamBox activity {DateTime.Now:yyyy-MM-dd}.csv", "CSV file (*.csv)|*.csv", () =>
        {
            var sb = new StringBuilder("Time,Type,Who,Event,Details\r\n");
            foreach (var r in rows) sb.Append(string.Join(",", new[] { r.Time, r.Kind, r.Actor, r.Event, r.Details }.Select(Csv))).Append("\r\n");
            return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }, Window.GetWindow(this));
    }
}
