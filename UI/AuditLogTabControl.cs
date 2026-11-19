using RollShopManager.Database;
using RollShopManager.Models;
using RollShopManager.Security;
using RollShopManager.Services;

namespace RollShopManager.UI;

/// <summary>
/// UserControl quản lý Nhật ký Kiểm toán &amp; An ninh Hệ thống (Tab NHẬT KÝ HỆ THỐNG - AUDIT LOGS):
/// - Theo dõi 100% lịch sử thêm/sửa/xóa trục, ca mài, nối chuỗi đường kính, mở khóa Admin, nhập sai PIN, sao lưu CSDL.
/// - Cung cấp công cụ Kiểm tra toàn vẹn vật lý CSDL (PRAGMA integrity_check) và Nối lại toàn bộ chuỗi đường kính.
/// </summary>
public sealed class AuditLogTabControl : UserControl
{
    private readonly RollBusinessService _rollService;

    private readonly KpiCardPanel _cardTotalEvents;
    private readonly KpiCardPanel _cardDataModifications;
    private readonly KpiCardPanel _cardSecurityEvents;
    private readonly KpiCardPanel _cardDbHealth;

    private readonly DateTimePicker _dtpFrom;
    private readonly DateTimePicker _dtpTo;
    private readonly ComboBox _cboActionFilter;
    private readonly TextBox _txtSearchKeyword;
    private readonly Button _btnRefresh;
    private readonly Button _btnRecalculateAllChains;
    private readonly Button _btnSeedDemoData;
    private readonly Button _btnClearAllData;

    private readonly DataGridView _dgvAuditLogs;
    private List<AuditLogEntity> _cachedLogs = new();

    public event EventHandler? DataChangedExternally;

