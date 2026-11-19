namespace RollShopManager.Models;

/// <summary>
/// Bảng GrindingLogs - Nhật ký mài dùng chung cho 3 tab MAI_WR, MAI_BUR, MAI_IMR.
/// </summary>
public class GrindingLogEntity
{
    /// <summary>Khóa chính tự tăng.</summary>
    public int Id { get; set; }

    /// <summary>Mã trục (Foreign Key tới Rolls.RollCode).</summary>
    public string RollCode { get; set; } = string.Empty;

    /// <summary>Thời điểm mài.</summary>
    public DateTime GrindDate { get; set; } = DateTime.Now;

    /// <summary>Đường kính trước mài tại ca đó (mm) - được hệ thống tự nối chuỗi.</summary>
    public decimal PreGrindDiameter { get; set; }

    /// <summary>Đường kính sau mài do công nhân nhập (mm).</summary>
    public decimal PostGrindDiameter { get; set; }

    /// <summary>false: Bình thường (mài định kỳ), true: Bất thường / Sự cố (mài phá).</summary>
    public bool IsAbnormal { get; set; }

    /// <summary>Ghi chú lỗi nếu bất thường.</summary>
    public string? Remarks { get; set; }

    /// <summary>Lượng tiêu hao của ca mài: Pre - Post.</summary>
    public decimal GrindConsumption => PreGrindDiameter - PostGrindDiameter;

    public GrindingLogEntity Clone()
    {
        return new GrindingLogEntity
        {
            Id = Id,
            RollCode = RollCode,
            GrindDate = GrindDate,
            PreGrindDiameter = PreGrindDiameter,
            PostGrindDiameter = PostGrindDiameter,
            IsAbnormal = IsAbnormal,
            Remarks = Remarks
        };
    }
}
