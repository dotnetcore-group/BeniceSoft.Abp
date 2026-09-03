namespace BeniceSoft.Core.Strategy;

public interface IThreadLock : IDisposable
{
    bool IsAcquired { get; }
}

/// <summary>一次性获取锁：同一实例仅首次 <see cref="IsAcquired"/> 为 true。</summary>
public class OnceLock : IThreadLock
{
    private const int Undo = 0;
    private const int Did = 1;
    private int _status;

    public bool IsAcquired
    {
        get
        {
            if (_status == Did)
            {
                return false;
            }

            return Interlocked.CompareExchange(ref _status, Did, Undo) == Undo;
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _status, 0);
        GC.SuppressFinalize(this);
    }
}

public class ThreadLock(bool isAcquired, Action? dispose = null) : IThreadLock
{
    private bool _disposed;

    public bool IsAcquired { get; } = isAcquired;

    public void Dispose()
    {
        if (IsAcquired && !_disposed)
        {
            _disposed = true;
            dispose?.Invoke();
            GC.SuppressFinalize(this);
        }
    }
}
