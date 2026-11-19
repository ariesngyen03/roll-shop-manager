using Dapper;
using Microsoft.Data.Sqlite;
using RollShopManager.Models;

namespace RollShopManager.Database;

/// <summary>Truy xuất bảng ProcurementConfigs.</summary>
public sealed class ProcurementConfigRepository : RepositoryBase
{
    private const string SelectColumns = "RollType, StandardUsableMmPerRoll, LeadTimeDays, SafetyStockRolls";

    public IReadOnlyList<ProcurementConfigEntity> GetAll(SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Query<ProcurementConfigEntity>(
                    $"SELECT {SelectColumns} FROM ProcurementConfigs;",
                    transaction: t)
                .OrderBy(config => RollTypes.GetSortOrder(config.RollType))
                .ToList());
    }

    public ProcurementConfigEntity? GetByType(string rollType, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.QuerySingleOrDefault<ProcurementConfigEntity>(
                $"SELECT {SelectColumns} FROM ProcurementConfigs WHERE RollType = @RollType;",
                new { RollType = rollType }, t));
    }

    /// <summary>Chèn cấu hình nếu loại trục chưa có cấu hình. Trả về 1 nếu chèn mới, 0 nếu đã tồn tại.</summary>
    public int InsertIfNotExists(ProcurementConfigEntity config, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute(
                @"INSERT OR IGNORE INTO ProcurementConfigs (RollType, StandardUsableMmPerRoll, LeadTimeDays, SafetyStockRolls)
                  VALUES (@RollType, @StandardUsableMmPerRoll, @LeadTimeDays, @SafetyStockRolls);",
                new
                {
                    config.RollType,
                    config.StandardUsableMmPerRoll,
                    config.LeadTimeDays,
                    config.SafetyStockRolls
                }, t));
    }

    public int Upsert(ProcurementConfigEntity config, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute(
                @"INSERT INTO ProcurementConfigs (RollType, StandardUsableMmPerRoll, LeadTimeDays, SafetyStockRolls)
                  VALUES (@RollType, @StandardUsableMmPerRoll, @LeadTimeDays, @SafetyStockRolls)
                  ON CONFLICT(RollType) DO UPDATE SET
                      StandardUsableMmPerRoll = excluded.StandardUsableMmPerRoll,
                      LeadTimeDays            = excluded.LeadTimeDays,
                      SafetyStockRolls        = excluded.SafetyStockRolls;",
                new
                {
                    config.RollType,
                    config.StandardUsableMmPerRoll,
                    config.LeadTimeDays,
                    config.SafetyStockRolls
                }, t));
    }
}
