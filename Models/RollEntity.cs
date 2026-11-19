namespace RollShopManager.Models;

/// <summary>
/// Bảng Rolls - Danh mục trục (khai báo tại màn hình QUAN_LY_TRUC).
/// </summary>
public class RollEntity
{
    /// <summary>Mã trục cố định (Primary Key). VD: WR-001, BUR-001, IMR-001.</summary>
    public string RollCode { get; set; } = string.Empty;

    /// <summary>Loại trục: chỉ nhận WR, BUR hoặc IMR.</summary>
    public string RollType { get; set; } = string.Empty;

    /// <summary>Ngày đưa trục vào sử dụng.</summary>
    public DateTime CommissionDate { get; set; } = DateTime.Today;

    /// <summary>Đường kính nguyên thủy khi trục mới 100% (mm).</summary>
    public decimal MaxDiameter { get; set; }

    /// <summary>Đường kính giới hạn phế phẩm (mm).</summary>
    public decimal MinDiameter { get; set; }

    /// <summary>
    /// Đường kính tại thời điểm bắt đầu quản lý bằng phần mềm (mm).
    /// Bằng MaxDiameter nếu là trục mới, nhỏ hơn nếu là trục cũ đang dùng dở.
    /// </summary>
    public decimal InitialDiameter { get; set; }

    /// <summary>Số lần đã mài trước khi đưa vào phần mềm (mặc định 0).</summary>
    public int InitialGrindCount { get; set; }

    public string? Notes { get; set; }

    /// <summary>Tổng độ dày lớp vỏ có thể mài của cây trục: MaxDiameter - MinDiameter.</summary>
    public decimal UsableLayerMm => MaxDiameter - MinDiameter;

    public RollEntity Clone()
    {
        return new RollEntity
        {
            RollCode = RollCode,
            RollType = RollType,
            CommissionDate = CommissionDate,
            MaxDiameter = MaxDiameter,
            MinDiameter = MinDiameter,
            InitialDiameter = InitialDiameter,
            InitialGrindCount = InitialGrindCount,
            Notes = Notes
        };
    }

    public void CopyRollFieldsFrom(RollEntity source)
    {
        ArgumentNullException.ThrowIfNull(source);
        RollCode = source.RollCode;
        RollType = source.RollType;
        CommissionDate = source.CommissionDate;
        MaxDiameter = source.MaxDiameter;
        MinDiameter = source.MinDiameter;
        InitialDiameter = source.InitialDiameter;
        InitialGrindCount = source.InitialGrindCount;
        Notes = source.Notes;
    }
}
