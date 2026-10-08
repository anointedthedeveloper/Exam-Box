using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ExamBox.Admin;

internal static class ExportHelper
{
    public static string Safe(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    /// <param name="filter">e.g. "PDF file (*.pdf)|*.pdf"</param>
    public static void Save(string fileName, string filter, Func<byte[]> build, Window? owner = null)
    {
        var dlg = new SaveFileDialog { Filter = filter, FileName = Safe(fileName) };
        if (dlg.ShowDialog(owner) != true) return;
        try
        {
            File.WriteAllBytes(dlg.FileName, build());
            if (Ui.Confirm("Saved.\n\nOpen the file now?"))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (Exception ex) { Ui.Error("Could not save the file:\n" + ex.Message); }
    }
}
