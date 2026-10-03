using ExamBox.Services;

namespace ExamBox.Admin;

/// <summary>Owns the embedded student web portal so the desktop app can start/stop it.</summary>
public sealed class PortalManager
{
    private PortalHost? _host;
    public bool Running => _host != null;
    public int Port { get; private set; }
    public string? Error { get; private set; }
    public event Action? Changed;

    public async Task StartAsync(int port)
    {
        await StopAsync();
        try
        {
            _host = await PortalHost.StartAsync(App.Db, port);
            Port = port;
            Error = null;
        }
        catch (Exception ex)
        {
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;
            Error = inner.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase) || inner.Message.Contains("Failed to bind", StringComparison.OrdinalIgnoreCase)
                ? $"Port {port} is already in use by another program. Choose a different port."
                : inner.Message;
        }
        Changed?.Invoke();
    }

    /// <param name="notify">False when shutting down: no UI is left to update.</param>
    public async Task StopAsync(bool notify = true)
    {
        if (_host == null) return;
        var h = _host;
        _host = null;
        try { await h.DisposeAsync(); } catch { /* already stopping */ }
        if (notify) Changed?.Invoke();
    }
}
