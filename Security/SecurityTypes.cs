namespace RollShopManager.Security;

/// <summary>Quyền của phiên làm việc hiện tại trên máy tính dùng chung.</summary>
public enum UserRole
{
    /// <summary>Nhân viên vận hành: chỉ được thêm mới ca mài, không được sửa / xóa bất cứ dữ liệu nào.</summary>
    Operator = 0,

    /// <summary>Quản lý: toàn quyền sau khi nhập đúng PIN.</summary>
    Admin = 1
}

/// <summary>Lý do thay đổi quyền.</summary>
public enum RoleChangeReason
{
    ManualUnlock = 0,
    ManualLock = 1,
    AutoLockTimeout = 2,
    ApplicationShutdown = 3
}

/// <summary>Kết quả của thao tác mở khóa quyền Admin.</summary>
public enum UnlockResult
{
    Success = 0,
    AlreadyAdmin = 1,
    InvalidFormat = 2,
    InvalidPin = 3,
    TemporarilyLockedOut = 4
}

public sealed class RoleChangedEventArgs : EventArgs
{
    public RoleChangedEventArgs(UserRole oldRole, UserRole newRole, RoleChangeReason reason, DateTime changedAt)
    {
        OldRole = oldRole;
        NewRole = newRole;
        Reason = reason;
        ChangedAt = changedAt;
    }

    public UserRole OldRole { get; }

    public UserRole NewRole { get; }

    public RoleChangeReason Reason { get; }

    public DateTime ChangedAt { get; }
}
