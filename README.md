# ExamBox

Exam platform in C#/.NET 8.

- **Admin side, `ExamBox.exe`**: a Windows desktop app (WPF). Self-contained single file: no .NET install,
  no web server to set up. Staff create students and exams, publish them, and see results.
- **Student side, web portal**: students sit exams in any browser. The exe hosts the portal itself
  (default `http://<this-pc-ip>:5109`), so there is nothing else to install.

Existing databases are upgraded in place when a newer version starts; nothing is lost.
Everything is stored locally in a SQLite database. No mock data: the first launch asks you to create the
administrator account, and all other data is created in the app.

## Using it

1. Run `ExamBox.exe`, create the administrator account.
2. **Students** → *Add student* (you choose the password, or click *Generate*), or *Import from Excel* (download the template; list ID, name, class and optionally a password, and ExamBox gives you a login sheet). Passwords are only ever changed by the admin: use the key icon on a student's row. Students see their profile and password under *My account*, but cannot change it themselves.
3. **Exams** → *New exam* (e.g. "SS1 English First Term"). Set the duration, pass mark and, optionally, the class it is for.
4. **Questions**: open the exam → *Download template*, let the teacher fill it in Excel, then *Import from Excel*.
   - One question per row. `Type` is `OBJ` (multiple choice, options A-E, `Correct` letter) or `THEORY` (typed answer, optional `Model answer` marking guide).
   - Sub-parts: give each its own row and number it `1a`, `1b`, `1c`. A THEORY row with 0 marks is a reading passage students only read.
   - Pictures: put `YES` in the `Image` column (or a file name plus a pictures folder when importing). Questions needing a picture show **Needed**; select each and *Attach picture*. The exam cannot be launched until all are attached.
   - Maths: type `x^2`, `x_1`, `\frac{3}{4}`, `\sqrt{49}`, `\pi`, `\times`, `\le` (or paste ², √, ÷ directly) and students see proper maths. Students answering theory questions get a symbol bar (² √ π ÷ × ≤ ...).
   - Pictures can also be pasted straight into the Image cell of a question row in Excel.
   - A preview lists any row that needs fixing before anything is added. Questions can also be added, edited and re-ordered by hand.
5. **Templates**: an exam that has not been launched is a draft/template. *Duplicate* it for another class or term, and *Launch* it when it is time: for everyone or one class, opening now or at a chosen date/time, closing at a chosen time or when you close it.
6. **Student portal** page shows the address to give students (same network/Wi-Fi). Allow ExamBox through the
   Windows firewall (Private networks) when prompted. Keep ExamBox open while exams run.
7. Students see *Available*, *Coming up* and their results. Objective answers are graded instantly; theory answers are typed in the browser (autosaved every few seconds, so a crash or closed tab loses nothing) and wait in **Marking**, where you give marks and comments per question. The final result appears once marking is finished.
8. **Live** tab on an exam: see who is sitting it, *Pause* a student (their clock stops and they are signed out; when they sign in again the exam waits with the time left), *Resume*, *Edit time left*, or *Submit now*.
9. Open an exam → **Results** to see scores and export them as CSV, Excel or PDF (also from **Reports**).

Data folder: `%LOCALAPPDATA%\ExamBox` (override with the `EXAMBOX_DATA` environment variable). Back it up by copying it.

## Features

- Administrator sign-in: "Remember me" pre-fills the username and password (password stored encrypted for your Windows account only; you still press Sign in every time the app starts), show-password eye, any non-empty password,
  warning when few attempts remain, then a 5-minute lock after 10 failures with a live countdown. Starts maximised with a loading splash.
- Forgotten admin password: close ExamBox and run `ExamBox.exe --reset-admin NewPassword` (students and exams are untouched).
- The student site never reveals whether an ID belongs to an administrator (same generic error for every failure).
- Slideshow sign-in screens, illustrations, loaders and page transitions in both the desktop app and the student portal.
- Student management: add, edit, search, deactivate, delete, reset password, exam history.
- Exams: objective (A-E) and theory questions in one exam, question pictures, shuffle, show/hide correct answers, pass mark, reusable templates, scheduled launch with class targeting. Questions lock once a student starts.
- Students: one attempt per exam, server-enforced deadline (also when a window closes), autosaved typed answers, instant objective grading, teacher-marked theory with feedback, answer review.
- Dashboard: live stats, enrolment sparkline, monthly growth, grade-distribution donut, pass/fail trend, searchable recent submissions, getting-started checklist.
- Reports: filter by exam and period, KPIs, per-exam pass rates, top students, CSV export.
- Settings: institution name (also shown on the student site), exam defaults, one-click database backup, security notes.

## Projects

| Project | What it is |
|---|---|
| `src/ExamBox.Core` | Database (EF Core + SQLite), models, auth/student/exam/dashboard services |
| `src/ExamBox.Portal` | Student web portal (ASP.NET Core MVC), embeddable via `PortalHost` |
| `src/ExamBox.Admin` | The `ExamBox.exe` desktop app; hosts the portal |
| `src/ExamBox.Server` | Headless portal host for Linux/servers (`--create-admin` to bootstrap) |
| `tests/ExamBox.Tests` | Service tests and end-to-end portal tests |

## Building the exe

Requires Microsoft's .NET 8 SDK (it includes the WindowsDesktop targets that distro-packaged SDKs lack).

```powershell
.\scripts\publish-admin.ps1        # Windows  -> dist\ExamBox.exe
```
```bash
./scripts/publish-admin.sh         # Linux/macOS (cross-publishes the Windows exe)
```

The GitHub Actions workflow `Build ExamBox.exe` also builds it and uploads `ExamBox-win-x64` as a downloadable artifact.
The exe is ~80 MB because it bundles the .NET runtime, WPF and ASP.NET Core. It is not code-signed, so Windows
SmartScreen may warn on first run ("More info" → "Run anyway").

```
dotnet test tests/ExamBox.Tests    # runs anywhere
```
