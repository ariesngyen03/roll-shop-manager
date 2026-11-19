namespace RollShopManager.Models;

/// <summary>Các trạng thái vòng đời của một cây trục.</summary>
public static class RollStatusTexts
{
    public const string InUse = "Đang sử dụng";
    public const string NeedInspection = "Cần kiểm tra (Bất thường)";
    public const string Scrap = "Hết đời (Scrap)";
}

/// <summary>Tình trạng của một ca mài.</summary>
public static class GrindConditionTexts
{
    public const string Normal = "Bình thường";
    public const string Abnormal = "Bất thường";

    public static string From(bool isAbnormal)
    {
        return isAbnormal ? Abnormal : Normal;
    }
}

/// <summary>Các mức cảnh báo đặt hàng trục.</summary>
public static class ProcurementAlertStatuses
{
    public const string Critical = "NGUY CẤP (ĐẶT HÀNG NGAY)";
    public const string Warning = "CẢNH BÁO (SẮP ĐẾN HẠN ĐẶT)";
    public const string Safe = "AN TOÀN";

    /// <summary>Ngưỡng số ngày còn lại trước hạn đặt hàng để chuyển sang mức CẢNH BÁO.</summary>
    public const int WarningThresholdDays = 14;

    public static string Classify(int daysUntilOrderDeadline)
    {
        if (daysUntilOrderDeadline <= 0)
        {
            return Critical;
        }

        if (daysUntilOrderDeadline <= WarningThresholdDays)
        {
            return Warning;
        }

        return Safe;
    }
}

/// <summary>Mã hành động ghi vào bảng AuditLogs.</summary>
public static class AuditActionTypes
{
    public const string DatabaseInitialized = "DATABASE_INITIALIZED";
    public const string DatabaseBackup = "DATABASE_BACKUP";

    public const string CreateRoll = "CREATE_ROLL";
    public const string UpdateRoll = "UPDATE_ROLL";
    public const string DeleteRoll = "DELETE_ROLL";

    public const string InsertBackdatedGrind = "INSERT_BACKDATED_GRIND";
    public const string UpdateGrind = "UPDATE_GRIND";
    public const string DeleteGrind = "DELETE_GRIND";
    public const string RecalculateChain = "RECALCULATE_CHAIN";

    public const string UpdateProcurementConfig = "UPDATE_PROCUREMENT_CONFIG";

    public const string CreateProductionPeriod = "CREATE_PRODUCTION_PERIOD";
    public const string UpdateProductionPeriod = "UPDATE_PRODUCTION_PERIOD";
    public const string DeleteProductionPeriod = "DELETE_PRODUCTION_PERIOD";

    public const string AdminUnlock = "ADMIN_UNLOCK";
    public const string AdminUnlockFailed = "ADMIN_UNLOCK_FAILED";
    public const string AdminLock = "ADMIN_LOCK";
    public const string AdminAutoLock = "ADMIN_AUTO_LOCK";
    public const string ChangePin = "CHANGE_PIN";
}
