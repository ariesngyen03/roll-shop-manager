namespace RollShopManager.Models;

/// <summary>
/// Danh mục loại trục hợp lệ trong xưởng và các định mức mặc định gắn với từng loại.
/// </summary>
public static class RollTypes
{
    /// <summary>Work Roll - Trục làm việc.</summary>
    public const string WR = "WR";

    /// <summary>Intermediate Roll - Trục trung gian.</summary>
    public const string IMR = "IMR";

    /// <summary>Backup Roll - Trục đỡ.</summary>
    public const string BUR = "BUR";

    /// <summary>Thứ tự chuẩn dùng cho hiển thị và báo cáo: WR, IMR, BUR.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { WR, IMR, BUR };

    /// <summary>Định mức mài mặc định (mm/lần) cho WR khi trục chưa có lịch sử mài.</summary>
    public const decimal DefaultWearPerGrindWr = 0.5m;

    /// <summary>Định mức mài mặc định (mm/lần) cho IMR khi trục chưa có lịch sử mài.</summary>
    public const decimal DefaultWearPerGrindImr = 0.5m;

    /// <summary>Định mức mài mặc định (mm/lần) cho BUR khi trục chưa có lịch sử mài.</summary>
    public const decimal DefaultWearPerGrindBur = 1.0m;

    public static string Normalize(string? rollType)
    {
        return (rollType ?? string.Empty).Trim().ToUpperInvariant();
    }

    public static bool IsValid(string? rollType)
    {
        string normalized = Normalize(rollType);
        return normalized == WR || normalized == IMR || normalized == BUR;
    }

    public static decimal GetDefaultWearPerGrind(string rollType)
    {
        return Normalize(rollType) switch
        {
            WR => DefaultWearPerGrindWr,
            IMR => DefaultWearPerGrindImr,
            BUR => DefaultWearPerGrindBur,
            _ => throw new ArgumentOutOfRangeException(nameof(rollType), rollType,
                "Loại trục không hợp lệ. Chỉ chấp nhận WR, IMR hoặc BUR.")
        };
    }

    public static int GetSortOrder(string? rollType)
    {
        return Normalize(rollType) switch
        {
            WR => 0,
            IMR => 1,
            BUR => 2,
            _ => 99
        };
    }

    public static string GetDisplayName(string rollType)
    {
        return Normalize(rollType) switch
        {
            WR => "Work Roll (Trục làm việc)",
            IMR => "Intermediate Roll (Trục trung gian)",
            BUR => "Backup Roll (Trục đỡ)",
            _ => rollType
        };
    }

    /// <summary>Tên tab nhật ký mài tương ứng với loại trục: MAI_WR, MAI_IMR, MAI_BUR.</summary>
    public static string GetGrindingTabName(string rollType)
    {
        string normalized = Normalize(rollType);
        if (!IsValid(normalized))
        {
            throw new ArgumentOutOfRangeException(nameof(rollType), rollType,
                "Loại trục không hợp lệ. Chỉ chấp nhận WR, IMR hoặc BUR.");
        }

        return "MAI_" + normalized;
    }
}
