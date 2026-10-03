using System.IO;
using System.Windows;
using System.Windows.Threading;
using ExamBox.Data;
using ExamBox.Models;
using ExamBox.Services;

namespace ExamBox.Admin;

public partial class App : Application
{
    public static DbFactory Db { get; private set; } = null!;
    public static AuthService Auth { get; private set; } = null!;
    public static StudentService Students { get; private set; } = null!;
    public static ExamService Exams { get; private set; } = null!;
    public static DashboardService Dashboard { get; private set; } = null!;
    public static AppSettings Settings { get; private set; } = null!;
    public static PortalManager Portal { get; } = new();
    public static User? CurrentUser { get; set; }

    private Mutex? _single;

    // Gives the taskbar button this app's own identity (and icon) instead of grouping it under the host process.
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(string appId);

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;
        try { SetCurrentProcessExplicitAppUserModelID("ExamBox.Admin"); } catch { /* cosmetic only */ }

        _single = new Mutex(true, "ExamBox.Admin.SingleInstance", out var first);
        if (!first)
        {
            MessageBox.Show("ExamBox is already running.", "ExamBox", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var splash = new SplashWindow();
        splash.Show();
        try
        {
            splash.SetStatus("Opening your data…");
            var db = new DbFactory();
            await Task.Run(db.Initialize);
            Db = db;
        }
        catch (Exception ex)
        {
            splash.Close();
            MessageBox.Show("ExamBox could not open its database:\n\n" + ex.Message, "ExamBox", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }
        Auth = new AuthService(Db);
        Students = new StudentService(Db);
        Exams = new ExamService(Db);
        Dashboard = new DashboardService(Db);
        Settings = AppSettings.Load(Db.DataDir);

        if (Settings.AutoStartServer)
        {
            splash.SetStatus("Starting the student portal…");
            await Portal.StartAsync(Settings.Port);
        }

        splash.SetStatus("Almost ready…");
        MainWindow = new MainWindow();   // becomes the main window before the splash closes, so closing it can't end the app
        MainWindow.Show();
        splash.Close();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Task.Run keeps the stop off the UI thread, which is blocked here.
        try { Task.Run(() => Portal.StopAsync(notify: false)).Wait(TimeSpan.FromSeconds(5)); } catch { /* shutting down */ }
        _single?.Dispose();
        base.OnExit(e);
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show("Something went wrong:\n\n" + e.Exception.Message, "ExamBox", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
