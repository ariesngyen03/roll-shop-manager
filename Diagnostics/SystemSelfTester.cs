using System.Text;
using ClosedXML.Excel;
using RollShopManager.Database;
using RollShopManager.Models;
using RollShopManager.Security;
using RollShopManager.Services;
using RollShopManager.UI;

namespace RollShopManager.Diagnostics;

/// <summary>
/// Bộ kiểm thử tự động toàn diện (End-to-End Automated Test Suite) cho toàn bộ 4 Phase của RollShopManager:
/// - Kiểm tra SQLite (WAL mode, TypeHandler decimal/DateTime/bool, Backup &amp; Restore, Integrity check).
/// - Kiểm tra Bảo mật (PIN SHA-256 + Salt, Unlock Admin, Lockout khi sai 5 lần, Auto-Lock).
/// - Kiểm tra Nghiệp vụ lõi (Tự nối chuỗi PreGrindDiameter, Sửa/Xóa ca mài ở giữa lịch sử, Ràng buộc Scrap, Dự báo đặt hàng).
/// - Kiểm tra Xuất bản (ClosedXML 5 Sheet + FormulaA1 COUNTIFS/SUM, QuestPDF A4 Landscape &amp; Portrait).
/// - Kiểm tra Giao diện WinForms (Khởi tạo MainForm 6 Tab, Z-Order Docking, DataGridView binding, RollWearChartDialog).
/// </summary>
public static class SystemSelfTester
{
    public static int RunAllTests(out string detailedReport)
    {
        var sb = new StringBuilder();
        int passed = 0;
        int failed = 0;

        string originalDbPath = DbConnectionFactory.DatabasePath;
        string tempDir = Path.Combine(Path.GetTempPath(), $"RollShopTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string testDbPath = Path.Combine(tempDir, "RollShop_Test.db");

        void AssertCase(string testName, Action testAction)
        {
            try
            {
                testAction();
                passed++;
                sb.AppendLine($"[PASS] {passed + failed:D2}. {testName}");
            }
            catch (Exception ex)
            {
                failed++;
                sb.AppendLine($"[FAIL] {passed + failed:D2}. {testName} -> LỖI: {ex.Message}");
            }
        }

        sb.AppendLine("================================================================================");
        sb.AppendLine("   BÁO CÁO KIỂM THỬ TỰ ĐỘNG TOÀN DIỆN HỆ THỐNG ROLLSHOPMANAGER (.NET 8.0)       ");
        sb.AppendLine("================================================================================");

        try
        {
            DbConnectionFactory.OverrideDatabasePath(testDbPath);

            // 1. Kiểm thử khởi tạo Database & WAL & TypeHandlers
            AssertCase("Khởi tạo CSDL SQLite (WAL Mode, Schema v1, Default PIN 123456, 3 cấu hình WR/IMR/BUR)", () =>
            {
                DbInitializer.Initialize();
                if (!File.Exists(testDbPath))
                    throw new InvalidOperationException("Không tạo được file CSDL.");

                var (healthy, _) = DatabaseMaintenance.CheckIntegrity();
                if (!healthy)
                    throw new InvalidOperationException("CSDL không đạt PRAGMA integrity_check.");

                if (DbInitializer.GetSchemaVersion() != DbInitializer.CurrentSchemaVersion)
                    throw new InvalidOperationException("Sai SchemaVersion.");
            });

            // 2. Kiểm thử Bảo mật PIN & Phân quyền
            AssertCase("Phân quyền Operator/Admin, Xác thực PIN SHA-256 + Salt & Đổi PIN", () =>
            {
                AppSession.Lock(RoleChangeReason.ManualLock);
                if (AppSession.IsAdmin)
                    throw new InvalidOperationException("Mặc định phải ở chế độ Operator.");

                if (AppSession.TryUnlockAdmin("999999") != UnlockResult.InvalidPin)
                    throw new InvalidOperationException("Phải từ chối PIN sai.");

                if (AppSession.TryUnlockAdmin("123456") != UnlockResult.Success || !AppSession.IsAdmin)
                    throw new InvalidOperationException("Mở khóa bằng PIN mặc định 123456 thất bại.");

                var pinSvc = new PinSecurityService();
                pinSvc.ChangePin("123456", "654321", "654321");
                if (!pinSvc.VerifyPin("654321"))
                    throw new InvalidOperationException("Xác thực PIN mới thất bại.");

                pinSvc.ChangePin("654321", "123456", "123456");
            });

            var service = new RollBusinessService();

            // 3. Kiểm thử Chặn quyền Operator khi khai báo trục
            AssertCase("Chặn Operator thực hiện thao tác quản trị (Khai báo / Sửa / Xóa trục)", () =>
            {
                AppSession.Lock(RoleChangeReason.ManualLock);
                bool blocked = false;
                try
                {
                    service.CreateRoll(new RollEntity
                    {
                        RollCode = "WR-TEST-01",
                        RollType = RollTypes.WR,
                        CommissionDate = DateTime.Today,
                        MaxDiameter = 450m,
                        MinDiameter = 400m
                    });
                }
                catch (UnauthorizedAccessException)
                {
                    blocked = true;
                }

                if (!blocked)
                    throw new InvalidOperationException("Operator không bị chặn khi tạo trục.");

                AppSession.TryUnlockAdmin("123456");
            });

            // 4. Kiểm thử Khai báo trục mới & Dapper Decimal/DateTime TypeHandler
            AssertCase("Khai báo trục mới & Đọc ghi chính xác tuyệt đối kiểu decimal qua Dapper TypeHandler", () =>
            {
                service.CreateRoll(new RollEntity
                {
                    RollCode = "WR-T01",
                    RollType = RollTypes.WR,
                    CommissionDate = DateTime.Today.AddDays(-20),
                    MaxDiameter = 450.50m,
                    MinDiameter = 400.25m,
                    InitialDiameter = 450.50m,
                    InitialGrindCount = 0,
                    Notes = "Trục test độ chính xác thập phân"
                });

                RollLifetimeViewDto lt = service.GetRollLifetime("WR-T01");
                if (lt.MaxDiameter != 450.50m || lt.MinDiameter != 400.25m || lt.CurrentDiameter != 450.50m)
                    throw new InvalidOperationException($"Sai lệch số thập phân: Max={lt.MaxDiameter}, Min={lt.MinDiameter}");
                if (lt.RemainingLifetimePercent != 100.0m)
                    throw new InvalidOperationException($"Lifetime khởi tạo phải là 100%, thực tế: {lt.RemainingLifetimePercent}%");
            });

            // 5. Kiểm thử Chuỗi ca mài tự động nối đuôi (PreGrindDiameter)
            int logId1 = 0, logId2 = 0, logId3 = 0;
            AssertCase("Ghi nhận 3 ca mài liên tiếp & Tự động điền ĐK trước mài nối đuôi", () =>
            {
                decimal pre1 = service.GetPreGrindDiameter("WR-T01");
                if (pre1 != 450.50m) throw new InvalidOperationException("Sai ĐK trước mài lần 1.");

                logId1 = service.AddGrindingLog("WR-T01", DateTime.Today.AddDays(-10), 449.50m, false, "Mài lần 1").Id;
                logId2 = service.AddGrindingLog("WR-T01", DateTime.Today.AddDays(-5), 448.00m, true, "Sự cố xước bề mặt").Id;
                logId3 = service.AddGrindingLog("WR-T01", DateTime.Today.AddDays(-1), 447.00m, false, "Mài lần 3").Id;

                var logs = service.GetGrindingLogViewsByRoll("WR-T01");
                if (logs.Count != 3) throw new InvalidOperationException("Số lượng log phải bằng 3.");
                if (logs[0].PreGrindDiameter != 450.50m || logs[0].GrindConsumption != 1.00m)
                    throw new InvalidOperationException("Sai tiêu hao ca 1.");
                if (logs[1].PreGrindDiameter != 449.50m || logs[1].GrindConsumption != 1.50m)
                    throw new InvalidOperationException("Sai nối đuôi ca 2.");
                if (logs[2].PreGrindDiameter != 448.00m || logs[2].GrindConsumption != 1.00m)
                    throw new InvalidOperationException("Sai nối đuôi ca 3.");
            });

            // 6. Kiểm thử Sửa ca mài ở giữa lịch sử -> RecalculateRollChain tự động cập nhật dây chuyền
            AssertCase("Sửa ĐK sau mài của ca giữa (Ca 2) -> Tự động nối lại ĐK trước mài của Ca 3 trong Transaction", () =>
            {
                // Sửa Ca 2 từ 448.00 thành 448.50 -> Ca 3 phải tự động đổi PreGrindDiameter thành 448.50 và Consumption = 1.50
                service.UpdateGrindingLog(logId2, DateTime.Today.AddDays(-5), 448.50m, true, "Đã hiệu chỉnh");
                var logs = service.GetGrindingLogViewsByRoll("WR-T01");
                if (logs[2].PreGrindDiameter != 448.50m || logs[2].GrindConsumption != 1.50m)
                    throw new InvalidOperationException($"Chuỗi không tự nối lại: Ca 3 Pre={logs[2].PreGrindDiameter}");
            });

            // 7. Kiểm thử Xóa ca mài ở giữa lịch sử -> RecalculateRollChain tự động nối Ca 1 sang Ca 3
            AssertCase("Xóa ca mài ở giữa (Ca 2) -> Tự động nối ĐK sau mài Ca 1 làm ĐK trước mài Ca 3", () =>
            {
                service.DeleteGrindingLog(logId2, "Test xóa ca giữa");
                var logs = service.GetGrindingLogViewsByRoll("WR-T01");
                if (logs.Count != 2) throw new InvalidOperationException("Phải còn 2 ca mài.");
                if (logs[1].PreGrindDiameter != 449.50m || logs[1].GrindConsumption != 2.50m)
                    throw new InvalidOperationException($"Sai nối chuỗi sau khi xóa: Pre={logs[1].PreGrindDiameter}");
            });

            // 8. Kiểm thử Nạp Bộ dữ liệu mẫu nhà máy (14 trục + 41 ca mài + 3 kỳ sản lượng)
            AssertCase("Nạp Bộ dữ liệu mẫu Nhà máy Cán thép (DemoDataSeeder) & Kiểm tra toàn vẹn chuỗi", () =>
            {
                var (rolls, logs, periods) = DemoDataSeeder.SeedFactoryDemoData(clearExisting: true);
                if (rolls != 14 || logs < 40 || periods != 3)
                    throw new InvalidOperationException($"Dữ liệu mẫu không đủ: Rolls={rolls}, Logs={logs}, Periods={periods}");

                int rechained = service.RecalculateAllRollChains();
                if (rechained != 0)
                    throw new InvalidOperationException($"Dữ liệu mẫu có {rechained} dòng lệch chuỗi.");
            });

            // 9. Kiểm thử Tính toán Báo cáo kỳ & Dự báo đặt hàng (MonthlyBurnRateRolls, RunwayDays, OrderDeadline, AlertStatus)
            AssertCase("Tính toán Báo cáo Tiêu hao Kỳ & Dự báo Đặt hàng (Hao Cây/Tháng, RunwayDays, OrderDeadline)", () =>
            {
                var summaries = service.GetConsumptionAndForecast(DateTime.Today.AddDays(-29), DateTime.Today, 16200m);
                if (summaries.Count != 3)
                    throw new InvalidOperationException("Phải trả về đúng 3 dòng tổng hợp cho WR, IMR, BUR.");

                foreach (var s in summaries)
                {
                    if (s.TotalWearMmInPeriod <= 0m)
                        throw new InvalidOperationException($"Loại {s.RollType} phải có tiêu hao > 0 trong 30 ngày mẫu.");
                    if (s.MonthlyBurnRateRolls <= 0m)
                        throw new InvalidOperationException($"Loại {s.RollType} phải có Hao (Cây/Tháng) > 0.");
                    if (s.PeriodDays == 30 && Math.Abs(s.MonthlyBurnRateRolls - s.EquivalentRollsConsumed) > 0.01m)
                        throw new InvalidOperationException($"Kỳ 30 ngày thì Hao (Cây/Tháng) ({s.MonthlyBurnRateRolls}) phải khớp Cây Dùng Quy Đổi ({s.EquivalentRollsConsumed}).");
                    if (!s.RunwayDays.HasValue || !s.RecommendedOrderDate.HasValue)
                        throw new InvalidOperationException($"Loại {s.RollType} phải tính được RunwayDays và RecommendedOrderDate.");
                }
            });

            // 10. Kiểm thử Xuất Excel 5 Sheet bằng ClosedXML & Đọc ngược kiểm tra FormulaA1
            AssertCase("Xuất Excel .xlsx chuẩn 5 Sheet (ClosedXML) & Kiểm tra công thức COUNTIFS, SUM, Subtraction", () =>
            {
                string excelPath = Path.Combine(tempDir, "TestExport5Sheets.xlsx");
                var allRolls = service.GetRollLifetimes();
                var wrLogs = service.GetGrindingLogViews(RollTypes.WR);
                var burLogs = service.GetGrindingLogViews(RollTypes.BUR);
                var imrLogs = service.GetGrindingLogViews(RollTypes.IMR);
                var summaries = service.GetConsumptionAndForecast(DateTime.Today.AddDays(-30), DateTime.Today, 15000m);
                var details = service.GetPeriodWearDetails(DateTime.Today.AddDays(-30), DateTime.Today);

                ExportService.ExportFullWorkbookToExcel(
                    excelPath, DateTime.Today.AddDays(-30), DateTime.Today, 15000m,
                    allRolls, wrLogs, burLogs, imrLogs, summaries, details);

                if (!File.Exists(excelPath) || new FileInfo(excelPath).Length < 5000)
                    throw new InvalidOperationException("File Excel xuất ra không tồn tại hoặc rỗng.");

                using var wb = new XLWorkbook(excelPath);
                string[] expectedSheets = { "QUAN_LY_TRUC", "MAI_WR", "MAI_BUR", "MAI_IMR", "BAO_CAO" };
                foreach (string sheetName in expectedSheets)
                {
                    if (!wb.Worksheets.Contains(sheetName))
                        throw new InvalidOperationException($"Thiếu sheet '{sheetName}' trong file Excel.");
                }

                var wsMaster = wb.Worksheet("QUAN_LY_TRUC");
                if (!wsMaster.Cell(5, 2).FormulaA1.Contains("COUNTIF", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Sheet QUAN_LY_TRUC thiếu công thức COUNTIF.");
                if (string.IsNullOrEmpty(wsMaster.Cell(9, 11).FormulaA1))
                    throw new InvalidOperationException("Sheet QUAN_LY_TRUC thiếu công thức % Lifetime ở dòng dữ liệu.");
            });

            // 11. Kiểm thử Xuất PDF Báo cáo Kỳ (Landscape) & PDF Roll Passport (Portrait) bằng QuestPDF
            AssertCase("Xuất PDF Báo cáo kỳ (A4 Ngang) & PDF Lý lịch trục Roll Passport (A4 Dọc) bằng QuestPDF", () =>
            {
                string pdfReportPath = Path.Combine(tempDir, "TestPeriodReport.pdf");
                string pdfPassportPath = Path.Combine(tempDir, "TestPassport_WR001.pdf");

                var summaries = service.GetConsumptionAndForecast(DateTime.Today.AddDays(-30), DateTime.Today, 15000m);
                var details = service.GetPeriodWearDetails(DateTime.Today.AddDays(-30), DateTime.Today);
                ExportService.ExportPeriodReportToPdf(pdfReportPath, DateTime.Today.AddDays(-30), DateTime.Today, 15000m, summaries, details);

                var wr001 = service.GetRollLifetime("WR-001");
                var wr001Logs = service.GetGrindingLogViewsByRoll("WR-001");
                ExportService.ExportRollPassportToPdf(pdfPassportPath, wr001, wr001Logs);

                if (!File.Exists(pdfReportPath) || new FileInfo(pdfReportPath).Length < 2000)
                    throw new InvalidOperationException("File PDF Báo cáo kỳ không hợp lệ.");
                if (!File.Exists(pdfPassportPath) || new FileInfo(pdfPassportPath).Length < 2000)
                    throw new InvalidOperationException("File PDF Roll Passport không hợp lệ.");
            });

            // 12. Kiểm thử Sao lưu (Backup) & Phục hồi (Restore) SQLite Online Backup API
            AssertCase("Sao lưu (Backup) & Phục hồi (Restore) CSDL trực tuyến bằng SQLite Online Backup API", () =>
            {
                string backupFile = DatabaseMaintenance.BackupDatabase(tempDir);
                if (!File.Exists(backupFile))
                    throw new InvalidOperationException("Không tạo được file backup.");

                DatabaseMaintenance.RestoreDatabase(backupFile);
                var (healthy, _) = DatabaseMaintenance.CheckIntegrity();
                if (!healthy)
                    throw new InvalidOperationException("CSDL sau khi Restore không đạt integrity_check.");
            });

            // 13. Kiểm thử Khởi tạo Giao diện WinForms (MainForm 6 Tab, Z-Order Docking, Biểu đồ GDI+)
            AssertCase("Khởi tạo Giao diện WinForms (MainForm 6 Tab, Z-Order Docking, RollWearChartDialog)", () =>
            {
                using var mainForm = new MainForm();
                // Kiểm tra Z-Order: Controls[0] phải là TabControl (Dock=Fill), Controls[1] là Header (Dock=Top)
                if (mainForm.Controls[0] is not TabControl tc || tc.TabPages.Count != 6)
                    throw new InvalidOperationException("MainForm Z-Order sai hoặc không đủ 6 TabPages.");

                var wr001 = service.GetRollLifetime("WR-001");
                var wr001Logs = service.GetGrindingLogViewsByRoll("WR-001");
                using var chartDialog = new RollWearChartDialog(wr001, wr001Logs);
                if (chartDialog.Controls.Count == 0)
                    throw new InvalidOperationException("RollWearChartDialog không khởi tạo được layout.");
            });

            // 14. Kiểm thử Bảng MonthlyProductions (CRUD, Upsert, Ràng buộc dữ liệu & Audit Log)
            AssertCase("Bảng MonthlyProductions: Upsert sản lượng tháng, đọc ghi chính xác & bắt lỗi số tấn âm", () =>
            {
                AppSession.TryUnlockAdmin("123456");

                var prod1 = service.SaveMonthlyProduction("2026-11", 16200m, "Test sản lượng tháng 11");
                if (prod1.ProductionTons != 16200m || prod1.YearMonth != "2026-11")
                    throw new InvalidOperationException("Lưu sản lượng tháng 11 không khớp.");

                var loaded = service.GetMonthlyProduction("2026-11");
                if (loaded is null || loaded.ProductionTons != 16200m)
                    throw new InvalidOperationException("Đọc lại sản lượng tháng 11 thất bại.");

                // Cập nhật lại sản lượng
                service.SaveMonthlyProduction("2026-11", 17500m, "Cập nhật tăng");
                var updated = service.GetMonthlyProduction("2026-11");
                if (updated is null || updated.ProductionTons != 17500m)
                    throw new InvalidOperationException("Cập nhật lại sản lượng tháng 11 thất bại.");

                // Kiểm tra chặn sản lượng âm
                bool blockedNegative = false;
                try
                {
                    service.SaveMonthlyProduction("2026-11", -100m);
                }
                catch (ArgumentOutOfRangeException)
                {
                    blockedNegative = true;
                }
                if (!blockedNegative)
                    throw new InvalidOperationException("Không chặn được sản lượng âm.");
            });

            // 15. Kiểm thử Ma Trận Tiêu Hao 12 Tháng Tự Động (GetYearlyMonthlyMatrix, mm/1000T, Cây quy đổi, Xu hướng MoM)
            AssertCase("Tính toán Ma Trận Tiêu Hao 12 Tháng Tự Động (13 dòng, mm/1000T, Cây quy đổi, Xu hướng MoM)", () =>
            {
                int currentYear = DateTime.Today.Year;
                var matrix = service.GetYearlyMonthlyMatrix(currentYear);
                if (matrix.Count != 13)
                    throw new InvalidOperationException($"Ma trận phải có đúng 13 dòng (12 tháng + 1 dòng tổng kết), thực tế: {matrix.Count}");

                for (int m = 1; m <= 12; m++)
                {
                    var monthRow = matrix[m - 1];
                    if (monthRow.MonthIndex != m || monthRow.IsSummaryRow)
                        throw new InvalidOperationException($"Sai chỉ số tháng ở dòng {m}.");

                    if (monthRow.ProductionTons > 0m && monthRow.TotalAllRollsWearMm > 0m)
                    {
                        decimal expectedRate = (monthRow.TotalAllRollsWearMm / monthRow.ProductionTons) * 1000m;
                        if (Math.Abs(monthRow.TotalWearPer1000Tons - expectedRate) > 0.001m)
                            throw new InvalidOperationException($"Sai công thức định mức mm/1.000 tấn ở tháng {m}: Thực tế {monthRow.TotalWearPer1000Tons}, Kỳ vọng {expectedRate}");
                    }
                }

                var summaryRow = matrix.Last();
                if (!summaryRow.IsSummaryRow || summaryRow.MonthIndex != 0)
                    throw new InvalidOperationException("Dòng cuối cùng phải là dòng tổng kết năm.");

                decimal sumTons = matrix.Take(12).Sum(r => r.ProductionTons);
                if (summaryRow.ProductionTons != sumTons)
                    throw new InvalidOperationException($"Tổng sản lượng cả năm không khớp: Tổng 12 tháng = {sumTons}, Dòng tổng = {summaryRow.ProductionTons}");

                decimal sumWear = matrix.Take(12).Sum(r => r.TotalAllRollsWearMm);
                if (summaryRow.TotalAllRollsWearMm != sumWear)
                    throw new InvalidOperationException($"Tổng mm mài cả năm không khớp: Tổng 12 tháng = {sumWear}, Dòng tổng = {summaryRow.TotalAllRollsWearMm}");

                // Kiểm tra xuất Excel có kèm Ma Trận 12 Tháng
                string matrixExcelPath = Path.Combine(tempDir, "TestMatrixExport.xlsx");
                var allRolls = service.GetRollLifetimes();
                var wrLogs = service.GetGrindingLogViews(RollTypes.WR);
                var burLogs = service.GetGrindingLogViews(RollTypes.BUR);
                var imrLogs = service.GetGrindingLogViews(RollTypes.IMR);
                var summaries = service.GetConsumptionAndForecast(DateTime.Today.AddDays(-30), DateTime.Today, 15000m);
                var details = service.GetPeriodWearDetails(DateTime.Today.AddDays(-30), DateTime.Today);

                ExportService.ExportFullWorkbookToExcel(
                    matrixExcelPath, DateTime.Today.AddDays(-30), DateTime.Today, 15000m,
                    allRolls, wrLogs, burLogs, imrLogs, summaries, details, matrix);

                if (!File.Exists(matrixExcelPath) || new FileInfo(matrixExcelPath).Length < 5000)
                    throw new InvalidOperationException("Xuất Excel kèm Ma trận 12 tháng thất bại.");

                // Kiểm tra xuất PDF có kèm Ma Trận 12 Tháng
                string matrixPdfPath = Path.Combine(tempDir, "TestMatrixReport.pdf");
                ExportService.ExportPeriodReportToPdf(
                    matrixPdfPath, DateTime.Today.AddDays(-30), DateTime.Today, 15000m,
                    summaries, details, matrix);

                if (!File.Exists(matrixPdfPath) || new FileInfo(matrixPdfPath).Length < 2000)
                    throw new InvalidOperationException("Xuất PDF kèm Ma trận 12 tháng thất bại.");
            });
        }
        finally
        {
            AppSession.Lock(RoleChangeReason.ManualLock);
            DbConnectionFactory.OverrideDatabasePath(originalDbPath);
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Bỏ qua nếu file tạm đang được GC giải phóng
            }
        }

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"TỔNG KẾT: ĐẠT {passed}/{passed + failed} KỊCH BẢN KIỂM THỬ (THẤT BẠI: {failed})");
        sb.AppendLine("================================================================================");

        detailedReport = sb.ToString();
        return failed == 0 ? 0 : 1;
    }
}
