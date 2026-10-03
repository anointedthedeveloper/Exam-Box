# ExamBox

Online exam platform built with ASP.NET Core 8 (MVC), EF Core and SQLite.

## Run

```bash
cd src/ExamBox
dotnet run          # http://localhost:5109
```

On first run there is no data. Open the site and you'll be sent to `/setup` to create the first
administrator. Everything after that is created through the UI — no seed or mock data.

## Features

- **Auth**: cookie authentication, separate Student / Staff sign-in tabs, hashed passwords (ASP.NET Identity hasher),
  lockout after 5 failed attempts (10 min), deactivated users are signed out immediately.
- **Student management** (admin): create, edit, search, deactivate, delete, reset password. A random temporary
  password is generated and shown once; students must change it on first sign-in.
- **Exams** (admin): create exams, multiple-choice questions with marks, publish/unpublish, results per exam.
  Questions lock once a student has started.
- **Student portal**: dashboard, start a timed exam (one attempt each), server-enforced deadline, automatic grading,
  answer review with correct answers.
- **Dashboard** (admin): live counts, average score, recent submissions, newest students.

## Configuration

Database location: `ConnectionStrings:Default` in `appsettings.json` (default `Data Source=exambox.db`).
The schema is created automatically on startup (`EnsureCreated`).
