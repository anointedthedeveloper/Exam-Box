using System.Windows;

namespace ExamBox.Admin;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = "v" + (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0");
    }

    public void SetStatus(string text) => Status.Text = text;
}
