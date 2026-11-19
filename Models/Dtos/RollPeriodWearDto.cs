namespace RollShopManager.Models;

/// <summary>
/// Bảng chi tiết tiêu hao của từng cây trục trong khoảng Ngày A -> Ngày B.
/// </summary>
public class RollPeriodWearDto
{
    public string RollCode { get; set; } = string.Empty;

    public string RollType { get; set; } = string.Empty;

    /// <summary>Số ca mài trong kỳ.</summary>
    public int GrindCountInPeriod { get; set; }

    /// <summary>Số ca mài bất thường trong kỳ.</summary>
    public int AbnormalGrindCountInPeriod { get; set; }

    /// <summary>Tổng mm mài định kỳ (bình thường) trong kỳ.</summary>
    public decimal NormalWearMm { get; set; }

    /// <summary>Tổng mm mài bất thường / sự cố trong kỳ.</summary>
    public decimal AbnormalWearMm { get; set; }

    /// <summary>Tổng mm mài trong kỳ = Normal + Abnormal.</summary>
    public decimal TotalWearMmInPeriod { get; set; }

    /// <summary>Trung bình lượng mài mỗi ca trong kỳ (mm/lần).</summary>
    public decimal AverageWearPerGrindMm => GrindCountInPeriod > 0
        ? Math.Round(TotalWearMmInPeriod / GrindCountInPeriod, 2)
        : 0m;

    /// <summary>Chuỗi hiển thị số ca mài kèm số ca sự cố.</summary>
    public string GrindSummaryDisplay => GrindCountInPeriod == 0
        ? "0 ca"
        : (AbnormalGrindCountInPeriod > 0
            ? $"{GrindCountInPeriod} ca ({AbnormalGrindCountInPeriod} sự cố)"
            : $"{GrindCountInPeriod} ca chuẩn");

    /// <summary>Đường kính hiện tại (tại thời điểm xem báo cáo).</summary>
    public decimal CurrentDiameter { get; set; }

    public decimal RemainingUsableMm { get; set; }

    public decimal RemainingLifetimePercent { get; set; }

    public string Status { get; set; } = RollStatusTexts.InUse;
}
