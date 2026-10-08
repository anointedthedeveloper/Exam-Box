using ClosedXML.Excel;
using ExamBox.Models;

namespace ExamBox.Services;

public sealed record ImportIssue(int Row, string Message, bool IsWarning = false);

public sealed class ImportResult
{
    public List<Question> Questions { get; } = new();
    public List<ImportIssue> Issues { get; } = new();
    public bool HasErrors => Issues.Any(i => !i.IsWarning);
    public int Objective => Questions.Count(q => q.Type == QuestionType.Objective);
    public int Theory => Questions.Count(q => q.Type == QuestionType.Theory);
    public int NeedImages => Questions.Count(q => q.MissingImage);
}

/// <summary>Reads teachers' question sheets (.xlsx) and writes the blank/sample template they start from.</summary>
public static class QuestionImporter
{
    private static readonly string[] Headers = { "No", "Type", "Question", "A", "B", "C", "D", "E", "Correct", "Marks", "Image", "Model answer" };

    public static byte[] BuildTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Questions");
        Header(ws);
        ws.SheetView.FreezeRows(1);
        var types = ws.Range("B2:B500").CreateDataValidation();
        types.List("OBJ,THEORY", true);
        var correct = ws.Range("I2:I500").CreateDataValidation();
        correct.List("A,B,C,D,E", true);
        ws.Range("A2:A500").Style.NumberFormat.Format = "@";
        ws.Range("C2:L500").Style.Alignment.WrapText = true;
        ws.Range("C2:L500").Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        Widths(ws);