    public AuditLogTabControl()
    {
        _rollService = new RollBusinessService();

        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        BackColor = IndustrialTheme.AppBackground;
        Font = IndustrialTheme.BodyFont;
        Padding = new Padding(16);

        TableLayoutPanel rootLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 105)); // 4 KPI Cards
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));  // Thanh lọc & Công cụ bảo trì
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Bảng AuditLog
        Controls.Add(rootLayout);

        // =====================================================================
        // PHẦN 1: 4 THẺ KPI AN NINH & TOÀN VẸN DỮ LIỆU
        // =====================================================================
        TableLayoutPanel kpiGrid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = Color.Transparent
        };
        for (int i = 0; i < 4; i++)
        {
            kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        }
        rootLayout.Controls.Add(kpiGrid, 0, 0);

        _cardTotalEvents = new KpiCardPanel
        {
            CardTitle = "TỔNG SỰ KIỆN TRONG KỲ",
            CardValue = "0",
            CardSubText = "Bản ghi kiểm toán lưu trữ",
            AccentColor = IndustrialTheme.PrimaryAccent,
            Margin = new Padding(0, 0, 6, 0)
        };
        kpiGrid.Controls.Add(_cardTotalEvents, 0, 0);

        _cardDataModifications = new KpiCardPanel
        {
            CardTitle = "THAO TÁC SỬA / XÓA DỮ LIỆU",
            CardValue = "0",
            CardSubText = "Sửa/Xóa ca mài & danh mục trục",
            AccentColor = IndustrialTheme.WarnBar,
            Margin = new Padding(6, 0, 6, 0)
        };
        kpiGrid.Controls.Add(_cardDataModifications, 1, 0);

        _cardSecurityEvents = new KpiCardPanel
        {
            CardTitle = "SỰ KIỆN PHÂN QUYỀN & PIN",
            CardValue = "0",
            CardSubText = "Mở khóa, tự khóa & cảnh báo sai PIN",
            AccentColor = IndustrialTheme.SecondarySlate,
            Margin = new Padding(6, 0, 6, 0)
        };
        kpiGrid.Controls.Add(_cardSecurityEvents, 2, 0);

        _cardDbHealth = new KpiCardPanel
        {
            CardTitle = "TRẠNG THÁI TOÀN VẸN CSDL",
            CardValue = "OK (WAL)",
            CardSubText = "PRAGMA integrity_check = ok",
            AccentColor = IndustrialTheme.SafeBar,
            Margin = new Padding(6, 0, 0, 0)
        };
        kpiGrid.Controls.Add(_cardDbHealth, 3, 0);

        // =====================================================================
        // PHẦN 2: THANH CÔNG CỤ LỌC & BẢO TRÌ TOÀN XƯỞNG
        // =====================================================================
        CardPanel toolbarCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0, 0, 0, 10)
        };
        rootLayout.Controls.Add(toolbarCard, 0, 1);

        TableLayoutPanel toolbarGrid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        toolbarGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
        toolbarGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
        toolbarCard.Controls.Add(toolbarGrid);

        FlowLayoutPanel filterLeft = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        toolbarGrid.Controls.Add(filterLeft, 0, 0);

        Label lblFrom = new() { Text = "Từ:", AutoSize = true, Margin = new Padding(0, 9, 4, 0), Font = IndustrialTheme.BodyBoldFont };
        _dtpFrom = new DateTimePicker
        {
            Width = 110,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy",
            Value = DateTime.Today.AddDays(-30),
            Margin = new Padding(0, 5, 10, 0)
        };
        _dtpFrom.ValueChanged += (s, e) => RefreshData();

        Label lblTo = new() { Text = "Đến:", AutoSize = true, Margin = new Padding(0, 9, 4, 0), Font = IndustrialTheme.BodyBoldFont };
        _dtpTo = new DateTimePicker
        {
            Width = 110,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy",
            Value = DateTime.Today,
            Margin = new Padding(0, 5, 10, 0)
        };
        _dtpTo.ValueChanged += (s, e) => RefreshData();

        Label lblType = new() { Text = "Loại:", AutoSize = true, Margin = new Padding(0, 9, 4, 0), Font = IndustrialTheme.BodyBoldFont };
        _cboActionFilter = new ComboBox { Width = 155 };
        IndustrialTheme.StyleComboBox(_cboActionFilter);
        _cboActionFilter.Margin = new Padding(0, 5, 10, 0);
        _cboActionFilter.Items.AddRange(new object[]
        {
            "Tất cả sự kiện",
            "Thêm / Sửa / Xóa Trục",
            "Nhật ký Ca mài & Nối chuỗi",
            "Bảo mật & Phân quyền PIN",
            "Báo cáo & Cấu hình Đặt hàng",
            "Khởi tạo & Sao lưu CSDL"
        });
        _cboActionFilter.SelectedIndex = 0;
        _cboActionFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

        Label lblSearch = new() { Text = "Tìm:", AutoSize = true, Margin = new Padding(0, 9, 4, 0), Font = IndustrialTheme.BodyBoldFont };
        _txtSearchKeyword = new TextBox { Width = 135 };
        IndustrialTheme.StyleInputBox(_txtSearchKeyword);
        _txtSearchKeyword.Margin = new Padding(0, 5, 8, 0);
        _txtSearchKeyword.TextChanged += (s, e) => ApplyFilter();

        _btnRefresh = new Button { Width = 80, Height = 32, Margin = new Padding(0, 3, 0, 0) };
        IndustrialTheme.StyleSecondaryButton(_btnRefresh, "Tải lại");
        _btnRefresh.Click += (s, e) => RefreshData();

        filterLeft.Controls.Add(lblFrom);
        filterLeft.Controls.Add(_dtpFrom);
        filterLeft.Controls.Add(lblTo);
        filterLeft.Controls.Add(_dtpTo);
        filterLeft.Controls.Add(lblType);
        filterLeft.Controls.Add(_cboActionFilter);
        filterLeft.Controls.Add(lblSearch);
        filterLeft.Controls.Add(_txtSearchKeyword);
        filterLeft.Controls.Add(_btnRefresh);

        FlowLayoutPanel toolsRight = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        toolbarGrid.Controls.Add(toolsRight, 1, 0);

        _btnRecalculateAllChains = new Button { Width = 175, Height = 32, Margin = new Padding(6, 3, 0, 0) };
        IndustrialTheme.StyleOutlineButton(_btnRecalculateAllChains, "🔧 Kiểm Tra Chuỗi Mài", IndustrialTheme.PrimaryAccent);
        _btnRecalculateAllChains.Click += OnRecalculateAllChainsClick;
        toolsRight.Controls.Add(_btnRecalculateAllChains);

        _btnClearAllData = new Button { Width = 155, Height = 32, Margin = new Padding(6, 3, 0, 0) };
        IndustrialTheme.StyleOutlineButton(_btnClearAllData, "🧹 Xóa Sạch Dữ Liệu", IndustrialTheme.DangerAccent);
        _btnClearAllData.Click += OnClearAllDataClick;
        toolsRight.Controls.Add(_btnClearAllData);

        _btnSeedDemoData = new Button { Width = 155, Height = 32, Margin = new Padding(0, 3, 0, 0) };
        IndustrialTheme.StyleOutlineButton(_btnSeedDemoData, "🧪 Nạp Dữ Liệu Mẫu", IndustrialTheme.SuccessAccent);
        _btnSeedDemoData.Click += OnSeedDemoDataClick;
        toolsRight.Controls.Add(_btnSeedDemoData);

        // =====================================================================
        // PHẦN 3: BẢNG DỮ LIỆU AUDIT LOGS
        // =====================================================================
        CardPanel gridCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0)
        };
        rootLayout.Controls.Add(gridCard, 0, 2);

        _dgvAuditLogs = new DataGridView { Dock = DockStyle.Fill };
        ConfigureColumns();
        IndustrialTheme.StyleDataGridView(_dgvAuditLogs);
        gridCard.Controls.Add(_dgvAuditLogs);

        AppSession.RoleChanged += (s, e) => ApplyRolePermissions();
        ApplyRolePermissions();
    }

    private void ConfigureColumns()
    {
        _dgvAuditLogs.Columns.Clear();
        _dgvAuditLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "Mã #", DataPropertyName = "Id", Width = 75 });
        _dgvAuditLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ActionTime",
            HeaderText = "Thời Gian Thực Hiện",
            DataPropertyName = "ActionTime",
            Width = 155,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy HH:mm:ss" }
        });
        _dgvAuditLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ActionType",
            HeaderText = "Tình Trạng / Loại Thao Tác",
            DataPropertyName = "ActionType",
            Width = 210
        });
        _dgvAuditLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "TargetEntity",
            HeaderText = "Phân Hệ",
            DataPropertyName = "TargetEntity",
            Width = 140
        });
        _dgvAuditLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Details",
            HeaderText = "Chi Tiết Thay Đổi (Máy trạm / Quyền / Dữ liệu Trước - Sau)",
            DataPropertyName = "Details",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
    }

    public void RefreshData()
    {
        if (_dtpFrom.Value.Date > _dtpTo.Value.Date)
        {
            return;
        }

        try
        {
            _cachedLogs = _rollService.GetAuditLogs(_dtpFrom.Value.Date, _dtpTo.Value.Date).ToList();
            UpdateKpiCards();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải nhật ký kiểm toán: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateKpiCards()
    {
        int total = _cachedLogs.Count;
        int modifications = _cachedLogs.Count(l =>
            l.ActionType.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) ||
            l.ActionType.Contains("DELETE", StringComparison.OrdinalIgnoreCase) ||
            l.ActionType.Contains("RECALCULATE", StringComparison.OrdinalIgnoreCase));

        int security = _cachedLogs.Count(l =>
            l.TargetEntity.Equals("Security", StringComparison.OrdinalIgnoreCase) ||
            l.ActionType.Contains("PIN", StringComparison.OrdinalIgnoreCase) ||
            l.ActionType.Contains("LOCK", StringComparison.OrdinalIgnoreCase));

        _cardTotalEvents.CardValue = total.ToString("N0");
        _cardDataModifications.CardValue = modifications.ToString("N0");
        _cardSecurityEvents.CardValue = security.ToString("N0");

        try
        {
            (bool isHealthy, string summary) = DatabaseMaintenance.CheckIntegrity();
            _cardDbHealth.CardValue = isHealthy ? "OK (WAL)" : "CẢNH BÁO";
            _cardDbHealth.CardSubText = summary;
            _cardDbHealth.AccentColor = isHealthy ? IndustrialTheme.SafeBar : IndustrialTheme.CritBar;
        }
        catch
        {
            _cardDbHealth.CardValue = "N/A";
        }
    }

    private void ApplyFilter()
    {
        int filterIndex = _cboActionFilter.SelectedIndex;
        string keyword = _txtSearchKeyword.Text.Trim().ToUpperInvariant();

        IEnumerable<AuditLogEntity> query = _cachedLogs;

        switch (filterIndex)
        {
            case 1: // Trục
                query = query.Where(l => l.TargetEntity.Equals("Rolls", StringComparison.OrdinalIgnoreCase));
                break;
            case 2: // Ca mài
                query = query.Where(l => l.TargetEntity.Equals("GrindingLogs", StringComparison.OrdinalIgnoreCase));
                break;
            case 3: // Bảo mật
                query = query.Where(l => l.TargetEntity.Equals("Security", StringComparison.OrdinalIgnoreCase) ||
                                         l.ActionType.Contains("PIN", StringComparison.OrdinalIgnoreCase) ||
                                         l.ActionType.Contains("LOCK", StringComparison.OrdinalIgnoreCase));
                break;
            case 4: // Báo cáo & Cấu hình
                query = query.Where(l => l.TargetEntity.Equals("ProcurementConfigs", StringComparison.OrdinalIgnoreCase) ||
                                         l.TargetEntity.Equals("ProductionPeriods", StringComparison.OrdinalIgnoreCase));
                break;
            case 5: // Database
                query = query.Where(l => l.TargetEntity.Equals("Database", StringComparison.OrdinalIgnoreCase));
                break;
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(l =>
                l.ActionType.ToUpperInvariant().Contains(keyword) ||
                l.TargetEntity.ToUpperInvariant().Contains(keyword) ||
                l.Details.ToUpperInvariant().Contains(keyword));
        }

        _dgvAuditLogs.DataSource = query.ToList();
        _dgvAuditLogs.ClearSelection();
    }

    private void OnRecalculateAllChainsClick(object? sender, EventArgs e)
    {
        if (!AppSession.IsAdmin)
        {
            MessageBox.Show("Chức năng này yêu cầu quyền Quản lý (Admin).", "Phân quyền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            (bool dbOk, string dbSummary) = DatabaseMaintenance.CheckIntegrity();
            int adjustedRows = _rollService.RecalculateAllRollChains();

            RefreshData();
            DataChangedExternally?.Invoke(this, EventArgs.Empty);

            MessageBox.Show(
                $"Đã hoàn tất kiểm tra toàn vẹn & rà soát chuỗi đường kính toàn xưởng!\n\n" +
                $"• Trạng thái CSDL: {dbSummary}\n" +
                $"• Số bản ghi ca mài cần điều chỉnh nối đuôi: {adjustedRows} dòng.",
                "Kiểm tra toàn vẹn hoàn tất",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi kiểm tra chuỗi đường kính: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSeedDemoDataClick(object? sender, EventArgs e)
    {
        if (!AppSession.IsAdmin)
        {
            MessageBox.Show("Vui lòng mở khóa quyền Quản lý (Admin) để nạp dữ liệu mẫu.", "Yêu cầu quyền Admin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult dr = MessageBox.Show(
            "Bạn muốn nạp Bộ dữ liệu mẫu thực tế Nhà máy Cán thép (14 trục WR/IMR/BUR + 41 ca mài + 3 kỳ sản lượng)?\n\n" +
            "• Chọn [Yes]: Giữ nguyên dữ liệu hiện có, chỉ bổ sung các trục mẫu chưa tồn tại.\n" +
            "• Chọn [No]: Xóa làm sạch dữ liệu cũ và nạp lại từ đầu bộ dữ liệu mẫu chuẩn.\n" +
            "• Chọn [Cancel]: Hủy thao tác.",
            "Nạp Dữ Liệu Mẫu Nhà Máy",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        if (dr == DialogResult.Cancel) return;

        bool clearExisting = (dr == DialogResult.No);

        try
        {
            var (rolls, logs, periods) = DemoDataSeeder.SeedFactoryDemoData(clearExisting);
            RefreshData();
            DataChangedExternally?.Invoke(this, EventArgs.Empty);

            MessageBox.Show(
                $"Đã nạp dữ liệu mẫu nhà máy thành công!\n\n" +
                $"• Số trục nạp mới: {rolls} cây\n" +
                $"• Số ca mài sinh tự động: {logs} ca\n" +
                $"• Số kỳ sản lượng mẫu: {periods} kỳ",
                "Nạp dữ liệu mẫu thành công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi nạp dữ liệu mẫu: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnClearAllDataClick(object? sender, EventArgs e)
    {
        if (!AppSession.IsAdmin)
        {
            MessageBox.Show("Vui lòng mở khóa quyền Quản lý (Admin) để xóa làm sạch dữ liệu.", "Yêu cầu quyền Admin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult confirm = MessageBox.Show(
            "CẢNH BÁO QUAN TRỌNG:\n\n" +
            "Bạn có chắc chắn muốn XÓA SẠCH toàn bộ dữ liệu vận hành (Danh mục trục, Nhật ký mài, Kỳ sản lượng) để đưa CSDL về trạng thái trống ban đầu không?\n\n" +
            "(Mẹo: Bạn có thể sao lưu file .db trước trong mục '⚙️ Đổi PIN / Backup')",
            "Xác Nhận Làm Sạch Cơ Sở Dữ Liệu",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirm != DialogResult.Yes) return;

        try
        {
            DemoDataSeeder.ClearAllOperationalData();
            RefreshData();
            DataChangedExternally?.Invoke(this, EventArgs.Empty);

            MessageBox.Show(
                "Đã xóa sạch toàn bộ dữ liệu thử nghiệm!\nCơ sở dữ liệu hiện đã trống 100%, sẵn sàng nhập liệu mới.",
                "Làm sạch CSDL hoàn tất",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi làm sạch dữ liệu: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyRolePermissions()
    {
        bool isAdmin = AppSession.IsAdmin;
        _btnRecalculateAllChains.Enabled = isAdmin;
        _btnSeedDemoData.Enabled = isAdmin;
        _btnClearAllData.Enabled = isAdmin;
    }
}
