using System.Windows;

namespace ExamBox.Admin;

internal static class Ui
{
    private static Window? Owner => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;

    public static void Error(string message) =>
        MessageBox.Show(Owner!, message, "ExamBox", MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void Info(string message) =>
        MessageBox.Show(Owner!, message, "ExamBox", MessageBoxButton.OK, MessageBoxImage.Information);

    public static bool Confirm(string message) =>
        MessageBox.Show(Owner!, message, "ExamBox", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <summary>Timestamps are stored in UTC.</summary>
    public static string Local(DateTime? utc, string none = "-") =>
        utc == null ? none : DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime().ToString("g");

    /// <summary>"Just now", "3 hours ago", "Yesterday", "12 Sep 2026" (timestamps are stored in UTC).</summary>
    public static string Ago(DateTime? utc, string none = "Never")
    {
        if (utc == null) return none;
        var t = DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc);
        var d = DateTime.UtcNow - t;
        if (d.TotalSeconds < 60) return "Just now";
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes} min ago";
        if (d.TotalHours < 24) return $"{(int)d.TotalHours} hour{((int)d.TotalHours == 1 ? "" : "s")} ago";
        if (d.TotalHours < 48) return "Yesterday";
        if (d.TotalDays < 7) return $"{(int)d.TotalDays} days ago";
        return t.ToLocalTime().ToString("d MMM yyyy");
    }

    public static bool TryInt(string? text, out int value) => int.TryParse((text ?? "").Trim(), out value);
}
