namespace RollShopManager.Security;

/// <summary>
/// Bộ hẹn giờ kiểm tra tự động khóa (chạy trên thread UI bằng System.Windows.Forms.Timer).
/// Khởi tạo 1 lần ở form chính: _autoLockMonitor = new AutoLockMonitor(); _autoLockMonitor.Start();
/// </summary>
public sealed class AutoLockMonitor : IDisposable
{
    public const int DefaultCheckIntervalMilliseconds = 1000;

    private readonly System.Windows.Forms.Timer _timer;
    private bool _disposed;

    public AutoLockMonitor()
        : this(DefaultCheckIntervalMilliseconds)
    {
    }

    public AutoLockMonitor(int checkIntervalMilliseconds)
    {
        if (checkIntervalMilliseconds < 100)
        {
            throw new ArgumentOutOfRangeException(nameof(checkIntervalMilliseconds),
                "Chu kỳ kiểm tra phải tối thiểu 100 ms.");
        }

        _timer = new System.Windows.Forms.Timer { Interval = checkIntervalMilliseconds };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>Phát sinh khi phiên Admin vừa bị tự động khóa về Operator.</summary>
    public event EventHandler? AutoLocked;

    /// <summary>Phát sinh mỗi chu kỳ khi đang là Admin, kèm thời gian còn lại trước khi tự khóa (dùng để hiển thị đếm ngược).</summary>
    public event EventHandler<TimeSpan>? CountdownTick;

    public bool IsRunning => _timer.Enabled;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AppSession.RegisterActivity();
        _timer.Start();
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _timer.Stop();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (AppSession.CheckAndApplyAutoLock())
        {
            AutoLocked?.Invoke(this, EventArgs.Empty);
            return;
        }

        TimeSpan? remaining = AppSession.TimeUntilAutoLock;
        if (remaining.HasValue)
        {
            CountdownTick?.Invoke(this, remaining.Value);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _timer.Dispose();
        _disposed = true;
    }
}
