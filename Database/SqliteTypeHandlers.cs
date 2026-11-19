using System.Data;
using System.Globalization;
using Dapper;

namespace RollShopManager.Database;

/// <summary>
/// Bộ chuyển đổi kiểu dữ liệu (TypeHandler) cho Dapper khi làm việc với Microsoft.Data.Sqlite:
/// - SQLite lưu decimal và DateTime dưới dạng TEXT (InvariantCulture) để bảo toàn độ chính xác tuyệt đối.
/// - Dapper cần TypeHandler để đọc ngược từ TEXT / REAL / INTEGER về đúng kiểu decimal, DateTime, bool
///   mà không phụ thuộc vào Region/Culture của máy tính Windows (dấu chấm hay dấu phẩy).
/// </summary>
public static class SqliteTypeHandlers
{
    private static bool _isRegistered;
    private static readonly object SyncLock = new();

    public static void Register()
    {
        lock (SyncLock)
        {
            if (_isRegistered)
            {
                return;
            }

            SqlMapper.AddTypeHandler(new DecimalTypeHandler());
            SqlMapper.AddTypeHandler(new NullableDecimalTypeHandler());
            SqlMapper.AddTypeHandler(new DateTimeTypeHandler());
            SqlMapper.AddTypeHandler(new NullableDateTimeTypeHandler());
            SqlMapper.AddTypeHandler(new BooleanTypeHandler());

            _isRegistered = true;
        }
    }

    private sealed class DecimalTypeHandler : SqlMapper.TypeHandler<decimal>
    {
        public override void SetValue(IDbDataParameter parameter, decimal value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString("0.########", CultureInfo.InvariantCulture);
        }

        public override decimal Parse(object value)
        {
            return value switch
            {
                decimal d => d,
                double db => Convert.ToDecimal(db, CultureInfo.InvariantCulture),
                float f => Convert.ToDecimal(f, CultureInfo.InvariantCulture),
                long l => l,
                int i => i,
                string s when decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed) => parsed,
                _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
            };
        }
    }

    private sealed class NullableDecimalTypeHandler : SqlMapper.TypeHandler<decimal?>
    {
        public override void SetValue(IDbDataParameter parameter, decimal? value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.HasValue
                ? value.Value.ToString("0.########", CultureInfo.InvariantCulture)
                : DBNull.Value;
        }

        public override decimal? Parse(object value)
        {
            if (value is null or DBNull)
            {
                return null;
            }

            if (value is string s && string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            return value switch
            {
                decimal d => d,
                double db => Convert.ToDecimal(db, CultureInfo.InvariantCulture),
                float f => Convert.ToDecimal(f, CultureInfo.InvariantCulture),
                long l => l,
                int i => i,
                string str when decimal.TryParse(str.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed) => parsed,
                _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
            };
        }
    }

    private sealed class DateTimeTypeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        public override DateTime Parse(object value)
        {
            return value switch
            {
                DateTime dt => dt,
                string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime parsed) => parsed,
                long ticks => new DateTime(ticks),
                _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            };
        }
    }

    private sealed class NullableDateTimeTypeHandler : SqlMapper.TypeHandler<DateTime?>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime? value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.HasValue
                ? value.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : DBNull.Value;
        }

        public override DateTime? Parse(object value)
        {
            if (value is null or DBNull)
            {
                return null;
            }

            if (value is string s && string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            return value switch
            {
                DateTime dt => dt,
                string str when DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime parsed) => parsed,
                long ticks => new DateTime(ticks),
                _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            };
        }
    }

    private sealed class BooleanTypeHandler : SqlMapper.TypeHandler<bool>
    {
        public override void SetValue(IDbDataParameter parameter, bool value)
        {
            parameter.DbType = DbType.Int64;
            parameter.Value = value ? 1L : 0L;
        }

        public override bool Parse(object value)
        {
            return value switch
            {
                bool b => b,
                long l => l != 0,
                int i => i != 0,
                short s => s != 0,
                byte by => by != 0,
                string str => str == "1" || bool.TryParse(str, out bool parsed) && parsed,
                _ => Convert.ToBoolean(value, CultureInfo.InvariantCulture)
            };
        }
    }
}
