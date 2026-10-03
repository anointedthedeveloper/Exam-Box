using ExamBox.Data;
using ExamBox.Services;

// Headless student-portal host (Linux/macOS/servers). The Windows desktop app hosts the same portal itself.
//   ExamBox.Server [--port 5109] [--data <folder>] [--create-admin <username> <password> "<Full Name>"]
var port = 5109; string? data = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--port": port = int.Parse(args[++i]); break;
        case "--data": data = args[++i]; break;
        case "--create-admin":
            var db0 = new DbFactory(data); db0.Initialize();
            var r = new AuthService(db0).CreateAdmin(args.ElementAtOrDefault(i + 3) ?? args[i + 1], args[i + 1], null, args[i + 2]);
            Console.WriteLine(r.Ok ? "Administrator created." : "Error: " + r.Error);
            return r.Ok ? 0 : 1;
    }
}
var db = new DbFactory(data);
db.Initialize();
await using var host = await PortalHost.StartAsync(db, port);
Console.WriteLine($"ExamBox student portal running. Data: {db.DataDir}");
foreach (var u in PortalHost.ReachableUrls(port)) Console.WriteLine("  " + u);
Console.WriteLine("Press Ctrl+C to stop.");
var stop = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.TrySetResult(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.TrySetResult();
await stop.Task;
return 0;
