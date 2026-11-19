namespace RollShopManager.Database;

/// <summary>Các khóa cấu hình lưu trong bảng AppSettings.</summary>
public static class AppSettingKeys
{
    public const string SchemaVersion = "SchemaVersion";
    public const string DatabaseCreatedAt = "DatabaseCreatedAt";
    public const string AdminPinHash = "AdminPinHash";
    public const string AdminPinSalt = "AdminPinSalt";
    public const string AdminPinIterations = "AdminPinIterations";
    public const string AdminPinUpdatedAt = "AdminPinUpdatedAt";
    public const string LastBackupAt = "LastBackupAt";
    public const string LastBackupPath = "LastBackupPath";
}
