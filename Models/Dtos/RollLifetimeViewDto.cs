namespace RollShopManager.Models;

/// <summary>
/// Thông tin vòng đời của một cây trục: dữ liệu khai báo + các chỉ số tính toán động.
/// </summary>
public class RollLifetimeViewDto : RollEntity
{
    /// <summary>Đường kính hiện tại = PostGrindDiameter của lần mài mới nhất, hoặc InitialDiameter nếu chưa mài.</summary>
    public decimal CurrentDiameter { get; set; }

    /// <summary>Tổng số lần mài = InitialGrindCount + số dòng nhật ký mài.</summary>
    public int TotalGrindCount { get; set; }

    /// <summary>Số lần mài được ghi nhận trong phần mềm (không gồm InitialGrindCount).</summary>
    public int LoggedGrindCount { get; set; }

    /// <summary>Tổng mm đã mài lũy kế = MaxDiameter - CurrentDiameter.</summary>
    public decimal TotalWearMm { get; set; }

    /// <summary>Số mm còn có thể mài = Max(0, CurrentDiameter - MinDiameter).</summary>
    public decimal RemainingUsableMm { get; set; }

    /// <summary>Phần trăm tuổi thọ còn lại (0 - 100).</summary>
    public decimal RemainingLifetimePercent { get; set; }

    /// <summary>Tiêu hao trung bình mỗi lần mài = TotalWearMm / TotalGrindCount (hoặc định mức mặc định).</summary>
    public decimal AverageWearPerGrind { get; set; }

    /// <summary>true nếu AverageWearPerGrind đang lấy theo định mức mặc định (trục chưa có dữ liệu mài).</summary>
    public bool IsUsingDefaultWearRate { get; set; }

    /// <summary>Ước tính số lần mài còn lại = Floor(RemainingUsableMm / AverageWearPerGrind) khi đã có lịch sử mài.</summary>
    public int EstimatedRemainingGrinds { get; set; }

    /// <summary>Chuỗi hiển thị ước tính lần mài còn lại: "Chưa mài" nếu chưa có dữ liệu mài, hoặc "~N lần" nếu đã có lịch sử mài.</summary>
    public string EstimatedRemainingGrindsText { get; set; } = "Chưa mài";

    /// <summary>Ngày mài gần nhất (null nếu chưa mài).</summary>
    public DateTime? LastGrindDate { get; set; }

    /// <summary>Ca mài gần nhất có phải bất thường hay không.</summary>
    public bool LastGrindIsAbnormal { get; set; }

    /// <summary>"Đang sử dụng", "Cần kiểm tra (Bất thường)" hoặc "Hết đời (Scrap)".</summary>
    public string Status { get; set; } = RollStatusTexts.InUse;

    public bool IsScrap => Status == RollStatusTexts.Scrap;
}