        var ex = wb.AddWorksheet("Example");
        Header(ex);
        object[][] rows =
        {
            new object[] { "1", "OBJ", "Choose the word that is opposite in meaning to \"generous\".", "Kind", "Selfish", "Wealthy", "Careful", "", "B", 1, "", "" },
            new object[] { "2", "OBJ", "Study the diagram. Which part makes food for the plant?", "Root", "Stem", "Leaf", "Flower", "", "C", 2, "yes", "" },
            new object[] { "3", "THEORY", "Read the passage and answer the questions below.", "", "", "", "", "", "", 0, "", "" },
            new object[] { "3a", "THEORY", "State the main idea of the passage.", "", "", "", "", "", "", 3, "", "The passage argues that reading daily builds vocabulary." },
            new object[] { "4", "OBJ", "Solve x^2 - 5x + 6 = 0. Which values of x satisfy it?", "x = 1 or 6", "x = 2 or 3", "x = -2 or -3", "x = 0 or 5", "", "B", 2, "", "" },
            new object[] { "5", "OBJ", "Simplify \\frac{3}{4} \\times 8 and find \\sqrt{49}.", "6 and 7", "6 and 8", "24 and 7", "3 and 7", "", "A", 2, "", "" },
            new object[] { "3b", "THEORY", "List two examples the writer gives.", "", "", "", "", "", "", 4, "", "" },
        };
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = ex.Cell(r + 2, c + 1);
                if (rows[r][c] is int n) cell.Value = n; else cell.Value = rows[r][c].ToString();
            }
        Widths(ex);
        ex.Cell(10, 1).Value = "This sheet is only an illustration. Type your own questions on the \"Questions\" sheet.";
        ex.Cell(10, 1).Style.Font.Italic = true;

        var how = wb.AddWorksheet("How to use");
        string[] lines =
        {
            "HOW TO FILL IN THE QUESTIONS SHEET",
            "",
            "One question per row. Do not change the heading row.",
            "No: the number students see. Use 1, 2, 3 ... or 3a, 3b for sub-parts. Leave blank to number automatically.",
            "Type: OBJ for multiple choice, THEORY for a question the student types an answer to.",
            "Question: the question text (for sub-parts, write only that part).",
            "A to E: the options. A and B are required for OBJ; C, D and E are optional. Leave blank for THEORY.",
            "Correct: the letter of the right option (OBJ only).",
            "Marks: marks for the question. A THEORY row with 0 marks is treated as a heading and not scored when it is just a passage.",
            "Image: leave blank when there is no picture. Type YES when the question needs a picture - you will attach it afterwards in ExamBox, question by question.",
            "       Or type the picture's file name (for example map1.png) and choose the folder that holds your pictures when importing.",
            "Model answer: optional marking guide for THEORY questions. Only teachers see it.",
            "Maths: type x^2 for a power, x_1 for an index, \\frac{a}{b} for a fraction, \\sqrt{x} for a root. Symbols such as \\pi, \\times, \\div, \\le, \\ge, \\pm, \\theta and \\degree are shown as real symbols to students. You can also paste symbols like \u00B2 \u221A \u00F7 straight in.",
            "Pictures: you can paste a picture into the Image cell of a question row (Insert > Pictures) and it is imported with that question.",
            "",
            "Save the file as .xlsx and use Import questions in ExamBox. A preview shows any row that needs fixing before anything is added.",
        };
        for (var i = 0; i < lines.Length; i++) how.Cell(i + 1, 1).Value = lines[i];
        how.Cell(1, 1).Style.Font.Bold = true;
        how.Column(1).Width = 120;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Header(IXLWorksheet ws)
    {
        for (var i = 0; i < Headers.Length; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = Headers[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#1D4ED8");
        }
    }

    private static void Widths(IXLWorksheet ws)
    {
        double[] w = { 6, 10, 60, 22, 22, 22, 22, 22, 9, 8, 14, 40 };
        for (var i = 0; i < w.Length; i++) ws.Column(i + 1).Width = w[i];
    }

    /// <param name="imagesFolder">Folder used to resolve file names typed in the Image column (optional).</param>
    public static ImportResult Parse(Stream xlsx, string? imagesFolder = null)
    {
        var res = new ImportResult();
        XLWorkbook wb;
        try { wb = new XLWorkbook(xlsx); }
        catch { res.Issues.Add(new ImportIssue(0, "This is not a valid Excel (.xlsx) file.")); return res; }
        using (wb)
        {
            var ws = wb.Worksheets.FirstOrDefault(w => w.Name.Equals("Questions", StringComparison.OrdinalIgnoreCase)) ?? wb.Worksheets.FirstOrDefault();
            if (ws == null) { res.Issues.Add(new ImportIssue(0, "The workbook has no sheets.")); return res; }

            var used = ws.RangeUsed();
            if (used == null) { res.Issues.Add(new ImportIssue(0, "The sheet is empty.")); return res; }

            // find the header row (first 10 rows) and map columns by name
            var map = new Dictionary<string, int>();
            var headerRow = 0;
            for (var r = used.FirstRow().RowNumber(); r <= Math.Min(used.LastRow().RowNumber(), used.FirstRow().RowNumber() + 9); r++)
            {
                var m = new Dictionary<string, int>();
                for (var c = used.FirstColumn().ColumnNumber(); c <= used.LastColumn().ColumnNumber(); c++)
                {
                    var key = Canon(ws.Cell(r, c).GetString());
                    if (key != null && !m.ContainsKey(key)) m[key] = c;
                }
                if (m.ContainsKey("question") && (m.ContainsKey("type") || m.ContainsKey("a"))) { map = m; headerRow = r; break; }
            }
            if (headerRow == 0)
            {
                res.Issues.Add(new ImportIssue(0, "Could not find the heading row. Use the ExamBox template (columns: No, Type, Question, A-E, Correct, Marks, Image, Model answer)."));
                return res;
            }

            // pictures pasted into the sheet: each attaches to the question on the row where it sits
            var pasted = new Dictionary<int, (byte[] Data, string Type)>();
            foreach (var pic in ws.Pictures)
            {
                try
                {
                    var row = pic.TopLeftCell?.Address.RowNumber ?? 0;
                    if (row <= headerRow) continue;
                    using var ms = new MemoryStream();
                    pic.ImageStream.Position = 0; pic.ImageStream.CopyTo(ms);
                    var bytes = ms.ToArray();
                    var kind = ImageSniffer.Detect(bytes);
                    if (kind == null) res.Issues.Add(new ImportIssue(row, "A picture on this row is not a PNG, JPG, GIF or WebP image. Attach it after importing.", true));
                    else if (bytes.Length > ExamService.MaxImageBytes) res.Issues.Add(new ImportIssue(row, "The picture on this row is larger than 3 MB. Attach a smaller one after importing.", true));
                    else pasted[row] = (bytes, kind);
                }
                catch { /* unreadable picture: skip */ }
            }

            string Get(int row, string key) => map.TryGetValue(key, out var c) ? ws.Cell(row, c).GetFormattedString().Trim() : "";

            for (var r = headerRow + 1; r <= used.LastRow().RowNumber(); r++)
            {
                var text = Get(r, "question");
                var anyCell = map.Values.Any(c => !ws.Cell(r, c).IsEmpty());
                if (!anyCell) continue;
                void Err(string m) => res.Issues.Add(new ImportIssue(r, m));
                if (text.Length == 0) { Err("The question text is empty."); continue; }

                var typeRaw = Get(r, "type").ToUpperInvariant();
                QuestionType type;
                if (typeRaw is "" or "OBJ" or "OBJECTIVE" or "MCQ" or "MULTIPLE CHOICE") type = typeRaw == "" && Get(r, "a").Length == 0 && Get(r, "b").Length == 0 ? QuestionType.Theory : QuestionType.Objective;
                else if (typeRaw is "THEORY" or "ESSAY" or "THEO") type = QuestionType.Theory;
                else { Err($"Unknown type \"{Get(r, "type")}\". Use OBJ or THEORY."); continue; }

                var marksRaw = Get(r, "marks");
                var marks = 0; var marksOk = marksRaw.Length == 0 || int.TryParse(marksRaw, out marks) || (double.TryParse(marksRaw, out var dm) && dm == Math.Floor(dm) && (marks = (int)dm) >= 0);
                if (!marksOk) { Err($"Marks \"{marksRaw}\" is not a whole number."); continue; }
                if (marksRaw.Length == 0) marks = type == QuestionType.Objective ? 1 : 0;

                var input = new QuestionInput(type, Get(r, "no"), text, marks,
                    Get(r, "a"), Get(r, "b"), Get(r, "c"), Get(r, "d"), Get(r, "e"), Get(r, "correct"), Get(r, "model"));

                if (type == QuestionType.Theory && marks == 0)
                    res.Issues.Add(new ImportIssue(r, "Theory row has no marks, so it is shown as a reading passage and is not scored.", true));
                var err = ExamService.Validate(input);
                if (err != null) { Err(err); continue; }
                var q = new Question();
                ExamService.Apply(q, input);

                if (pasted.TryGetValue(r, out var pp)) { q.ImageData = pp.Data; q.ImageType = pp.Type; q.ImageRequired = true; }
                var img = Get(r, "image");
                if (img.Length > 0 && !q.HasImage)
                {
                    var lower = img.ToLowerInvariant();
                    if (lower is "yes" or "y" or "required" or "true" or "1" or "x") q.ImageRequired = true;
                    else if (lower is "no" or "n" or "none" or "false" or "0" or "-") { }
                    else
                    {
                        q.ImageRequired = true;
                        var path = imagesFolder == null ? null : SafeFile(imagesFolder, img);
                        if (path == null)
                            res.Issues.Add(new ImportIssue(r, imagesFolder == null
                                ? $"Picture \"{img}\" was not loaded (no pictures folder chosen). Attach it after importing."
                                : $"Picture \"{img}\" was not found in the chosen folder. Attach it after importing.", true));
                        else
                        {
                            var bytes = File.ReadAllBytes(path);
                            var t = ImageSniffer.Detect(bytes);
                            if (t == null) res.Issues.Add(new ImportIssue(r, $"\"{img}\" is not a PNG, JPG, GIF or WebP picture. Attach it after importing.", true));
                            else if (bytes.Length > ExamService.MaxImageBytes) res.Issues.Add(new ImportIssue(r, $"\"{img}\" is larger than 3 MB. Attach a smaller one after importing.", true));
                            else { q.ImageData = bytes; q.ImageType = t; }
                        }
                    }
                }
                res.Questions.Add(q);
            }
            if (res.Questions.Count == 0 && !res.HasErrors) res.Issues.Add(new ImportIssue(0, "No questions were found under the heading row."));
        }
        return res;
    }

    private static string? SafeFile(string folder, string name)
    {
        // only a bare file name is allowed: no sub-folders or traversal out of the chosen folder
        if (name != Path.GetFileName(name)) return null;
        var p = Path.Combine(folder, name);
        return File.Exists(p) ? p : null;
    }

    private static string? Canon(string h)
    {
        var s = new string(h.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return s switch
        {
            "no" or "number" or "qno" or "q" or "sn" or "s" or "num" => "no",
            "type" or "qtype" or "questiontype" => "type",
            "question" or "questions" or "text" or "questiontext" => "question",
            "a" or "optiona" => "a", "b" or "optionb" => "b", "c" or "optionc" => "c", "d" or "optiond" => "d", "e" or "optione" => "e",
            "correct" or "answer" or "correctanswer" or "key" or "correctoption" => "correct",
            "marks" or "mark" or "score" or "points" => "marks",
            "image" or "picture" or "img" or "diagram" => "image",
            "modelanswer" or "markingguide" or "guide" or "solution" => "model",
            _ => null,
        };
    }
}
