using System.Diagnostics;
using RollShopManager.Database;
using RollShopManager.Models;

namespace RollShopManager.Security;

/// <summary>
/// Phiên làm việc toàn cục của ứng dụng trên máy tính dùng chung tại xưởng.
/// - Mặc định luôn là Operator khi mở app.
/// - Nhập đúng PIN quản lý -> Admin.
/// - Không thao tác quá <see cref="AutoLockTimeout"/> (3 phút) -> tự động khóa về Operator.
/// - Nhập sai PIN quá <see cref="MaxFailedUnlockAttempts"/> lần -> tạm khóa chức năng mở khóa trong <see cref="UnlockLockoutDuration"/>.
/// </summary>
public static class AppSession
{
    public static readonly TimeSpan AutoLockTimeout = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan UnlockLockoutDuration = TimeSpan.FromSeconds(60);
    public const int MaxFailedUnlockAttempts = 5;

    private static readonly object SyncRoot = new();
    private static readonly AuditLogRepository AuditRepository = new();

    private static UserRole _currentRole = UserRole.Operator;
    private static DateTime _lastActivityTime = DateTime.Now;
    private static DateTime? _adminSince;
    private static int _failedUnlockAttempts;
    private static DateTime? _unlockLockoutUntil;
    private static PinSecurityService? _pinSecurityService;

    /// <summary>Phát sinh mỗi khi quyền thay đổi (mở khóa, khóa tay, tự động khóa). Có thể phát trên thread UI.</summary>
    public static event EventHandler<RoleChangedEventArgs>? RoleChanged;

    public static UserRole CurrentRole
    {
        get
        {
            lock (SyncRoot)
            {
                return _currentRole;
            }
        }
    }

    public static bool IsAdmin => CurrentRole == UserRole.Admin;

    public static bool IsOperator => CurrentRole == UserRole.Operator;

    /// <summary>Thời điểm có thao tác chuột / bàn phím gần nhất (được cập nhật bởi IdleActivityMessageFilter).</summary>
    public static DateTime LastActivityTime
    {
        get
        {
            lock (SyncRoot)
            {
                return _lastActivityTime;
            }
        }
    }

    /// <summary>Thời điểm bắt đầu phiên Admin hiện tại (null nếu đang là Operator).</summary>
    public static DateTime? AdminSince
    {
        get
        {
            lock (SyncRoot)
            {
                return _adminSince;
            }
        }
    }

    public static int FailedUnlockAttempts
    {
        get
        {
            lock (SyncRoot)
            {
                return _failedUnlockAttempts;
            }
        }
    }

    public static TimeSpan IdleDuration
    {
        get
        {
            TimeSpan idle = DateTime.Now - LastActivityTime;
            return idle < TimeSpan.Zero ? TimeSpan.Zero : idle;
        }
    }

    /// <summary>Thời gian còn lại trước khi tự động khóa (null nếu đang là Operator).</summary>
    public static TimeSpan? TimeUntilAutoLock
    {
        get
        {
            lock (SyncRoot)
            {
                if (_currentRole != UserRole.Admin)
                {
                    return null;
                }

                TimeSpan remaining = AutoLockTimeout - (DateTime.Now - _lastActivityTime);
                return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
            }
        }
    }

    /// <summary>Thời gian còn lại của trạng thái tạm khóa mở khóa do nhập sai PIN nhiều lần (null nếu không bị khóa).</summary>
    public static TimeSpan? RemainingUnlockLockout
    {
        get
        {
            lock (SyncRoot)
            {
                if (!_unlockLockoutUntil.HasValue)
                {
                    return null;
                }

                TimeSpan remaining = _unlockLockoutUntil.Value - DateTime.Now;
                return remaining > TimeSpan.Zero ? remaining : null;
            }
        }
    }

    /// <summary>Thay thế dịch vụ kiểm tra PIN (dùng cho kiểm thử). Mặc định tự tạo PinSecurityService.</summary>
    public static void ConfigurePinSecurityService(PinSecurityService pinSecurityService)
    {
        ArgumentNullException.ThrowIfNull(pinSecurityService);
        lock (SyncRoot)
        {
            _pinSecurityService = pinSecurityService;
        }
    }

    /// <summary>Cập nhật thời điểm hoạt động gần nhất.</summary>
    public static void RegisterActivity()
    {
        lock (SyncRoot)
        {
            _lastActivityTime = DateTime.Now;
        }
    }

