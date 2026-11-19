using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using RollShopManager.Models;

namespace RollShopManager.Database;

/// <summary>
/// Sao lưu an toàn file RollShop.db bằng SQLite Online Backup API
/// (sao lưu nhất quán ngay cả khi ứng dụng đang mở kết nối / đang ở chế độ WAL).
/// </summary>
public static class DatabaseMaintenance
{
    public const string BackupFilePrefix = "RollShop_backup_";
    public const string BackupFileExtension = ".db";

    /// <summary>
    /// Sao lưu RollShop.db tới <paramref name="destinationPath"/>.
    /// - Nếu destinationPath là thư mục (đã tồn tại hoặc kết thúc bằng dấu '\'), file sẽ được đặt tên tự động
    ///   RollShop_backup_yyyyMMdd_HHmmss.db trong thư mục đó.
    /// - Quy trình: backup ra file tạm -> chuyển journal về DELETE (file độc lập) -> PRAGMA integrity_check
    ///   -> đổi tên file tạm thành file đích. File đích cũ chỉ bị ghi đè khi bản sao lưu mới đã hợp lệ.
    /// </summary>
    /// <returns>Đường dẫn tuyệt đối của file sao lưu.</returns>
    public static string BackupDatabase(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("Đường dẫn sao lưu không được để trống.", nameof(destinationPath));
        }

        string sourcePath = Path.GetFullPath(DbConnectionFactory.DatabasePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Không tìm thấy file cơ sở dữ liệu cần sao lưu.", sourcePath);
        }

        string finalPath = ResolveDestinationFilePath(destinationPath);
        if (string.Equals(finalPath, sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("File sao lưu không được trùng với file cơ sở dữ liệu đang sử dụng.");
        }

        string? destinationDirectory = Path.GetDirectoryName(finalPath);
        if (string.IsNullOrEmpty(destinationDirectory))
        {
            throw new InvalidOperationException($"Không xác định được thư mục đích của file sao lưu '{finalPath}'.");
        }

        Directory.CreateDirectory(destinationDirectory);

        string tempPath = finalPath + ".tmp";
        DeleteFileIfExists(tempPath);
        DeleteFileIfExists(tempPath + "-wal");
        DeleteFileIfExists(tempPath + "-shm");
        DeleteFileIfExists(tempPath + "-journal");

        try
        {
            using (SqliteConnection source = DbConnectionFactory.CreateOpenConnection())
            using (var destination = new SqliteConnection(
                       DbConnectionFactory.BuildConnectionString(tempPath, SqliteOpenMode.ReadWriteCreate, pooling: false)))
            {
                destination.Open();
                source.BackupDatabase(destination);
                destination.ExecuteScalar<string>("PRAGMA journal_mode = DELETE;");
            }

            using (var verify = new SqliteConnection(
                       DbConnectionFactory.BuildConnectionString(tempPath, SqliteOpenMode.ReadOnly, pooling: false)))
            {
                verify.Open();
                string? integrity = verify.ExecuteScalar<string>("PRAGMA integrity_check;");
                if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Bản sao lưu không vượt qua kiểm tra toàn vẹn (integrity_check = '{integrity}').");
                }

                long rollTableCount = verify.ExecuteScalar<long>(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'Rolls';");
                if (rollTableCount != 1)
                {
                    throw new InvalidOperationException("Bản sao lưu không chứa bảng Rolls - dữ liệu sao lưu không hợp lệ.");
                }
            }

            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch
        {
            DeleteFileIfExists(tempPath);
            DeleteFileIfExists(tempPath + "-journal");
            throw;
        }

        RecordBackup(finalPath);
        return finalPath;
    }

    /// <summary>
    /// Sao lưu vào thư mục chỉ định với tên tự động theo thời gian, đồng thời chỉ giữ lại
    /// <paramref name="keepLatestCount"/> bản sao lưu gần nhất do phần mềm tạo ra.
    /// </summary>
    public static string CreateTimestampedBackup(string backupFolder, int keepLatestCount = 30)
    {
        if (string.IsNullOrWhiteSpace(backupFolder))
        {
            throw new ArgumentException("Thư mục sao lưu không được để trống.", nameof(backupFolder));
        }

        if (keepLatestCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(keepLatestCount), "Phải giữ lại ít nhất 1 bản sao lưu.");
        }

        string folder = Path.GetFullPath(backupFolder);
        Directory.CreateDirectory(folder);

        string fileName = BuildTimestampedFileName(DateTime.Now);
        string backupPath = BackupDatabase(Path.Combine(folder, fileName));

        List<FileInfo> oldBackups = new DirectoryInfo(folder)
            .GetFiles(BackupFilePrefix + "*" + BackupFileExtension, SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(keepLatestCount)
            .ToList();

        foreach (FileInfo oldBackup in oldBackups)
        {
            oldBackup.Delete();
        }

        return backupPath;
    }

    /// <summary>
    /// Kiểm tra tính toàn vẹn vật lý và khóa ngoại của file RollShop.db hiện tại.
    /// </summary>
    public static (bool IsHealthy, string Summary) CheckIntegrity()
    {
        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        string integrity = connection.ExecuteScalar<string>("PRAGMA integrity_check;") ?? "unknown";
        long fkErrors = connection.ExecuteScalar<long>("SELECT COUNT(1) FROM pragma_foreign_key_check;");

        bool ok = string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase) && fkErrors == 0;
        string summary = ok
            ? "CSDL SQLite (WAL) hoàn toàn khỏe mạnh: PRAGMA integrity_check = ok, 0 lỗi khóa ngoại."
            : $"Cảnh báo toàn vẹn CSDL: integrity_check = '{integrity}', lỗi khóa ngoại = {fkErrors}.";

        return (ok, summary);
    }

