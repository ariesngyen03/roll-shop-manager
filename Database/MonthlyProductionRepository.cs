using Dapper;
using Microsoft.Data.Sqlite;
using RollShopManager.Models;

namespace RollShopManager.Database;

/// <summary>
/// Truy xuất và cập nhật dữ liệu bảng MonthlyProductions trong SQLite.
/// </summary>
public sealed class MonthlyProductionRepository : RepositoryBase
{
    private const string SelectColumns = "YearMonth, Year, Month, ProductionTons, Notes";

    public IReadOnlyList<MonthlyProductionEntity> GetAll(SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Query<MonthlyProductionEntity>(
                $"SELECT {SelectColumns} FROM MonthlyProductions ORDER BY YearMonth ASC;",
                transaction: t).AsList());
    }

    public IReadOnlyList<MonthlyProductionEntity> GetByYear(int year, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Query<MonthlyProductionEntity>(
                $"SELECT {SelectColumns} FROM MonthlyProductions WHERE Year = @Year ORDER BY Month ASC;",
                new { Year = year }, t).AsList());
    }

    public MonthlyProductionEntity? GetByYearMonth(string yearMonth, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonth);
        return WithConnection(connection, transaction, (c, t) =>
            c.QuerySingleOrDefault<MonthlyProductionEntity>(
                $"SELECT {SelectColumns} FROM MonthlyProductions WHERE YearMonth = @YearMonth;",
                new { YearMonth = yearMonth.Trim() }, t));
    }

    /// <summary>
    /// Thêm mới hoặc cập nhật sản lượng tháng theo YearMonth (Upsert).
    /// </summary>
    public int Upsert(MonthlyProductionEntity entity, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.YearMonth);

        return WithConnection(connection, transaction, (c, t) =>
            c.Execute(
                @"INSERT INTO MonthlyProductions (YearMonth, Year, Month, ProductionTons, Notes)
                  VALUES (@YearMonth, @Year, @Month, @ProductionTons, @Notes)
                  ON CONFLICT(YearMonth) DO UPDATE SET
                      ProductionTons = excluded.ProductionTons,
                      Notes          = excluded.Notes;",
                new
                {
                    YearMonth = entity.YearMonth.Trim(),
                    entity.Year,
                    entity.Month,
                    entity.ProductionTons,
                    entity.Notes
                }, t));
    }

    public int Delete(string yearMonth, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonth);
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute("DELETE FROM MonthlyProductions WHERE YearMonth = @YearMonth;",
                new { YearMonth = yearMonth.Trim() }, t));
    }
}
