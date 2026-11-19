using Microsoft.Data.Sqlite;

namespace RollShopManager.Database;

/// <summary>
/// Lớp cơ sở cho Repository: cho phép mỗi hàm tự mở kết nối riêng, hoặc chạy trên
/// kết nối / transaction do tầng Service truyền vào để gộp nhiều thao tác thành một giao dịch.
/// </summary>
public abstract class RepositoryBase
{
    protected static T WithConnection<T>(
        SqliteConnection? connection,
        SqliteTransaction? transaction,
        Func<SqliteConnection, SqliteTransaction?, T> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (connection is null && transaction is not null)
        {
            connection = transaction.Connection
                         ?? throw new InvalidOperationException("Transaction đã kết thúc, không còn gắn với kết nối nào.");
        }

        if (connection is not null)
        {
            if (transaction is not null && !ReferenceEquals(transaction.Connection, connection))
            {
                throw new InvalidOperationException("Transaction không thuộc về kết nối được truyền vào.");
            }

            return work(connection, transaction);
        }

        using SqliteConnection ownedConnection = DbConnectionFactory.CreateOpenConnection();
        return work(ownedConnection, null);
    }
}
