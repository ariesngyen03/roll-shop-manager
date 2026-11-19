namespace RollShopManager.Security;

/// <summary>Ngoại lệ ném ra khi người dùng hiện tại không đủ quyền thực hiện thao tác.</summary>
public sealed class PermissionDeniedException : UnauthorizedAccessException
{
    public PermissionDeniedException(string message)
        : base(message)
    {
    }

    public PermissionDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Chốt chặn phân quyền ở tầng nghiệp vụ. UI có thể ẩn nút, nhưng Service vẫn luôn gọi các hàm này
/// để bảo đảm Operator không thể sửa / xóa dữ liệu dù bằng bất kỳ đường nào.
/// </summary>
public static class AuthorizationGuard
{
    /// <summary>Operator và Admin đều được thêm mới ca mài (theo trình tự thời gian).</summary>
    public static bool CanAddGrindingLog => true;

    /// <summary>Chỉ Admin mới được sửa / xóa dữ liệu, khai báo trục, cấu hình đặt hàng.</summary>
    public static bool CanEditOrDelete => AppSession.IsAdmin;

    /// <summary>Ném <see cref="PermissionDeniedException"/> nếu phiên hiện tại không phải Admin.</summary>
    public static void EnsureAdmin(string actionDescription)
    {
        if (!AppSession.IsAdmin)
        {
            string action = string.IsNullOrWhiteSpace(actionDescription) ? "Thao tác này" : actionDescription.Trim();
            throw new PermissionDeniedException(
                $"{action} yêu cầu quyền Quản lý (Admin). Vui lòng mở khóa bằng mã PIN quản lý.");
        }

        AppSession.RegisterActivity();
    }
}
