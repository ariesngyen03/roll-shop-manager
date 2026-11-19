namespace RollShopManager.Models;

/// <summary>
/// DTO đại diện cho 1 dòng dữ liệu trong Ma trận Tiêu hao 12 Tháng (từ Tháng 01 đến Tháng 12 hoặc dòng Tổng hợp cả năm).
/// Tự động tổng hợp từ nhật ký mài và liên kết với sản lượng tháng để tính định mức mm/1.000 tấn và số cây quy đổi.
/// </summary>
public sealed class MonthlyConsumptionMatrixDto
{
    /// <summary>Định danh tháng dạng "YYYY-MM" (VD: "2026-11") hoặc "YYYY-ALL" cho dòng tổng cộng.</summary>
    public string YearMonth { get; set; } = string.Empty;

    /// <summary>Tiêu đề hiển thị (VD: "Tháng 11/2026" hoặc "CẢ NĂM 2026 (TỔNG &amp; ĐỊNH MỨC TB)").</summary>
    public string MonthDisplay { get; set; } = string.Empty;

    /// <summary>Tháng trong năm (1..12) hoặc 0 nếu là dòng tổng kết năm.</summary>
    public int MonthIndex { get; set; }

    /// <summary>Sản lượng cán thép thực tế của tháng (Tấn). Cho phép Admin chỉnh sửa trực tiếp trên lưới.</summary>
    public decimal ProductionTons { get; set; }

    // =========================================================================
    // 1. TRỤC LÀM VIỆC WR (WORK ROLL)
    // =========================================================================
    public int WrGrindCount { get; set; }
    public decimal WrNormalWearMm { get; set; }
    public decimal WrAbnormalWearMm { get; set; }
    public decimal WrTotalWearMm { get; set; }
    public decimal WrWearPerTon { get; set; }
    public decimal WrWearPer1000Tons { get; set; }
    public decimal WrEquivalentRollsUsed { get; set; }

    // =========================================================================
    // 2. TRỤC TRUNG GIAN IMR (INTERMEDIATE ROLL)
    // =========================================================================
    public int ImrGrindCount { get; set; }
    public decimal ImrNormalWearMm { get; set; }
    public decimal ImrAbnormalWearMm { get; set; }
    public decimal ImrTotalWearMm { get; set; }
    public decimal ImrWearPerTon { get; set; }
    public decimal ImrWearPer1000Tons { get; set; }
    public decimal ImrEquivalentRollsUsed { get; set; }

    // =========================================================================
    // 3. TRỤC ĐÈ BUR (BACKUP ROLL)
    // =========================================================================
    public int BurGrindCount { get; set; }
    public decimal BurNormalWearMm { get; set; }
    public decimal BurAbnormalWearMm { get; set; }
    public decimal BurTotalWearMm { get; set; }
    public decimal BurWearPerTon { get; set; }
    public decimal BurWearPer1000Tons { get; set; }
    public decimal BurEquivalentRollsUsed { get; set; }

    // =========================================================================
    // 4. TỔNG HỢP VÀ CHỈ SỐ XU HƯỚNG
    // =========================================================================
    /// <summary>Tổng lượng mài của cả 3 loại trục trong tháng (mm).</summary>
    public decimal TotalAllRollsWearMm { get; set; }

    /// <summary>Tổng số lượt mài của cả 3 loại trục.</summary>
    public int TotalAllGrindCount => WrGrindCount + ImrGrindCount + BurGrindCount;

    /// <summary>Tổng số cây quy đổi cả 3 loại.</summary>
    public decimal TotalEquivalentRollsUsed => WrEquivalentRollsUsed + ImrEquivalentRollsUsed + BurEquivalentRollsUsed;

    /// <summary>Suất tiêu hao tổng hợp cả 3 loại trục (mm/tấn sản lượng).</summary>
    public decimal TotalWearPerTon => WrWearPerTon + ImrWearPerTon + BurWearPerTon;

    /// <summary>Suất tiêu hao tổng hợp cả 3 loại trục (mm/1.000 tấn sản lượng).</summary>
    public decimal TotalWearPer1000Tons => WrWearPer1000Tons + ImrWearPer1000Tons + BurWearPer1000Tons;

    /// <summary>
    /// Xu hướng tăng/giảm so với tháng liền trước (MoM - Month over Month Trend %):
    /// Dương: Tiêu hao tăng so với tháng trước (% đỏ).
    /// Âm: Tiêu hao giảm - cải thiện hiệu quả (% xanh).
    /// Null nếu tháng này hoặc tháng trước không có dữ liệu để so sánh hoặc là dòng tổng kết năm.
    /// </summary>
    public decimal? MomTrendPercent { get; set; }

    /// <summary>Đánh dấu đây là dòng tổng cộng / trung bình cả năm (không phải tháng cụ thể).</summary>
    public bool IsSummaryRow { get; set; }
}
