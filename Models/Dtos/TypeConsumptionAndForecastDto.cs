namespace RollShopManager.Models;

/// <summary>
/// Bảng tổng hợp KPI tiêu hao &amp; Cảnh báo đặt hàng theo từng loại trục (WR, IMR, BUR) trong kỳ Ngày A -> Ngày B.
/// Kết hợp giữa số liệu thực tế phát sinh trong kỳ và mô hình dự báo đặt hàng thông minh theo tốc độ vận hành / sản lượng.
/// </summary>
public class TypeConsumptionAndForecastDto
{
    public string RollType { get; set; } = string.Empty;

    // ----- Thông tin kỳ báo cáo -----
    public DateTime PeriodFromDate { get; set; }

    public DateTime PeriodToDate { get; set; }

    /// <summary>Số ngày lịch của kỳ (tính cả ngày đầu và ngày cuối).</summary>
    public int PeriodDays { get; set; }

    /// <summary>Số ngày vận hành hiệu dụng dùng để tính tốc độ hao (đã loại trừ ngày tương lai chưa chạy hoặc tháng trống).</summary>
    public int EffectiveActiveDays { get; set; }

    public decimal ProductionTons { get; set; }

    // ----- Tiêu hao trong kỳ -----
    public int GrindCountInPeriod { get; set; }

    public int AbnormalGrindCountInPeriod { get; set; }

    public decimal NormalWearMm { get; set; }

    public decimal AbnormalWearMm { get; set; }

    public decimal TotalWearMmInPeriod { get; set; }

    /// <summary>Lượng mài trung bình mỗi ca mài (mm/lần) = TotalWearMmInPeriod / GrindCountInPeriod.</summary>
    public decimal AverageWearPerGrindMm => GrindCountInPeriod > 0
        ? Math.Round(TotalWearMmInPeriod / GrindCountInPeriod, 2)
        : 0m;

    /// <summary>Tỷ lệ % hao mòn do sự cố bất thường = (AbnormalWearMm / TotalWearMmInPeriod) * 100.</summary>
    public decimal AbnormalWearRatioPercent => TotalWearMmInPeriod > 0m
        ? Math.Round((AbnormalWearMm / TotalWearMmInPeriod) * 100m, 1)
        : 0m;

    /// <summary>mm trục / tấn thép = TotalWearMmInPeriod / ProductionTons.</summary>
    public decimal WearMmPerTon { get; set; }

    /// <summary>mm trục / 1000 tấn thép = WearMmPerTon * 1000.</summary>
    public decimal WearMmPer1000Tons { get; set; }

    /// <summary>Số cây quy đổi đã tiêu thụ = TotalWearMmInPeriod / StandardUsableMmPerRoll.</summary>
    public decimal EquivalentRollsConsumed { get; set; }

    // ----- Tồn kho -----
    /// <summary>Số cây trục còn hoạt động (chưa Scrap) của loại này.</summary>
    public int ActiveRollCount { get; set; }

    /// <summary>Số cây trục đang hoạt động nhưng đã sắp hết đời (&lt; 15% vỏ hợp kim).</summary>
    public int LowLifeRollCount { get; set; }

    /// <summary>Tổng mm còn có thể mài trong toàn xưởng = Sum(CurrentDia - MinDia) của các trục chưa Scrap.</summary>
    public decimal TotalRemainingMmInShop { get; set; }

    /// <summary>Số cây nguyên quy đổi còn lại = TotalRemainingMmInShop / StandardUsableMmPerRoll.</summary>
    public decimal EquivalentRollsRemaining { get; set; }

    // ----- Cấu hình đặt hàng áp dụng -----
    public decimal StandardUsableMmPerRoll { get; set; }

    public int LeadTimeDays { get; set; }

    public decimal SafetyStockRolls { get; set; }

    /// <summary>Quỹ mm tồn kho an toàn = SafetyStockRolls * StandardUsableMmPerRoll.</summary>
    public decimal SafetyStockMm { get; set; }

    /// <summary>Quỹ mm được phép dùng trước khi chạm tồn kho an toàn.</summary>
    public decimal NetUsableMm { get; set; }

    // ----- Dự báo -----
    /// <summary>Tốc độ đốt trục mỗi ngày (mm/ngày).</summary>
    public decimal DailyBurnRateMm { get; set; }

    /// <summary>Tốc độ đốt trục mỗi ngày quy đổi ra số cây.</summary>
    public decimal DailyBurnRateRolls { get; set; }

    /// <summary>Tốc độ đốt trục quy chuẩn theo tháng 30 ngày (mm/tháng) = DailyBurnRateMm * 30.</summary>
    public decimal MonthlyBurnRateMm { get; set; }

    /// <summary>Tốc độ hao trục quy chuẩn theo tháng 30 ngày (Cây/Tháng).</summary>
    public decimal MonthlyBurnRateRolls { get; set; }

    /// <summary>Số ngày còn chạy được trước khi chạm tồn kho an toàn. Null nếu không có tiêu hao để dự báo.</summary>
    public int? RunwayDays { get; set; }

    /// <summary>Số ngày còn lại trước hạn chót phải đặt hàng = RunwayDays - LeadTimeDays.</summary>
    public int? DaysUntilOrderDeadline { get; set; }

    /// <summary>Ngày khuyến nghị đặt hàng = Today + DaysUntilOrderDeadline.</summary>
    public DateTime? RecommendedOrderDate { get; set; }

    /// <summary>Ngày dự kiến chạm mức tồn kho an toàn = Today + RunwayDays.</summary>
    public DateTime? SafetyStockReachDate { get; set; }

    /// <summary>Ngày dự kiến cạn sạch toàn bộ quỹ mm còn lại trong xưởng (hết trục hoàn toàn).</summary>
    public DateTime? EstimatedStockoutDate { get; set; }

    /// <summary>"NGUY CẤP (ĐẶT HÀNG NGAY)", "CẢNH BÁO (SẮP ĐẾN HẠN ĐẶT)" hoặc "AN TOÀN".</summary>
    public string AlertStatus { get; set; } = ProcurementAlertStatuses.Safe;

    /// <summary>Diễn giải ngắn gọn cho cảnh báo.</summary>
    public string AlertMessage { get; set; } = string.Empty;

    // ----- Các trường hiển thị trực quan trên bảng UI -----
    public string GrindSummaryDisplay => GrindCountInPeriod == 0
        ? "0 ca"
        : (AbnormalGrindCountInPeriod > 0
            ? $"{GrindCountInPeriod} ca ({AbnormalGrindCountInPeriod} sự cố)"
            : $"{GrindCountInPeriod} ca chuẩn");

    public string RemainingInventoryDisplay =>
        $"{TotalRemainingMmInShop:N2} mm (~{EquivalentRollsRemaining:0.00} cây)";

    public string OrderScheduleDisplay
    {
        get
        {
            if (!RecommendedOrderDate.HasValue || !DaysUntilOrderDeadline.HasValue)
            {
                return "Chưa cần đặt";
            }

            int d = DaysUntilOrderDeadline.Value;
            if (d < 0)
            {
                return $"{RecommendedOrderDate:dd/MM/yyyy} (Trễ {Math.Abs(d)}d)";
            }

            if (d == 0)
            {
                return $"{RecommendedOrderDate:dd/MM/yyyy} (Hôm nay!)";
            }

            return $"{RecommendedOrderDate:dd/MM/yyyy} (Còn {d}d)";
        }
    }
}
