using System.Globalization;
using System.Windows;
using ExamBox.Models;

namespace ExamBox.Admin;

public partial class LaunchDialog : Window
{
    private readonly Exam _exam;

    public LaunchDialog(Exam exam)
    {
        _exam = exam;
        InitializeComponent();
        Title = Heading.Text = "Launch exam";
        Sub.Text = $"“{exam.Title}”, {exam.Questions.Count} question(s), {exam.DurationMinutes} minutes. Students see it on the portal once it opens.";
        foreach (var c in App.Students.Classes()) ClassBox.Items.Add(c);
        if (!string.IsNullOrWhiteSpace(exam.ForDepartment)) { WhoClass.IsChecked = true; ClassBox.Text = exam.ForDepartment; }
        var tomorrow = DateTime.Now.Date.AddDays(1);
        OpenDate.SelectedDate = tomorrow; CloseDate.SelectedDate = tomorrow;
        When_Changed(this, new RoutedEventArgs());
    }

    private void Who_Changed(object sender, RoutedEventArgs e)
    {
        if (ClassBox == null) return;
        ClassBox.IsEnabled = WhoClass.IsChecked == true;
        if (ClassBox.IsEnabled) ClassBox.Focus();
    }

    private void When_Changed(object sender, RoutedEventArgs e)
    {
        if (OpenRow == null || CloseRow == null || Summary == null) return;
        OpenRow.IsEnabled = OpenAt.IsChecked == true;
        CloseRow.IsEnabled = CloseAt.IsChecked == true;
        Summary.Text = OpenAt.IsChecked == true ? "Students see it under “Coming up” until it opens." : "It opens for students immediately.";
    }

    private static bool TryWhen(DateTime? date, string time, out DateTime utc)
    {
        utc = default;
        if (date == null) return false;
        if (!TimeSpan.TryParseExact(time.Trim(), new[] { "h\\:mm", "hh\\:mm" }, CultureInfo.InvariantCulture, out var t)) return false;
        utc = DateTime.SpecifyKind(date.Value.Date + t, DateTimeKind.Local).ToUniversalTime();
        return true;
    }

    private void Fail(string m) { ErrorText.Text = m; ErrorText.Visibility = Visibility.Visible; }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        DateTime? opens = null, closes = null;
        if (OpenAt.IsChecked == true)
        {
            if (!TryWhen(OpenDate.SelectedDate, OpenTime.Text, out var o)) { Fail("Pick the opening date and a time like 08:00."); return; }
            opens = o;
        }
        if (CloseAt.IsChecked == true)
        {
            if (!TryWhen(CloseDate.SelectedDate, CloseTime.Text, out var c)) { Fail("Pick the closing date and a time like 18:00."); return; }
            closes = c;
        }
        var cls = WhoClass.IsChecked == true ? ClassBox.Text.Trim() : null;
        if (WhoClass.IsChecked == true && string.IsNullOrEmpty(cls)) { Fail("Type or pick the class."); return; }
        var r = App.Exams.Launch(_exam.Id, opens, closes, cls);
        if (!r.Ok) { Fail(r.Error!); return; }
        DialogResult = true;
    }
}
