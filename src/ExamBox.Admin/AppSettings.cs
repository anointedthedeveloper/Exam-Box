using System.IO;
using System.Text.Json;

namespace ExamBox.Admin;

public sealed class AppSettings
{
    public int Port { get; set; } = 5109;
    public bool AutoStartServer { get; set; } = true;
    /// <summary>"Remember me": last administrator username (pre-filled on the sign-in screen).</summary>
    public string? RememberedUser { get; set; }
    /// <summary>Password encrypted with Windows DPAPI for the current Windows user only; enables automatic sign-in.</summary>
    public string? RememberedSecret { get; set; }

    private string _path = "";

    public static AppSettings Load(string dir)
    {
        var path = Path.Combine(dir, "settings.json");
        AppSettings s;
        try { s = File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new() : new(); }
        catch { s = new(); }
        if (s.Port is < 1 or > 65535) s.Port = 5109;
        s._path = path;
        return s;
    }

    public void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
        catch { /* settings are a convenience; never crash over them */ }
    }
}