    /// <summary>
    /// Khôi phục (Restore) cơ sở dữ liệu trực tiếp từ file sao lưu .db bằng SQLite Online Backup API.
    /// Trước khi ghi đè sẽ kiểm tra tính hợp lệ của file backup.
    /// </summary>
    public static void RestoreDatabase(string backupFilePath)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath))
        {
            throw new ArgumentException("Đường dẫn file sao lưu không được để trống.", nameof(backupFilePath));
        }

        string sourceBackupPath = Path.GetFullPath(backupFilePath);
        if (!File.Exists(sourceBackupPath))
        {
            throw new FileNotFoundException("Không tìm thấy file sao lưu để phục hồi.", sourceBackupPath);
        }

        string activePath = Path.GetFullPath(DbConnectionFactory.DatabasePath);
        if (string.Equals(sourceBackupPath, activePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("File sao lưu đang chọn trùng với file CSDL đang hoạt động.");
        }

        // 1. Kiểm tra tính toàn vẹn của file backup trước khi phục hồi
        using (var verifyConn = new SqliteConnection(
                   DbConnectionFactory.BuildConnectionString(sourceBackupPath, SqliteOpenMode.ReadOnly, pooling: false)))
        {
            verifyConn.Open();
            string? integrity = verifyConn.ExecuteScalar<string>("PRAGMA integrity_check;");
            if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"File sao lưu bị hỏng (integrity_check = '{integrity}'). Hủy phục hồi.");
            }

            long requiredTables = verifyConn.ExecuteScalar<long>(
                "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name IN ('Rolls', 'GrindingLogs', 'ProcurementConfigs');");
            if (requiredTables < 3)
            {
                throw new InvalidOperationException("File .db được chọn không đúng cấu trúc của RollShopManager.");
            }
        }

        // 2. Dùng SQLite Online Backup API chép từ file backup sang CSDL đang chạy
        using (var backupSource = new SqliteConnection(
                   DbConnectionFactory.BuildConnectionString(sourceBackupPath, SqliteOpenMode.ReadOnly, pooling: false)))
        using (SqliteConnection activeDestination = DbConnectionFactory.CreateOpenConnection())
        {
            backupSource.Open();
            backupSource.BackupDatabase(activeDestination);
        }

        // 3. Ghi nhận AuditLog sau khi phục hồi
        var auditRepository = new AuditLogRepository();
        auditRepository.Insert(new AuditLogEntity
        {
            ActionTime = DateTime.Now,
            ActionType = "RESTORE_DATABASE",
            TargetEntity = "Database",
            Details = $"[{Environment.MachineName}] Phục hồi CSDL từ file sao lưu '{sourceBackupPath}'."
        });
    }

    public static string BuildTimestampedFileName(DateTime timestamp)
    {
        return BackupFilePrefix + timestamp.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + BackupFileExtension;
    }

    private static string ResolveDestinationFilePath(string destinationPath)
    {
        string trimmed = destinationPath.Trim();
        bool endsWithSeparator = trimmed.EndsWith(Path.DirectorySeparatorChar) ||
                                 trimmed.EndsWith(Path.AltDirectorySeparatorChar);
        string fullPath = Path.GetFullPath(trimmed);

        if (endsWithSeparator || Directory.Exists(fullPath))
        {
            return Path.Combine(fullPath, BuildTimestampedFileName(DateTime.Now));
        }

        if (string.IsNullOrEmpty(Path.GetExtension(fullPath)))
        {
            fullPath += BackupFileExtension;
        }

        return fullPath;
    }

    private static void RecordBackup(string backupPath)
    {
        var settingsRepository = new AppSettingsRepository();
        var auditRepository = new AuditLogRepository();
        DateTime now = DateTime.Now;

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        settingsRepository.Set(AppSettingKeys.LastBackupAt,
            now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), connection, transaction);
        settingsRepository.Set(AppSettingKeys.LastBackupPath, backupPath, connection, transaction);

        long sizeBytes = new FileInfo(backupPath).Length;
        auditRepository.Insert(new AuditLogEntity
        {
            ActionTime = now,
            ActionType = AuditActionTypes.DatabaseBackup,
            TargetEntity = "Database",
            Details = $"[{Environment.MachineName}] Sao lưu CSDL thành công tới '{backupPath}' ({sizeBytes:N0} bytes)."
        }, connection, transaction);

        transaction.Commit();
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
