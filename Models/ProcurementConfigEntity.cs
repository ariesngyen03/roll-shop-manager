namespace RollShopManager.Models;

/// <summary>
/// Bảng ProcurementConfigs - Cấu hình đặt hàng cho từng loại trục WR, IMR, BUR.
/// </summary>
public class ProcurementConfigEntity
{
    /// <summary>Loại trục (Primary Key): WR, IMR, BUR.</summary>
    public string RollType { get; set; } = string.Empty;

    /// <summary>Độ dày lớp vỏ chuẩn của 1 cây mới (MaxDia - MinDia chuẩn), VD: WR=50, IMR=50, BUR=80.</summary>
    public decimal StandardUsableMmPerRoll { get; set; }

    /// <summary>Tổng số ngày chờ gia công + nhập khẩu về kho, VD: 35 ngày.</summary>
    public int LeadTimeDays { get; set; }

    /// <summary>Số cây trục tồn kho an toàn tối thiểu, VD: 2.0 cây.</summary>
    public decimal SafetyStockRolls { get; set; }

    public ProcurementConfigEntity Clone()
    {
        return new ProcurementConfigEntity
        {
            RollType = RollType,
            StandardUsableMmPerRoll = StandardUsableMmPerRoll,
            LeadTimeDays = LeadTimeDays,
            SafetyStockRolls = SafetyStockRolls
        };
    }
}
