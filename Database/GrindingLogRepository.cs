using Dapper;
using Microsoft.Data.Sqlite;
using RollShopManager.Models;

namespace RollShopManager.Database;

/// <summary>Truy xuất bảng GrindingLogs.</summary>
public sealed class GrindingLogRepository : RepositoryBase
{
    private const string SelectColumns =
        "g.Id, g.RollCode, g.GrindDate, g.PreGrindDiameter, g.PostGrindDiameter, g.IsAbnormal, g.Remarks";

    public IReadOnlyList<GrindingLogEntity> GetAll(SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Query<GrindingLogEntity>(
                $"SELECT {SelectColumns} FROM GrindingLogs g ORDER BY g.RollCode, g.GrindDate, g.Id;",
                transaction: t).AsList());
    }

    /// <summary>Lấy toàn bộ nhật ký mài của một trục, sắp xếp theo GrindDate ASC, Id ASC.</summary>
    public IReadOnlyList<GrindingLogEntity> GetByRollCode(string rollCode, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Query<GrindingLogEntity>(
                $"SELECT {SelectColumns} FROM GrindingLogs g WHERE g.RollCode = @RollCode ORDER BY g.GrindDate, g.Id;",
                new { RollCode = rollCode }, t).AsList());
    }

    /// <summary>Lấy nhật ký mài của toàn bộ trục thuộc một loại (phục vụ tab MAI_WR / MAI_IMR / MAI_BUR).</summary>
    public IReadOnlyList<GrindingLogEntity> GetByRollType(string rollType, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Query<GrindingLogEntity>(
                $@"SELECT {SelectColumns}
                     FROM GrindingLogs g
                    INNER JOIN Rolls r ON r.RollCode = g.RollCode
                    WHERE r.RollType = @RollType
                    ORDER BY g.RollCode, g.GrindDate, g.Id;",
                new { RollType = rollType }, t).AsList());
    }

    public GrindingLogEntity? GetById(int id, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.QuerySingleOrDefault<GrindingLogEntity>(
                $"SELECT {SelectColumns} FROM GrindingLogs g WHERE g.Id = @Id;",
                new { Id = id }, t));
    }

    /// <summary>Lấy toàn bộ nhật ký mài trong khoảng thời gian [FromDate, ToDate], sắp xếp theo GrindDate ASC, Id ASC.</summary>
    public IReadOnlyList<GrindingLogEntity> GetByDateRange(DateTime fromDate, DateTime toDate, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Query<GrindingLogEntity>(
                $"SELECT {SelectColumns} FROM GrindingLogs g WHERE g.GrindDate >= @FromDate AND g.GrindDate <= @ToDate ORDER BY g.GrindDate, g.Id;",
                new { FromDate = fromDate, ToDate = toDate }, t).AsList());
    }

    /// <summary>Lần mài mới nhất của trục theo GrindDate DESC, Id DESC.</summary>
    public GrindingLogEntity? GetLatestByRollCode(string rollCode, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.QueryFirstOrDefault<GrindingLogEntity>(
                $"SELECT {SelectColumns} FROM GrindingLogs g WHERE g.RollCode = @RollCode ORDER BY g.GrindDate DESC, g.Id DESC LIMIT 1;",
                new { RollCode = rollCode }, t));
    }

    public int CountByRollCode(string rollCode, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            (int)c.ExecuteScalar<long>(
                "SELECT COUNT(1) FROM GrindingLogs WHERE RollCode = @RollCode;",
                new { RollCode = rollCode }, t));
    }

    /// <summary>Thêm mới một dòng nhật ký mài, trả về Id vừa sinh.</summary>
    public int Insert(GrindingLogEntity log, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(log);
        return WithConnection(connection, transaction, (c, t) =>
            (int)c.ExecuteScalar<long>(
                @"INSERT INTO GrindingLogs (RollCode, GrindDate, PreGrindDiameter, PostGrindDiameter, IsAbnormal, Remarks)
                  VALUES (@RollCode, @GrindDate, @PreGrindDiameter, @PostGrindDiameter, @IsAbnormal, @Remarks);
                  SELECT last_insert_rowid();",
                new
                {
                    log.RollCode,
                    log.GrindDate,
                    log.PreGrindDiameter,
                    log.PostGrindDiameter,
                    IsAbnormal = log.IsAbnormal ? 1 : 0,
                    log.Remarks
                }, t));
    }

    public int Update(GrindingLogEntity log, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(log);
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute(
                @"UPDATE GrindingLogs
                     SET GrindDate         = @GrindDate,
                         PreGrindDiameter  = @PreGrindDiameter,
                         PostGrindDiameter = @PostGrindDiameter,
                         IsAbnormal        = @IsAbnormal,
                         Remarks           = @Remarks
                   WHERE Id = @Id;",
                new
                {
                    log.Id,
                    log.GrindDate,
                    log.PreGrindDiameter,
                    log.PostGrindDiameter,
                    IsAbnormal = log.IsAbnormal ? 1 : 0,
                    log.Remarks
                }, t));
    }

    public int UpdatePreGrindDiameter(int id, decimal preGrindDiameter, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute(
                "UPDATE GrindingLogs SET PreGrindDiameter = @PreGrindDiameter WHERE Id = @Id;",
                new { Id = id, PreGrindDiameter = preGrindDiameter }, t));
    }

    public int Delete(int id, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute("DELETE FROM GrindingLogs WHERE Id = @Id;", new { Id = id }, t));
    }

    public int DeleteByRollCode(string rollCode, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute("DELETE FROM GrindingLogs WHERE RollCode = @RollCode;", new { RollCode = rollCode }, t));
    }
}
