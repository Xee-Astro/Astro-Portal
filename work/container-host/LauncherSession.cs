using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace AsiairContainer;
internal sealed class LauncherSession : IDisposable
{
    private readonly Mutex? mutex;
    private readonly EventWaitHandle? activation;
    private RegisteredWaitHandle? listener;
    public bool Acquired { get; }
    public LauncherSession(string root, bool enabled)
    {
        if (!enabled) { Acquired = true; return; }
        string id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(root).TrimEnd('\\', '/').ToUpperInvariant())));
        activation = new(false, EventResetMode.AutoReset, "Local\\AstroPortal-" + id + "-activate");
        mutex = new(false, "Local\\AstroPortal-" + id);
        try { Acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { Acquired = true; }
        if (!Acquired) activation.Set();
    }
    public void Observe(Application app)
    {
        if (activation is null || !Acquired) return;
        listener = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) =>
        {
            if (app.Dispatcher.HasShutdownStarted) return;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                var main = app.MainWindow; if (main is null) return;
                var window = main.OwnedWindows.Cast<Window>().LastOrDefault(w => w.IsVisible) ?? main;
                if (window.WindowState == WindowState.Minimized) window.WindowState = window.WindowStyle == WindowStyle.None ? WindowState.Maximized : WindowState.Normal;
                window.Show(); window.Activate();
            }));
        }, null, Timeout.Infinite, false);
    }
    public void Dispose()
    {
        listener?.Unregister(null); activation?.Dispose();
        if (Acquired && mutex is not null) mutex.ReleaseMutex();
        mutex?.Dispose();
    }
}