    /// <summary>Thử mở khóa quyền Admin bằng PIN quản lý.</summary>
    public static UnlockResult TryUnlockAdmin(string? pin)
    {
        DateTime now = DateTime.Now;
        PinSecurityService pinService;

        lock (SyncRoot)
        {
            if (_currentRole == UserRole.Admin)
            {
                _lastActivityTime = now;
                return UnlockResult.AlreadyAdmin;
            }

            if (_unlockLockoutUntil.HasValue)
            {
                if (now < _unlockLockoutUntil.Value)
                {
                    return UnlockResult.TemporarilyLockedOut;
                }

                _unlockLockoutUntil = null;
                _failedUnlockAttempts = 0;
            }

            _pinSecurityService ??= new PinSecurityService();
            pinService = _pinSecurityService;
        }

        if (!PinPolicy.IsValidFormat(pin, out _))
        {
            RegisterFailedUnlock(now, "PIN sai định dạng");
            return UnlockResult.InvalidFormat;
        }

        if (!pinService.VerifyPin(pin))
        {
            RegisterFailedUnlock(now, "PIN không đúng");
            return UnlockResult.InvalidPin;
        }

        RoleChangedEventArgs args;
        lock (SyncRoot)
        {
            UserRole oldRole = _currentRole;
            _currentRole = UserRole.Admin;
            _adminSince = now;
            _lastActivityTime = now;
            _failedUnlockAttempts = 0;
            _unlockLockoutUntil = null;
            args = new RoleChangedEventArgs(oldRole, UserRole.Admin, RoleChangeReason.ManualUnlock, now);
        }

        WriteAuditSafe(AuditActionTypes.AdminUnlock, "Mở khóa quyền Admin thành công.");
        RoleChanged?.Invoke(null, args);
        return UnlockResult.Success;
    }

    /// <summary>Khóa về quyền Operator (bấm nút khóa thủ công).</summary>
    public static void Lock()
    {
        Lock(RoleChangeReason.ManualLock);
    }

    /// <summary>Khóa về quyền Operator với lý do cụ thể. Không làm gì nếu đang là Operator.</summary>
    public static void Lock(RoleChangeReason reason)
    {
        DateTime now = DateTime.Now;
        RoleChangedEventArgs args;
        TimeSpan adminDuration;

        lock (SyncRoot)
        {
            if (_currentRole == UserRole.Operator)
            {
                return;
            }

            adminDuration = _adminSince.HasValue ? now - _adminSince.Value : TimeSpan.Zero;
            UserRole oldRole = _currentRole;
            _currentRole = UserRole.Operator;
            _adminSince = null;
            args = new RoleChangedEventArgs(oldRole, UserRole.Operator, reason, now);
        }

        string actionType = reason == RoleChangeReason.AutoLockTimeout
            ? AuditActionTypes.AdminAutoLock
            : AuditActionTypes.AdminLock;
        string reasonText = reason switch
        {
            RoleChangeReason.AutoLockTimeout => $"Tự động khóa do không thao tác quá {AutoLockTimeout.TotalMinutes:0} phút",
            RoleChangeReason.ApplicationShutdown => "Khóa khi đóng ứng dụng",
            _ => "Khóa thủ công"
        };

        WriteAuditSafe(actionType, $"{reasonText}. Thời lượng phiên Admin: {adminDuration:hh\\:mm\\:ss}.");
        RoleChanged?.Invoke(null, args);
    }

    /// <summary>
    /// Kiểm tra quá thời gian không thao tác; nếu đang là Admin và đã quá 3 phút thì khóa về Operator.
    /// </summary>
    /// <returns>true nếu vừa thực hiện tự động khóa.</returns>
    public static bool CheckAndApplyAutoLock()
    {
        bool shouldLock;
        lock (SyncRoot)
        {
            shouldLock = _currentRole == UserRole.Admin && DateTime.Now - _lastActivityTime >= AutoLockTimeout;
        }

        if (!shouldLock)
        {
            return false;
        }

        Lock(RoleChangeReason.AutoLockTimeout);
        return true;
    }

    private static void RegisterFailedUnlock(DateTime now, string reason)
    {
        int attempts;
        bool lockedOut = false;

        lock (SyncRoot)
        {
            _failedUnlockAttempts++;
            attempts = _failedUnlockAttempts;
            if (_failedUnlockAttempts >= MaxFailedUnlockAttempts)
            {
                _unlockLockoutUntil = now + UnlockLockoutDuration;
                lockedOut = true;
            }
        }

        string details = $"Mở khóa Admin thất bại ({reason}). Lần sai thứ {attempts}/{MaxFailedUnlockAttempts}.";
        if (lockedOut)
        {
            details += $" Tạm khóa chức năng mở khóa trong {UnlockLockoutDuration.TotalSeconds:0} giây.";
        }

        WriteAuditSafe(AuditActionTypes.AdminUnlockFailed, details);
    }

    /// <summary>
    /// Ghi audit nhưng không bao giờ làm gián đoạn thao tác khóa / mở khóa
    /// (ưu tiên an toàn: việc khóa về Operator luôn phải thành công kể cả khi DB lỗi).
    /// </summary>
    private static void WriteAuditSafe(string actionType, string details)
    {
        try
        {
            AuditRepository.Insert(new AuditLogEntity
            {
                ActionTime = DateTime.Now,
                ActionType = actionType,
                TargetEntity = "AppSession",
                Details = $"[{Environment.MachineName}] {details}"
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppSession] Không ghi được AuditLog ({actionType}): {ex}");
        }
    }
}
