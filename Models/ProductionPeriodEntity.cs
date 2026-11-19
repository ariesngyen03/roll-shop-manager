namespace RollShopManager.Models;

/// <summary>
/// Bảng ProductionPeriods - Lịch sử các kỳ báo cáo sản lượng.
/// </summary>
public class ProductionPeriodEntity
{
    public int Id { get; set; }

    public string PeriodName { get; set; } = string.Empty;

    public DateTime FromDate { get; set; } = DateTime.Today;

    public DateTime ToDate { get; set; } = DateTime.Today;

    /// <summary>Sản lượng cán trong kỳ (tấn).</summary>
    public decimal ProductionTons { get; set; }

    public ProductionPeriodEntity Clone()
    {
        return new ProductionPeriodEntity
        {
            Id = Id,
            PeriodName = PeriodName,
            FromDate = FromDate,
            ToDate = ToDate,
            ProductionTons = ProductionTons
        };
    }
}
