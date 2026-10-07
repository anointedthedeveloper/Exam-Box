using System.IO;
using System.Text.Json;

namespace ExamBox.Admin;

public sealed class AppSettings
{
    public int Port { get; set; } = 5109;
    public bool AutoStartServer { get; set; } = true;
    /// <summary>Shown on the admin dashboard and beside the logo on the student website.</summary>
    public string InstitutionName { get; set; } = "";
    /// <summary>Pre-filled when creating a new exam.</summary>
    public int DefaultDurationMinutes { get; set; } = 30;
    public int DefaultPassMark { get; set; } = 50;
    /// <summary>The "new" badge in the sidebar disappears once the page has been opened.</summary>
    public bool SeenReports { get; set; }
    public bool SeenSettings { get; set; }
    /// <summary>"Remember me": the last administrator username, pre-filled on the sign-in screen.
    /// No password is ever stored: the administrator signs in again every time the app starts.</summary>
    public string? RememberedUser { get; set; }

    private string _path = "";

    public static AppSettings Load(string dir)
    {
        var path = Path.Combine(dir, "settings.json");
        AppSettings s;
        try { s = File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new() : new(); }
        catch { s = new(); }
        if (s.Port is < 1 or > 65535) s.Port = 5109;
        if (s.DefaultDurationMinutes is < 1 or > 600) s.DefaultDurationMinutes = 30;
        if (s.DefaultPassMark is < 1 or > 100) s.DefaultPassMark = 50;
        s._path = path;
        return s;
    }

    public void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
        catch { /* settings are a convenience; never crash over them */ }
    }
}
