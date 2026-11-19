using System.ComponentModel.DataAnnotations;

namespace RollShopManager.Models;

/// <summary>
/// Đại diện cho bản ghi sản lượng cán thép theo tháng (YYYY-MM) trong xưởng cán.
/// Lưu trong bảng SQLite MonthlyProductions để tự động tính toán định mức mm/1.000 tấn cho toàn bộ 12 tháng.
/// </summary>
public sealed class MonthlyProductionEntity
{
    /// <summary>Khóa chính: định dạng "YYYY-MM", ví dụ "2026-11".</summary>
    [Key]
    public string YearMonth { get; set; } = string.Empty;

    /// <summary>Năm (ví dụ 2026).</summary>
    public int Year { get; set; }

    /// <summary>Tháng (1 đến 12).</summary>
    public int Month { get; set; }

    /// <summary>Sản lượng cán thép thực tế của tháng (tấn). Mặc định 0.</summary>
    public decimal ProductionTons { get; set; }

    /// <summary>Ghi chú ca kíp / sản lượng (tùy chọn).</summary>
    public string? Notes { get; set; }
}
