# ExamBox

Exam platform in C#/.NET 8.

- **Admin side — `ExamBox.exe`**: a Windows desktop app (WPF). Self-contained single file: no .NET install,
  no web server to set up. Staff create students and exams, publish them, and see results.
- **Student side — web portal**: students sit exams in any browser. The exe hosts the portal itself
  (default `http://<this-pc-ip>:5109`), so there is nothing else to install.

Everything is stored locally in a SQLite database. No mock data: the first launch asks you to create the
administrator account, and all other data is created in the app.

## Using it

1. Run `ExamBox.exe`, create the administrator account.
2. **Students** → *Add student*. A temporary password is shown once; give it to the student (they must change it at first sign-in).
3. **Exams** → *New exam*, add multiple-choice questions, then *Publish*.
4. **Student portal** page shows the address to give students (same network/Wi-Fi). Allow ExamBox through the
   Windows firewall (Private networks) when prompted. Keep ExamBox open while exams run.
5. Open an exam → **Results** to see scores, or export them as CSV.

Data folder: `%LOCALAPPDATA%\ExamBox` (override with the `EXAMBOX_DATA` environment variable). Back it up by copying it.

## Features

- Administrator sign-in with "Remember me" (password stored encrypted for your Windows user only), starts maximised, hashed passwords, any non-empty password allowed, lockout after 10 failed attempts (5 min), deactivated students are signed out immediately.
- Student management: add, edit, search, deactivate, delete, reset password, exam history.
- Exams: timed multiple-choice, marks per question, pass mark, publish/unpublish. Questions lock once a student starts.
- Students: one attempt per exam, server-enforced deadline, automatic grading, answer review.
- Dashboard with live counts, average score, recent submissions.

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
