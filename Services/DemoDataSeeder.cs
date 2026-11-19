using Dapper;
using Microsoft.Data.Sqlite;
using RollShopManager.Database;
using RollShopManager.Models;

namespace RollShopManager.Services;

/// <summary>
/// Bộ tạo dữ liệu mẫu thực tế của Phân xưởng Mài Trục Cán (MES Roll Shop Demo Data):
/// - Nạp 14 cây trục (WR, IMR, BUR) đủ mọi trạng thái: Trục mới, Đang dùng, Trục cũ chuyển giao, Sắp hết đời (&lt;15%), Phế phẩm (Scrap).
/// - Nạp hơn 45 bản ghi Nhật ký mài nối đuôi đường kính chính xác 100% trải đều trong 40 ngày gần nhất.
/// - Nạp sẵn các Kỳ báo cáo sản lượng mẫu để kiểm thử ngay lập tức biểu đồ, báo cáo dự báo, xuất Excel 5 Sheet và PDF.
/// </summary>
public static class DemoDataSeeder
{
    /// <summary>
    /// Kiểm tra xem trong CSDL đã có bất kỳ cây trục nào chưa.
    /// </summary>
    public static bool IsDatabaseEmpty()
    {
        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        long count = connection.ExecuteScalar<long>("SELECT COUNT(1) FROM Rolls;");
        return count == 0;
    }

    /// <summary>
    /// Xóa sạch toàn bộ dữ liệu vận hành (Nhật ký mài, Danh mục trục, Kỳ báo cáo sản lượng, Nhật ký cũ)
    /// để đưa CSDL về trạng thái sạch 100% sẵn sàng bàn giao / vận hành chính thức,
    /// chỉ giữ lại cấu hình định mức đặt hàng chuẩn và mã PIN quản lý.
    /// </summary>
    public static void ClearAllOperationalData()
    {
        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using (SqliteTransaction transaction = connection.BeginTransaction())
        {
            connection.Execute("DELETE FROM GrindingLogs;", transaction: transaction);
            connection.Execute("DELETE FROM Rolls;", transaction: transaction);
            connection.Execute("DELETE FROM ProductionPeriods;", transaction: transaction);
            connection.Execute("DELETE FROM MonthlyProductions;", transaction: transaction);
            connection.Execute("DELETE FROM AuditLogs;", transaction: transaction);
            connection.Execute("DELETE FROM sqlite_sequence WHERE name IN ('GrindingLogs', 'ProductionPeriods', 'MonthlyProductions', 'AuditLogs');", transaction: transaction);

            var auditRepo = new AuditLogRepository();
            auditRepo.Insert(new AuditLogEntity
            {
                ActionTime = DateTime.Now,
                ActionType = "RESET_CLEAN_DB",
                TargetEntity = "Database",
                Details = $"[{Environment.MachineName}] Đã xóa sạch toàn bộ dữ liệu vận hành (Rolls, GrindingLogs, ProductionPeriods, MonthlyProductions) - Đưa CSDL về trạng thái gốc sạch 100%."
            }, connection, transaction);

            transaction.Commit();
        }

        connection.Execute("VACUUM;");
    }

