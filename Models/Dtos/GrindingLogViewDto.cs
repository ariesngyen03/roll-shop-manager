namespace RollShopManager.Models;

/// <summary>
/// Dòng hiển thị trên các tab MAI_WR, MAI_BUR, MAI_IMR.
/// </summary>
public class GrindingLogViewDto
{
    public int Id { get; set; }

    public string RollCode { get; set; } = string.Empty;

    public string RollType { get; set; } = string.Empty;

    /// <summary>Lần mài thứ N (tính cả InitialGrindCount).</summary>
    public int GrindSequence { get; set; }

    public DateTime GrindDate { get; set; }

    public decimal PreGrindDiameter { get; set; }

    public decimal PostGrindDiameter { get; set; }

    /// <summary>Tiêu hao mài = Pre - Post.</summary>
    public decimal GrindConsumption { get; set; }

    /// <summary>Tổng mm mài lũy kế = MaxDiameter - PostGrindDiameter.</summary>
    public decimal CumulativeWear { get; set; }

    /// <summary>Phần trăm tuổi thọ còn lại tính tại thời điểm sau ca mài này.</summary>
    public decimal RemainingLifetimePercent { get; set; }

    /// <summary>Ước tính số lần mài còn lại tính tại thời điểm sau ca mài này.</summary>
    public int EstimatedRemainingGrinds { get; set; }

    public bool IsAbnormal { get; set; }

    /// <summary>"Bình thường" / "Bất thường".</summary>
    public string ConditionText { get; set; } = GrindConditionTexts.Normal;

    public string? Remarks { get; set; }

    /// <summary>true nếu đây là ca mài mới nhất của cây trục (chỉ dòng này mới quyết định ĐK hiện tại).</summary>
    public bool IsLatestOfRoll { get; set; }
}
