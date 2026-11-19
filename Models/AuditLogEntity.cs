namespace RollShopManager.Models;

/// <summary>
/// Bảng AuditLogs - Lưu vết các thao tác quản trị (sửa / xóa dữ liệu, mở khóa, đổi PIN...).
/// </summary>
public class AuditLogEntity
{
    public int Id { get; set; }

    public DateTime ActionTime { get; set; } = DateTime.Now;

    /// <summary>Mã hành động, xem <see cref="AuditActionTypes"/>.</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>Đối tượng bị tác động, VD: Rolls, GrindingLogs, ProcurementConfigs.</summary>
    public string TargetEntity { get; set; } = string.Empty;

    /// <summary>Mô tả chi tiết (giá trị trước / sau khi thay đổi).</summary>
    public string Details { get; set; } = string.Empty;
}