    /// <summary>
    /// Nạp bộ dữ liệu mẫu nhà máy cán thép.
    /// Nếu <paramref name="clearExisting"/> = true, sẽ làm sạch dữ liệu trục/mài cũ trước khi nạp mới.
    /// </summary>
    public static (int RollsSeeded, int LogsSeeded, int PeriodsSeeded) SeedFactoryDemoData(bool clearExisting = false)
    {
        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        var rollRepo = new RollRepository();
        var logRepo = new GrindingLogRepository();
        var periodRepo = new ProductionPeriodRepository();
        var monthlyRepo = new MonthlyProductionRepository();
        var configRepo = new ProcurementConfigRepository();
        var auditRepo = new AuditLogRepository();

        if (clearExisting)
        {
            connection.Execute("DELETE FROM GrindingLogs;", transaction: transaction);
            connection.Execute("DELETE FROM Rolls;", transaction: transaction);
            connection.Execute("DELETE FROM ProductionPeriods;", transaction: transaction);
            connection.Execute("DELETE FROM MonthlyProductions;", transaction: transaction);
        }

        // Đảm bảo cấu hình định mức đặt hàng chuẩn
        configRepo.Upsert(new ProcurementConfigEntity
        {
            RollType = RollTypes.WR,
            StandardUsableMmPerRoll = 50m,
            LeadTimeDays = 35,
            SafetyStockRolls = 1.5m
        }, connection, transaction);

        configRepo.Upsert(new ProcurementConfigEntity
        {
            RollType = RollTypes.IMR,
            StandardUsableMmPerRoll = 50m,
            LeadTimeDays = 40,
            SafetyStockRolls = 1.0m
        }, connection, transaction);

        configRepo.Upsert(new ProcurementConfigEntity
        {
            RollType = RollTypes.BUR,
            StandardUsableMmPerRoll = 80m,
            LeadTimeDays = 45,
            SafetyStockRolls = 1.0m
        }, connection, transaction);

        DateTime today = DateTime.Today;

        // 1. Danh mục 14 cây trục mẫu
        var sampleRolls = new List<RollEntity>
        {
            // --- WORK ROLLS (WR: Max 450, Min 400 -> Vỏ 50mm) ---
            new()
            {
                RollCode = "WR-001",
                RollType = RollTypes.WR,
                CommissionDate = today.AddMonths(-6),
                MaxDiameter = 450.00m,
                MinDiameter = 400.00m,
                InitialDiameter = 450.00m,
                InitialGrindCount = 0,
                Notes = "Trục làm việc Giá cán số 1 - Thép hợp kim Cr5"
            },
            new()
            {
                RollCode = "WR-002",
                RollType = RollTypes.WR,
                CommissionDate = today.AddMonths(-8),
                MaxDiameter = 450.00m,
                MinDiameter = 400.00m,
                InitialDiameter = 434.00m,
                InitialGrindCount = 0,
                Notes = "Trục làm việc Giá cán số 1 (Bắt đầu theo dõi từ ĐK hiện trạng 434.00 mm)"
            },
            new()
            {
                RollCode = "WR-003",
                RollType = RollTypes.WR,
                CommissionDate = today.AddMonths(-2),
                MaxDiameter = 450.00m,
                MinDiameter = 400.00m,
                InitialDiameter = 450.00m,
                InitialGrindCount = 0,
                Notes = "Trục làm việc mới nhập lô Q3 - Độ bóng cao"
            },
            new()
            {
                RollCode = "WR-004",
                RollType = RollTypes.WR,
                CommissionDate = today.AddMonths(-14),
                MaxDiameter = 450.00m,
                MinDiameter = 400.00m,
                InitialDiameter = 412.50m,
                InitialGrindCount = 0,
                Notes = "Trục sắp hết tuổi thọ (<15%) - Cần theo dõi sát"
            },
            new()
            {
                RollCode = "WR-005",
                RollType = RollTypes.WR,
                CommissionDate = today.AddMonths(-18),
                MaxDiameter = 450.00m,
                MinDiameter = 400.00m,
                InitialDiameter = 404.20m,
                InitialGrindCount = 0,
                Notes = "Trục đã chạm ngưỡng phế phẩm (Scrap) trong tháng này"
            },
            new()
            {
                RollCode = "WR-006",
                RollType = RollTypes.WR,
                CommissionDate = today.AddMonths(-4),
                MaxDiameter = 450.00m,
                MinDiameter = 400.00m,
                InitialDiameter = 442.00m,
                InitialGrindCount = 0,
                Notes = "Trục làm việc dự phòng Giá cán số 2"
            },

            // --- INTERMEDIATE ROLLS (IMR: Max 650, Min 600 -> Vỏ 50mm) ---
            new()
            {
                RollCode = "IMR-001",
                RollType = RollTypes.IMR,
                CommissionDate = today.AddMonths(-9),
                MaxDiameter = 650.00m,
                MinDiameter = 600.00m,
                InitialDiameter = 638.00m,
                InitialGrindCount = 0,
                Notes = "Trục trung gian trên - Độ cứng 78 HSD"
            },
            new()
            {
                RollCode = "IMR-002",
                RollType = RollTypes.IMR,
                CommissionDate = today.AddMonths(-11),
                MaxDiameter = 650.00m,
                MinDiameter = 600.00m,
                InitialDiameter = 626.00m,
                InitialGrindCount = 0,
                Notes = "Trục trung gian dưới"
            },
            new()
            {
                RollCode = "IMR-003",
                RollType = RollTypes.IMR,
                CommissionDate = today.AddMonths(-16),
                MaxDiameter = 650.00m,
                MinDiameter = 600.00m,
                InitialDiameter = 611.20m,
                InitialGrindCount = 0,
                Notes = "Sắp chạm ngưỡng giới hạn (<15%), ưu tiên cán khổ hẹp"
            },
            new()
            {
                RollCode = "IMR-004",
                RollType = RollTypes.IMR,
                CommissionDate = today.AddMonths(-3),
                MaxDiameter = 650.00m,
                MinDiameter = 600.00m,
                InitialDiameter = 650.00m,
                InitialGrindCount = 0,
                Notes = "Trục trung gian mới 100%"
            },

            // --- BACKUP ROLLS (BUR: Max 1250, Min 1170 -> Vỏ 80mm) ---
            new()
            {
                RollCode = "BUR-001",
                RollType = RollTypes.BUR,
                CommissionDate = today.AddMonths(-12),
                MaxDiameter = 1250.00m,
                MinDiameter = 1170.00m,
                InitialDiameter = 1236.00m,
                InitialGrindCount = 0,
                Notes = "Trục đỡ chính - Thép rèn nguyên khối Cr3Mo"
            },
            new()
            {
                RollCode = "BUR-002",
                RollType = RollTypes.BUR,
                CommissionDate = today.AddMonths(-15),
                MaxDiameter = 1250.00m,
                MinDiameter = 1170.00m,
                InitialDiameter = 1218.00m,
                InitialGrindCount = 0,
                Notes = "Trục đỡ dưới - Chịu tải lực cán lớn"
            },
            new()
            {
                RollCode = "BUR-003",
                RollType = RollTypes.BUR,
                CommissionDate = today.AddMonths(-22),
                MaxDiameter = 1250.00m,
                MinDiameter = 1170.00m,
                InitialDiameter = 1185.00m,
                InitialGrindCount = 0,
                Notes = "Trục đỡ sắp hết đời (<15%), cần lên kế hoạch thay thế"
            },
            new()
            {
                RollCode = "BUR-004",
                RollType = RollTypes.BUR,
                CommissionDate = today.AddMonths(-26),
                MaxDiameter = 1250.00m,
                MinDiameter = 1170.00m,
                InitialDiameter = 1176.50m,
                InitialGrindCount = 0,
                Notes = "Trục đỡ đã thanh lý phế phẩm (Scrap)"
            }
        };

        int rollsSeeded = 0;
        var activeRollMap = new Dictionary<string, RollEntity>(StringComparer.OrdinalIgnoreCase);

        foreach (RollEntity roll in sampleRolls)
        {
            if (!rollRepo.Exists(roll.RollCode, connection, transaction))
            {
                rollRepo.Insert(roll, connection, transaction);
                rollsSeeded++;
                activeRollMap[roll.RollCode] = roll;
            }
        }

        // 2. Tạo chuỗi lịch sử ca mài nối đuôi tự động cho các trục vừa thêm
        var grindPlan = new List<(string Code, int DaysAgo, int Hour, decimal WearMm, bool Abnormal, string? Remarks)>
        {
            // WR-001 (Initial 450.00)
            ("WR-001", 34, 8,  0.80m, false, "Mài định kỳ sau ca cán tôn mạ"),
            ("WR-001", 27, 14, 0.85m, false, "Mài định kỳ chuẩn"),
            ("WR-001", 20, 9,  1.65m, true,  "Xước bề mặt do đứt dải tôn ca đêm"),
            ("WR-001", 14, 15, 0.75m, false, "Mài phục hồi độ nhám Ra 0.8"),
            ("WR-001", 8,  10, 0.80m, false, "Mài định kỳ sau 1.400 tấn"),
            ("WR-001", 3,  16, 0.85m, false, "Mài định kỳ chuẩn"),
            ("WR-001", 1,  9,  0.75m, false, "Mài chuẩn ca sáng"),

            // WR-002 (Initial 434.00)
            ("WR-002", 32, 10, 0.90m, false, "Mài định kỳ"),
            ("WR-002", 24, 16, 2.10m, true,  "Bám vảy cán + Nứt nhiệt nhẹ phần biên"),
            ("WR-002", 17, 11, 0.85m, false, "Mài định kỳ chuẩn"),
            ("WR-002", 11, 14, 0.90m, false, "Mài định kỳ chuẩn"),
            ("WR-002", 5,  8,  0.80m, false, "Mài định kỳ chuẩn"),
            ("WR-002", 2,  15, 0.85m, false, "Mài định kỳ sau 1.500 tấn"),

            // WR-003 (Initial 450.00)
            ("WR-003", 18, 9,  0.70m, false, "Mài rà lần đầu"),
            ("WR-003", 10, 13, 0.75m, false, "Mài định kỳ chuẩn"),
            ("WR-003", 4,  10, 0.80m, false, "Mài định kỳ chuẩn"),

            // WR-004 (Initial 412.50 -> đưa xuống < 15% tức < 407.50)
            ("WR-004", 29, 8,  1.20m, false, "Mài định kỳ"),
            ("WR-004", 21, 14, 1.95m, true,  "Cháy vệt bề mặt do mất nước làm mát"),
            ("WR-004", 13, 9,  1.10m, false, "Mài định kỳ"),
            ("WR-004", 6,  16, 1.35m, false, "Mài định kỳ"),
            ("WR-004", 1,  14, 1.30m, false, "Cảnh báo lượng dư còn rất thấp (<15%)"),

            // WR-005 (Initial 404.20 -> chạm ngưỡng Scrap <= 400.00)
            ("WR-005", 22, 10, 1.40m, false, "Mài tận dụng cuối đời"),
            ("WR-005", 12, 15, 2.85m, true,  "Bong tróc lớp tôi bề mặt - Chạm ngưỡng Scrap"),

            // WR-006 (Initial 442.00)
            ("WR-006", 15, 11, 0.80m, false, "Mài định kỳ chuẩn"),
            ("WR-006", 7,  15, 0.85m, false, "Mài định kỳ chuẩn"),

            // IMR-001 (Initial 638.00)
            ("IMR-001", 31, 9,  1.10m, false, "Mài định kỳ trục trung gian"),
            ("IMR-001", 19, 14, 1.15m, false, "Mài định kỳ chuẩn"),
            ("IMR-001", 9,  10, 1.20m, false, "Mài định kỳ chuẩn"),
            ("IMR-001", 2,  11, 1.10m, false, "Mài định kỳ chuẩn"),

            // IMR-002 (Initial 626.00)
            ("IMR-002", 26, 13, 1.25m, false, "Mài định kỳ"),
            ("IMR-002", 16, 8,  2.40m, true,  "Lõm cục bộ do kẹt đầu băng thép"),
            ("IMR-002", 6,  14, 1.20m, false, "Mài định kỳ chuẩn"),

            // IMR-003 (Initial 611.20 -> đưa xuống < 15% tức < 607.50)
            ("IMR-003", 25, 10, 1.30m, false, "Mài định kỳ"),
            ("IMR-003", 14, 16, 1.45m, false, "Mài định kỳ"),
            ("IMR-003", 4,  9,  2.15m, true,  "Mòn bậc vai trục - Đã xuống dưới 15% Lifetime"),

            // IMR-004 (Initial 650.00)
            ("IMR-004", 12, 10, 1.00m, false, "Mài định kỳ ca đầu"),
            ("IMR-004", 3,  15, 1.05m, false, "Mài định kỳ chuẩn"),

            // BUR-001 (Initial 1236.00)
            ("BUR-001", 28, 8,  2.20m, false, "Mài đại tu chu kỳ 15.000 tấn"),
            ("BUR-001", 11, 13, 2.30m, false, "Mài định kỳ trục đỡ"),
            ("BUR-001", 2,  16, 2.10m, false, "Mài vát mép chống mỏi"),

            // BUR-002 (Initial 1218.00)
            ("BUR-002", 23, 9,  2.50m, false, "Mài định kỳ trục đỡ"),
            ("BUR-002", 8,  14, 3.80m, true,  "Rỗ mỏi tiếp xúc (Spalling) - Mài sâu khử vết nứt"),

            // BUR-003 (Initial 1185.00 -> đưa xuống < 15% tức < 1182.00)
            ("BUR-003", 19, 11, 2.40m, false, "Mài định kỳ"),
            ("BUR-003", 5,  10, 4.20m, true,  "Nứt mỏi biên trục - Lifetime còn ~10.5%"),

            // BUR-004 (Initial 1176.50 -> chạm Scrap <= 1170.00)
            ("BUR-004", 30, 15, 3.10m, false, "Mài chu kỳ cuối"),
            ("BUR-004", 15, 11, 3.60m, true,  "Hết lớp hợp kim chịu lực - Đưa vào Scrap")
        };

        int logsSeeded = 0;
        var currentDiaTracker = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in activeRollMap)
        {
            currentDiaTracker[kv.Key] = kv.Value.InitialDiameter;
        }

