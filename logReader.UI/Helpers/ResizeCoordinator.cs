namespace logReader.UI.Helpers;

/// <summary>Combines geometry updates from the same resize pass on the UI thread.</summary>
internal sealed class ResizeCoordinator : IDisposable
{
    private readonly Form _owner;
    private readonly Action _layout;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private bool _pending, _running, _posted, _resizing, _disposed;

    public ResizeCoordinator(Form owner, Action layout)
    {
        _owner = owner;
        _layout = layout;
        _timer.Tick += (_, _) => Flush();
        owner.ResizeBegin += (_, _) => _resizing = true;
        owner.ResizeEnd += (_, _) => { _resizing = false; Flush(); };
        owner.HandleCreated += (_, _) => { if (_pending) Request(); };
        owner.HandleDestroyed += (_, _) =>
        {
            _posted = false;
            if (!_disposed) _timer.Stop();
        };
        owner.Disposed += (_, _) => Dispose();
    }

    public void Request()
    {
        if (_disposed || _owner.IsDisposed || _owner.Disposing) return;
        _pending = true;
        if (!_owner.IsHandleCreated) return;
        if (_resizing) _timer.Start();
        else if (!_posted)
        {
            // Programmatic changes settle in the next message pass; live mouse
            // sizing is limited to one update per frame instead of per event.
            _posted = true;
            _owner.BeginInvoke((Action)(() => { _posted = false; Flush(); }));
        }
    }

    public void Flush()
    {
        if (_disposed || _running || !_pending || !_owner.IsHandleCreated ||
            _owner.IsDisposed || _owner.Disposing) return;
        _pending = false;
        _timer.Stop();
        _running = true;
        try { _layout(); }
        finally
        {
            _running = false;
            if (_pending && !_disposed) Request();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
    }
}
