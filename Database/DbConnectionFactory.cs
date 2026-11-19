using Microsoft.Data.Sqlite;

namespace RollShopManager.Database;

/// <summary>
/// Quản lý đường dẫn file RollShop.db và tạo kết nối SQLite.
/// </summary>
public static class DbConnectionFactory
{
    public const string DatabaseFileName = "RollShop.db";

    /// <summary>Thời gian chờ khi DB đang bị khóa bởi thao tác khác (ms).</summary>
    public const int BusyTimeoutMilliseconds = 5000;

    private static readonly object PathLock = new();
    private static string _databasePath = Path.Combine(AppContext.BaseDirectory, DatabaseFileName);

    static DbConnectionFactory()
    {
        SqliteTypeHandlers.Register();
    }

    /// <summary>Đường dẫn tuyệt đối tới RollShop.db (mặc định nằm cùng thư mục file .exe).</summary>
    public static string DatabasePath
    {
        get
        {
            lock (PathLock)
            {
                return _databasePath;
            }
        }
    }

    public static string ConnectionString => BuildConnectionString(DatabasePath, SqliteOpenMode.ReadWriteCreate, pooling: true);

    /// <summary>
    /// Đổi vị trí file DB (dùng cho kiểm thử hoặc chạy trên ổ dữ liệu riêng). Phải gọi trước DbInitializer.Initialize().
    /// </summary>
    public static void OverrideDatabasePath(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Đường dẫn cơ sở dữ liệu không được để trống.", nameof(databasePath));
        }

        string fullPath = Path.GetFullPath(databasePath);
        lock (PathLock)
        {
            SqliteConnection.ClearAllPools();
            _databasePath = fullPath;
        }
    }

    public static string BuildConnectionString(string databasePath, SqliteOpenMode mode, bool pooling)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = mode,
            Cache = SqliteCacheMode.Private,
            ForeignKeys = true,
            Pooling = pooling,
            DefaultTimeout = 30
        };
        return builder.ToString();
    }

    /// <summary>Mở kết nối tới RollShop.db (đã bật foreign_keys và busy_timeout).</summary>
    public static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        try
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds};";
            command.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