        foreach (var item in grindPlan)
        {
            if (!currentDiaTracker.TryGetValue(item.Code, out decimal preDia))
            {
                continue;
            }

            decimal postDia = Math.Round(preDia - item.WearMm, 3);
            DateTime grindTime = today.AddDays(-item.DaysAgo).AddHours(item.Hour).AddMinutes(15);
            if (grindTime > DateTime.Now)
            {
                grindTime = DateTime.Now.AddMinutes(-30);
            }

            logRepo.Insert(new GrindingLogEntity
            {
                RollCode = item.Code,
                GrindDate = grindTime,
                PreGrindDiameter = preDia,
                PostGrindDiameter = postDia,
                IsAbnormal = item.Abnormal,
                Remarks = item.Remarks
            }, connection, transaction);

            currentDiaTracker[item.Code] = postDia;
            logsSeeded++;
        }

        // 3. Tạo các kỳ báo cáo sản lượng mẫu
        int periodsSeeded = 0;
        IReadOnlyList<ProductionPeriodEntity> existingPeriods = periodRepo.GetAll(connection, transaction);
        if (existingPeriods.Count == 0)
        {
            DateTime firstDayThisMonth = new(today.Year, today.Month, 1);
            DateTime lastMonthEnd = firstDayThisMonth.AddDays(-1);
            DateTime lastMonthStart = new(lastMonthEnd.Year, lastMonthEnd.Month, 1);

            periodRepo.Insert(new ProductionPeriodEntity
            {
                PeriodName = $"Kỳ Tháng {lastMonthStart:MM/yyyy} (18,500 Tấn)",
                FromDate = lastMonthStart,
                ToDate = lastMonthEnd,
                ProductionTons = 18500m
            }, connection, transaction);

            periodRepo.Insert(new ProductionPeriodEntity
            {
                PeriodName = $"Kỳ 30 Ngày Gần Nhất (16,200 Tấn)",
                FromDate = today.AddDays(-29),
                ToDate = today,
                ProductionTons = 16200m
            }, connection, transaction);

            periodRepo.Insert(new ProductionPeriodEntity
            {
                PeriodName = $"Kỳ Tháng Hiện Tại {today:MM/yyyy} (8,400 Tấn)",
                FromDate = firstDayThisMonth,
                ToDate = today,
                ProductionTons = 8400m
            }, connection, transaction);

            periodsSeeded = 3;
        }

