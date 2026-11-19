using Dapper;
using Microsoft.Data.Sqlite;
using RollShopManager.Models;

namespace RollShopManager.Database;

/// <summary>Truy xuất bảng AuditLogs (chỉ ghi thêm, không sửa / xóa).</summary>
public sealed class AuditLogRepository : RepositoryBase
{
    private const string SelectColumns = "Id, ActionTime, ActionType, TargetEntity, Details";

    public int Insert(AuditLogEntity log, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(log);
        return WithConnection(connection, transaction, (c, t) =>
            (int)c.ExecuteScalar<long>(
                @"INSERT INTO AuditLogs (ActionTime, ActionType, TargetEntity, Details)
                  VALUES (@ActionTime, @ActionType, @TargetEntity, @Details);
                  SELECT last_insert_rowid();",
                new
                {
                    log.ActionTime,
                    log.ActionType,
                    log.TargetEntity,
                    log.Details
                }, t));
    }

    public IReadOnlyList<AuditLogEntity> GetRecent(int maxRows, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        if (maxRows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRows), "Số dòng cần lấy phải lớn hơn 0.");
        }

        return WithConnection(connection, transaction, (c, t) =>
            c.Query<AuditLogEntity>(
                $"SELECT {SelectColumns} FROM AuditLogs ORDER BY ActionTime DESC, Id DESC LIMIT @MaxRows;",
                new { MaxRows = maxRows }, t).AsList());
    }

    /// <summary>Lấy nhật ký trong khoảng [from, to] (bao gồm cả hai đầu).</summary>
    public IReadOnlyList<AuditLogEntity> GetByTimeRange(DateTime from, DateTime to, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        if (from > to)
        {
            throw new ArgumentException("Thời điểm bắt đầu phải nhỏ hơn hoặc bằng thời điểm kết thúc.", nameof(from));
        }

        return WithConnection(connection, transaction, (c, t) =>
            c.Query<AuditLogEntity>(
                $"SELECT {SelectColumns} FROM AuditLogs WHERE ActionTime >= @From AND ActionTime <= @To ORDER BY ActionTime DESC, Id DESC;",
                new { From = from, To = to }, t).AsList());
    }
}
