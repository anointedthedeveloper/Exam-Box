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
    public static string Local(DateTime? utc, string none = "—") =>
        utc == null ? none : DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime().ToString("g");

    public static bool TryInt(string? text, out int value) => int.TryParse((text ?? "").Trim(), out value);
}
