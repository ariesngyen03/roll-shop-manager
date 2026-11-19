using Dapper;
using Microsoft.Data.Sqlite;
using RollShopManager.Models;

namespace RollShopManager.Database;

/// <summary>Truy xuất bảng ProductionPeriods.</summary>
public sealed class ProductionPeriodRepository : RepositoryBase
{
    private const string SelectColumns = "Id, PeriodName, FromDate, ToDate, ProductionTons";

    public IReadOnlyList<ProductionPeriodEntity> GetAll(SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Query<ProductionPeriodEntity>(
                $"SELECT {SelectColumns} FROM ProductionPeriods ORDER BY FromDate DESC, Id DESC;",
                transaction: t).AsList());
    }

    public ProductionPeriodEntity? GetById(int id, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.QuerySingleOrDefault<ProductionPeriodEntity>(
                $"SELECT {SelectColumns} FROM ProductionPeriods WHERE Id = @Id;",
                new { Id = id }, t));
    }

    public int Insert(ProductionPeriodEntity period, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(period);
        return WithConnection(connection, transaction, (c, t) =>
            (int)c.ExecuteScalar<long>(
                @"INSERT INTO ProductionPeriods (PeriodName, FromDate, ToDate, ProductionTons)
                  VALUES (@PeriodName, @FromDate, @ToDate, @ProductionTons);
                  SELECT last_insert_rowid();",
                new
                {
                    period.PeriodName,
                    period.FromDate,
                    period.ToDate,
                    period.ProductionTons
                }, t));
    }

    public int Update(ProductionPeriodEntity period, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(period);
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute(
                @"UPDATE ProductionPeriods
                     SET PeriodName     = @PeriodName,
                         FromDate       = @FromDate,
                         ToDate         = @ToDate,
                         ProductionTons = @ProductionTons
                   WHERE Id = @Id;",
                new
                {
                    period.Id,
                    period.PeriodName,
                    period.FromDate,
                    period.ToDate,
                    period.ProductionTons
                }, t));
    }

    public int Delete(int id, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute("DELETE FROM ProductionPeriods WHERE Id = @Id;", new { Id = id }, t));
    }
}
