using System.Windows;

namespace ExamBox.Admin;

/// <summary>Lets the admin set how long a student has left in a running or paused exam.</summary>
public partial class MinutesDialog : Window
{
    private readonly int _attemptId;

    public MinutesDialog(int attemptId, string student, int currentSeconds)
    {
        _attemptId = attemptId;
        InitializeComponent();
        Title = Heading.Text = "Edit time left";
        Sub.Text = $"{student} has {Math.Max(1, (int)Math.Ceiling(currentSeconds / 60.0))} minute(s) left. Set the new total from now.";
        Minutes.Text = Math.Max(1, (int)Math.Ceiling(currentSeconds / 60.0)).ToString();
        Loaded += (_, _) => { Minutes.Focus(); Minutes.SelectAll(); };
    }

    private void Add(int n) { if (Ui.TryInt(Minutes.Text, out var m)) Minutes.Text = (m + n).ToString(); }
    private void Add5_Click(object sender, RoutedEventArgs e) => Add(5);
    private void Add10_Click(object sender, RoutedEventArgs e) => Add(10);
    private void Add30_Click(object sender, RoutedEventArgs e) => Add(30);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Ui.TryInt(Minutes.Text, out var m)) { ErrorText.Text = "Enter a whole number of minutes."; ErrorText.Visibility = Visibility.Visible; return; }
        var r = App.Live.SetMinutesLeft(_attemptId, m);
        if (!r.Ok) { ErrorText.Text = r.Error; ErrorText.Visibility = Visibility.Visible; return; }
        DialogResult = true;
    }
}
