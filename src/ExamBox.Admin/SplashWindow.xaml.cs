using System.Windows;
using System.Windows.Media.Animation;

namespace ExamBox.Admin;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = "v" + (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0");
    }

    public void SetStatus(string text) => Status.Text = text;

    /// <summary>Fades the splash away (revealing the window behind it), then closes it.</summary>
    public Task FadeOutAsync()
    {
        var done = new TaskCompletionSource();
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        fade.Completed += (_, _) => { Close(); done.TrySetResult(); };
        BeginAnimation(OpacityProperty, fade);
        return done.Task;
    }
}
