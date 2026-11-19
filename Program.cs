using System.Text;
using RollShopManager.Database;
using RollShopManager.Diagnostics;
using RollShopManager.Security;
using RollShopManager.Services;
using RollShopManager.UI;

namespace RollShopManager;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // 1. Cấu hình DPI và giao diện WinForms hiện đại
        ApplicationConfiguration.Initialize();

        // Chế độ kiểm thử tự động toàn diện qua CLI: dotnet run -- --self-test
        if (args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            int code = SystemSelfTester.RunAllTests(out string report);
            string reportFile = Path.Combine(AppContext.BaseDirectory, "SelfTestReport.txt");
            File.WriteAllText(reportFile, report, Encoding.UTF8);
            return code;
        }

        // 2. Khởi tạo Cơ sở dữ liệu SQLite (Tạo bảng, Index, nạp config mặc định WR/IMR/BUR, nạp mã PIN 123456)
        DbInitializer.Initialize();

        // Hỗ trợ nạp dữ liệu mẫu và thoát (dùng cho chuẩn bị môi trường): dotnet run -- --seed-demo-only
        if (args.Any(a => string.Equals(a, "--seed-demo-only", StringComparison.OrdinalIgnoreCase)))
        {
            DemoDataSeeder.SeedFactoryDemoData(clearExisting: false);
            return 0;
        }

        // Hỗ trợ nạp sẵn dữ liệu mẫu qua tham số: dotnet run -- --seed-demo
        if (args.Any(a => string.Equals(a, "--seed-demo", StringComparison.OrdinalIgnoreCase)))
        {
            DemoDataSeeder.SeedFactoryDemoData(clearExisting: true);
        }

        // Hỗ trợ xóa sạch dữ liệu vận hành qua tham số: dotnet run -- --clean-db-only
        if (args.Any(a => string.Equals(a, "--clean-db-only", StringComparison.OrdinalIgnoreCase)))
        {
            DemoDataSeeder.ClearAllOperationalData();
            return 0;
        }

        // Hỗ trợ tạo cặp DB (1 DB mẫu dự phòng test + 1 DB chính sạch dữ liệu): dotnet run -- --prepare-package-dbs <folder>
        int prepIndex = Array.FindIndex(args, a => string.Equals(a, "--prepare-package-dbs", StringComparison.OrdinalIgnoreCase));
        if (prepIndex >= 0 && prepIndex + 1 < args.Length)
        {
            string targetDir = Path.GetFullPath(args[prepIndex + 1]);
            string backupSubDir = Path.Combine(targetDir, "Database_Mau_DuPhong");
            Directory.CreateDirectory(backupSubDir);

            // Bước 1: Nạp đầy đủ dữ liệu mẫu và xuất ra file RollShop_Sample_Test.db
            DemoDataSeeder.SeedFactoryDemoData(clearExisting: true);
            string sampleDbPath = Path.Combine(backupSubDir, "RollShop_Sample_Test.db");
            DatabaseMaintenance.BackupDatabase(sampleDbPath);

            // Bước 2: Xóa sạch toàn bộ dữ liệu vận hành để DB chính trở về trạng thái sạch 100%
            DemoDataSeeder.ClearAllOperationalData();
            string cleanBackupPath = Path.Combine(backupSubDir, "RollShop_Empty_Clean.db");
            DatabaseMaintenance.BackupDatabase(cleanBackupPath);

            // Bước 3: Xuất bản DB sạch ra thư mục đóng gói chính
            string mainPackageDbPath = Path.Combine(targetDir, DbConnectionFactory.DatabaseFileName);
            if (!string.Equals(mainPackageDbPath, DbConnectionFactory.DatabasePath, StringComparison.OrdinalIgnoreCase))
            {
                DatabaseMaintenance.BackupDatabase(mainPackageDbPath);
            }

            return 0;
        }

        // 3. Đăng ký bộ lọc thông điệp Windows toàn ứng dụng để theo dõi thao tác chuột và bàn phím (Auto-Lock sau 3 phút)
        Application.AddMessageFilter(new IdleActivityMessageFilter());

        // 4. Khởi chạy Form chính của hệ thống
        Application.Run(new MainForm());
        return 0;
    }
}