        // 4. Tạo sản lượng cán thép theo tháng khớp với các tháng thực tế có nhật ký mài
        DateTime prevMonthDate = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
        var activeMonthSamples = new (int Year, int Month, decimal Tons, string Note)[]
        {
            (prevMonthDate.Year, prevMonthDate.Month, 18500m, $"Sản lượng thực tế tháng {prevMonthDate:MM/yyyy}"),
            (today.Year, today.Month, 16200m, $"Sản lượng lũy kế tháng hiện tại {today:MM/yyyy}")
        };

        foreach (var item in activeMonthSamples)
        {
            string ym = $"{item.Year:D4}-{item.Month:D2}";
            var existing = monthlyRepo.GetByYearMonth(ym, connection, transaction);
            if (existing is null)
            {
                monthlyRepo.Upsert(new MonthlyProductionEntity
                {
                    YearMonth = ym,
                    Year = item.Year,
                    Month = item.Month,
                    ProductionTons = item.Tons,
                    Notes = item.Note
                }, connection, transaction);
            }
        }

        auditRepo.Insert(new AuditLogEntity
        {
            ActionTime = DateTime.Now,
            ActionType = "SEED_DEMO_DATA",
            TargetEntity = "Database",
            Details = $"[{Environment.MachineName}] Nạp bộ dữ liệu mẫu nhà máy: {rollsSeeded} trục, {logsSeeded} ca mài, {periodsSeeded} kỳ báo cáo."
        }, connection, transaction);

        transaction.Commit();
        return (rollsSeeded, logsSeeded, periodsSeeded);
    }
}
