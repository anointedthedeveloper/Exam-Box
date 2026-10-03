using System.Windows;

namespace ExamBox.Admin;

public partial class SplashWindow : Window
{
    public SplashWindow() => InitializeComponent();
    public void SetStatus(string text) => Status.Text = text;
}
