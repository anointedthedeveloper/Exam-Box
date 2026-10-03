using System.Windows;

namespace ExamBox.Admin;

public partial class TempPasswordDialog : Window
{
    public TempPasswordDialog(string name, string studentId, string password, bool isNew)
    {
        InitializeComponent();
        Heading.Text = isNew ? $"{name} was added" : "Password reset";
        Sub.Text = $"Give this to {name} (student ID {studentId}). They must choose a new password the first time they sign in.";
        Pass.Text = password;
        Loaded += (_, _) => { Pass.Focus(); Pass.SelectAll(); };
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(Pass.Text); CopyBtn.Content = "Copied"; } catch { /* clipboard busy */ }
    }
}
