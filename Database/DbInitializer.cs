using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using RollShopManager.Models;
using RollShopManager.Security;

namespace RollShopManager.Database;

/// <summary>
/// Khởi tạo cấu trúc CSDL RollShop.db: tạo bảng, chỉ mục, nạp cấu hình đặt hàng mặc định và mã PIN mặc định.
/// </summary>
public static class DbInitializer
{
    public const int CurrentSchemaVersion = 1;

    private static readonly object InitializeLock = new();

    public static bool IsInitialized { get; private set; }

    /// <summary>
    /// Ghi chú quan trọng về kiểu dữ liệu: toàn bộ cột decimal được khai báo TEXT để SQLite lưu nguyên văn
    /// chuỗi số thập phân (Microsoft.Data.Sqlite tự bind decimal thành TEXT theo InvariantCulture),
    /// tránh sai số dấu phẩy động của REAL. Cột DateTime cũng lưu TEXT dạng "yyyy-MM-dd HH:mm:ss.FFFFFFF"
    /// nên so sánh / sắp xếp theo chuỗi vẫn đúng thứ tự thời gian.
    /// </summary>
    private const string CreateSchemaSql = @"
CREATE TABLE IF NOT EXISTS AppSettings (
    Key   TEXT NOT NULL PRIMARY KEY,
    Value TEXT NULL
);

CREATE TABLE IF NOT EXISTS Rolls (
    RollCode          TEXT    NOT NULL PRIMARY KEY COLLATE NOCASE,
    RollType          TEXT    NOT NULL CHECK (RollType IN ('WR', 'BUR', 'IMR')),
    CommissionDate    TEXT    NOT NULL,
    MaxDiameter       TEXT    NOT NULL,
    MinDiameter       TEXT    NOT NULL,
    InitialDiameter   TEXT    NOT NULL,
    InitialGrindCount INTEGER NOT NULL DEFAULT 0 CHECK (InitialGrindCount >= 0),
    Notes             TEXT    NULL
);

CREATE INDEX IF NOT EXISTS IX_Rolls_RollType ON Rolls (RollType);

CREATE TABLE IF NOT EXISTS GrindingLogs (
    Id                INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    RollCode          TEXT    NOT NULL COLLATE NOCASE
                      REFERENCES Rolls (RollCode) ON UPDATE CASCADE ON DELETE RESTRICT,
    GrindDate         TEXT    NOT NULL,
    PreGrindDiameter  TEXT    NOT NULL,
    PostGrindDiameter TEXT    NOT NULL,
    IsAbnormal        INTEGER NOT NULL DEFAULT 0 CHECK (IsAbnormal IN (0, 1)),
    Remarks           TEXT    NULL
);

CREATE INDEX IF NOT EXISTS IX_GrindingLogs_Roll_Date ON GrindingLogs (RollCode, GrindDate, Id);
CREATE INDEX IF NOT EXISTS IX_GrindingLogs_GrindDate ON GrindingLogs (GrindDate);

CREATE TABLE IF NOT EXISTS ProcurementConfigs (
    RollType                TEXT    NOT NULL PRIMARY KEY CHECK (RollType IN ('WR', 'BUR', 'IMR')),
    StandardUsableMmPerRoll TEXT    NOT NULL,
    LeadTimeDays            INTEGER NOT NULL CHECK (LeadTimeDays >= 0),
    SafetyStockRolls        TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS ProductionPeriods (
    Id             INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    PeriodName     TEXT    NOT NULL,
    FromDate       TEXT    NOT NULL,
    ToDate         TEXT    NOT NULL,
    ProductionTons TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_ProductionPeriods_FromDate ON ProductionPeriods (FromDate);

CREATE TABLE IF NOT EXISTS MonthlyProductions (
    YearMonth      TEXT    NOT NULL PRIMARY KEY,
    Year           INTEGER NOT NULL,
    Month          INTEGER NOT NULL,
    ProductionTons TEXT    NOT NULL DEFAULT '0',
    Notes          TEXT    NULL
);

CREATE INDEX IF NOT EXISTS IX_MonthlyProductions_Year ON MonthlyProductions (Year);

CREATE TABLE IF NOT EXISTS AuditLogs (
    Id           INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    ActionTime   TEXT    NOT NULL,
    ActionType   TEXT    NOT NULL,
    TargetEntity TEXT    NOT NULL,
    Details      TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_AuditLogs_ActionTime ON AuditLogs (ActionTime);
";

    /// <summary>Cấu hình đặt hàng mặc định nạp khi khởi tạo DB lần đầu.</summary>
    public static IReadOnlyList<ProcurementConfigEntity> DefaultProcurementConfigs { get; } = new[]
    {
        new ProcurementConfigEntity { RollType = RollTypes.WR,  StandardUsableMmPerRoll = 50m, LeadTimeDays = 35, SafetyStockRolls = 1.5m },
        new ProcurementConfigEntity { RollType = RollTypes.IMR, StandardUsableMmPerRoll = 50m, LeadTimeDays = 40, SafetyStockRolls = 1.0m },
        new ProcurementConfigEntity { RollType = RollTypes.BUR, StandardUsableMmPerRoll = 80m, LeadTimeDays = 45, SafetyStockRolls = 1.0m }
    };

    /// <summary>
    /// Tạo file RollShop.db (nếu chưa có), tạo đầy đủ bảng, nạp cấu hình mặc định và PIN mặc định "123456".
    /// Hàm idempotent: gọi nhiều lần không làm mất hay nhân đôi dữ liệu.
    /// </summary>
    public static void Initialize()
    {
        SqliteTypeHandlers.Register();

        lock (InitializeLock)
        {
            string databasePath = DbConnectionFactory.DatabasePath;
            string? directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            bool isNewDatabase = !File.Exists(databasePath);

            using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();

            // WAL giúp đọc / ghi song song an toàn hơn và giảm rủi ro hỏng file khi mất điện đột ngột.
            connection.ExecuteScalar<string>("PRAGMA journal_mode = WAL;");
            connection.Execute("PRAGMA synchronous = FULL;");

            using SqliteTransaction transaction = connection.BeginTransaction();

            connection.Execute(CreateSchemaSql, transaction: transaction);

            var settingsRepository = new AppSettingsRepository();
            var procurementRepository = new ProcurementConfigRepository();
            var auditRepository = new AuditLogRepository();

            int seededConfigCount = 0;
            foreach (ProcurementConfigEntity config in DefaultProcurementConfigs)
            {
                seededConfigCount += procurementRepository.InsertIfNotExists(config, connection, transaction);
            }

            bool pinSeeded = false;
            string? existingPinHash = settingsRepository.Get(AppSettingKeys.AdminPinHash, connection, transaction);
            string? existingPinSalt = settingsRepository.Get(AppSettingKeys.AdminPinSalt, connection, transaction);
            if (string.IsNullOrWhiteSpace(existingPinHash) || string.IsNullOrWhiteSpace(existingPinSalt))
            {
                PinHashResult defaultPin = PinHasher.CreateHash(PinPolicy.DefaultPin);
                PinSecurityService.StorePinHash(settingsRepository, defaultPin, connection, transaction);
                pinSeeded = true;
            }

            if (settingsRepository.Get(AppSettingKeys.DatabaseCreatedAt, connection, transaction) is null)
            {
                settingsRepository.Set(AppSettingKeys.DatabaseCreatedAt,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    connection, transaction);
            }

            settingsRepository.Set(AppSettingKeys.SchemaVersion,
                CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture), connection, transaction);

            if (isNewDatabase || seededConfigCount > 0 || pinSeeded)
            {
                auditRepository.Insert(new AuditLogEntity
                {
                    ActionTime = DateTime.Now,
                    ActionType = AuditActionTypes.DatabaseInitialized,
                    TargetEntity = "Database",
                    Details = $"[{Environment.MachineName}] Khởi tạo CSDL tại '{databasePath}'. " +
                              $"DB mới: {(isNewDatabase ? "Có" : "Không")}; " +
                              $"Số cấu hình đặt hàng mặc định được nạp: {seededConfigCount}; " +
                              $"Nạp PIN mặc định: {(pinSeeded ? "Có" : "Không")}."
                }, connection, transaction);
            }

            transaction.Commit();
            IsInitialized = true;
        }
    }

    /// <summary>Đọc phiên bản schema hiện tại trong DB (0 nếu chưa có).</summary>
    public static int GetSchemaVersion()
    {
        var settingsRepository = new AppSettingsRepository();
        string? value = settingsRepository.Get(AppSettingKeys.SchemaVersion);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int version) ? version : 0;
    }
}
