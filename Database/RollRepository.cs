using Dapper;
using Microsoft.Data.Sqlite;
using RollShopManager.Models;

namespace RollShopManager.Database;

/// <summary>Truy xuất bảng Rolls.</summary>
public sealed class RollRepository : RepositoryBase
{
    private const string SelectColumns =
        "RollCode, RollType, CommissionDate, MaxDiameter, MinDiameter, InitialDiameter, InitialGrindCount, Notes";

    public IReadOnlyList<RollEntity> GetAll(SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Query<RollEntity>(
                $"SELECT {SelectColumns} FROM Rolls ORDER BY RollType, RollCode;",
                transaction: t).AsList());
    }

    public IReadOnlyList<RollEntity> GetByType(string rollType, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Query<RollEntity>(
                $"SELECT {SelectColumns} FROM Rolls WHERE RollType = @RollType ORDER BY RollCode;",
                new { RollType = rollType }, t).AsList());
    }

    public RollEntity? GetByCode(string rollCode, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.QuerySingleOrDefault<RollEntity>(
                $"SELECT {SelectColumns} FROM Rolls WHERE RollCode = @RollCode;",
                new { RollCode = rollCode }, t));
    }

    public bool Exists(string rollCode, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.ExecuteScalar<long>(
                "SELECT COUNT(1) FROM Rolls WHERE RollCode = @RollCode;",
                new { RollCode = rollCode }, t) > 0);
    }

    public int Insert(RollEntity roll, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(roll);
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute(
                @"INSERT INTO Rolls (RollCode, RollType, CommissionDate, MaxDiameter, MinDiameter, InitialDiameter, InitialGrindCount, Notes)
                  VALUES (@RollCode, @RollType, @CommissionDate, @MaxDiameter, @MinDiameter, @InitialDiameter, @InitialGrindCount, @Notes);",
                new
                {
                    roll.RollCode,
                    roll.RollType,
                    roll.CommissionDate,
                    roll.MaxDiameter,
                    roll.MinDiameter,
                    roll.InitialDiameter,
                    roll.InitialGrindCount,
                    roll.Notes
                }, t));
    }

    public int Update(RollEntity roll, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(roll);
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute(
                @"UPDATE Rolls
                     SET RollType          = @RollType,
                         CommissionDate    = @CommissionDate,
                         MaxDiameter       = @MaxDiameter,
                         MinDiameter       = @MinDiameter,
                         InitialDiameter   = @InitialDiameter,
                         InitialGrindCount = @InitialGrindCount,
                         Notes             = @Notes
                   WHERE RollCode = @RollCode;",
                new
                {
                    roll.RollCode,
                    roll.RollType,
                    roll.CommissionDate,
                    roll.MaxDiameter,
                    roll.MinDiameter,
                    roll.InitialDiameter,
                    roll.InitialGrindCount,
                    roll.Notes
                }, t));
    }

    public int Delete(string rollCode, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
    {
        return WithConnection(connection, transaction, (c, t) =>
            c.Execute("DELETE FROM Rolls WHERE RollCode = @RollCode;", new { RollCode = rollCode }, t));
    }
}
