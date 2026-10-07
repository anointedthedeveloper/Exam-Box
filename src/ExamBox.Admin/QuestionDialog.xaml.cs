using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ExamBox.Models;
using ExamBox.Services;
using Microsoft.Win32;

namespace ExamBox.Admin;

public partial class QuestionDialog : Window
{
    private readonly int _examId;
    private readonly Question? _existing;
    private byte[]? _pendingImage;      // picture picked but not saved yet
    private bool _clearImage;

    public QuestionDialog(int examId, Question? existing)
    {
        _examId = examId;
        _existing = existing;
        InitializeComponent();
        Title = Heading.Text = existing == null ? "Add question" : "Edit question";
        SaveBtn.Content = existing == null ? "Add question" : "Save question";
        if (existing != null)
        {
            TypeBox.SelectedIndex = existing.Type == QuestionType.Theory ? 1 : 0;
            Number.Text = existing.Number; Marks.Text = existing.Marks.ToString();
            QText.Text = existing.Text; OptA.Text = existing.OptionA; OptB.Text = existing.OptionB;
            OptC.Text = existing.OptionC; OptD.Text = existing.OptionD; OptE.Text = existing.OptionE;
            Correct.SelectedIndex = Math.Max(0, "ABCDE".IndexOf(existing.CorrectOption ?? "A", StringComparison.Ordinal));
            ModelAnswer.Text = existing.ModelAnswer;
            NeedPic.IsChecked = existing.ImageRequired;
            if (existing.HasImage) Show(existing.ImageData!);
        }
        Loaded += (_, _) => QText.Focus();
    }

    private void Type_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ObjPanel == null) return;
        var theory = TypeBox.SelectedIndex == 1;
        ObjPanel.Visibility = theory ? Visibility.Collapsed : Visibility.Visible;
        TheoryPanel.Visibility = theory ? Visibility.Visible : Visibility.Collapsed;
        if (_existing == null && Marks.Text == (theory ? "1" : "5")) Marks.Text = theory ? "5" : "1";
    }

    private void Show(byte[] data)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.StreamSource = new MemoryStream(data); bmp.EndInit(); bmp.Freeze();
            Preview.Source = bmp; NoPic.Visibility = Visibility.Collapsed; RemovePic.Visibility = Visibility.Visible;
        }
        catch { Preview.Source = null; NoPic.Visibility = Visibility.Visible; RemovePic.Visibility = Visibility.Collapsed; }
    }

    private void Pick_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Pictures (*.png;*.jpg;*.jpeg;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.webp" };
        if (dlg.ShowDialog(this) != true) return;
        var bytes = File.ReadAllBytes(dlg.FileName);
        if (ImageSniffer.Detect(bytes) == null) { Fail("Use a PNG, JPG, GIF or WebP picture."); return; }
        if (bytes.Length > ExamService.MaxImageBytes) { Fail("That picture is larger than 3 MB. Shrink it and try again."); return; }
        ErrorText.Visibility = Visibility.Collapsed;
        _pendingImage = bytes; _clearImage = false; NeedPic.IsChecked = true;
        Show(bytes);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        _pendingImage = null; _clearImage = true;
        Preview.Source = null; NoPic.Visibility = Visibility.Visible; RemovePic.Visibility = Visibility.Collapsed;
    }

    private void Fail(string m) { ErrorText.Text = m; ErrorText.Visibility = Visibility.Visible; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Ui.TryInt(Marks.Text, out var marks)) { Fail("Marks must be a whole number."); return; }
        var theory = TypeBox.SelectedIndex == 1;
        var correct = ((ComboBoxItem)Correct.SelectedItem).Content?.ToString() ?? "A";
        var hasPic = _pendingImage != null || (!_clearImage && _existing?.HasImage == true);
        var input = new QuestionInput(theory ? QuestionType.Theory : QuestionType.Objective, Number.Text, QText.Text, marks,
            OptA.Text, OptB.Text, OptC.Text, OptD.Text, OptE.Text, correct, ModelAnswer.Text, NeedPic.IsChecked == true || hasPic);
        var r = App.Exams.SaveQuestion(_examId, _existing?.Id ?? 0, input);
        if (!r.Ok) { Fail(r.Error!); return; }
        // the picture needs the question's id, so find it again for new questions
        var q = _existing ?? App.Exams.Get(_examId)!.Questions.OrderByDescending(x => x.Id).First();
        if (_pendingImage != null)
        {
            var ir = App.Exams.SetImage(_examId, q.Id, _pendingImage, "");
            if (!ir.Ok) { Fail(ir.Error!); return; }
        }
        else if (_clearImage && _existing?.HasImage == true)
            App.Exams.ClearImage(_examId, q.Id, NeedPic.IsChecked == true);
        DialogResult = true;
    }
}
