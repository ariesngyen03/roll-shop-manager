using System.Globalization;
using RollShopManager.Models;
using RollShopManager.Security;
using RollShopManager.Services;

namespace RollShopManager.UI;

/// <summary>
/// Phân hệ Báo Cáo Sản Lượng, Tổng Hợp Theo Tháng, Phân Tích Định Mức &amp; Dự Báo Đặt Hàng Trục (Tab BAO_CAO).
/// Giao diện tối giản, tập trung vào các chỉ số cốt lõi:
/// - Tầng 0: Thanh điều khiển chọn kỳ báo cáo (Theo Tháng hoặc Từ ngày - Đến ngày) &amp; Sản lượng Tấn.
/// - Tầng 1: Bảng Định mức Tiêu hao &amp; Dự báo Đặt hàng theo 3 chủng loại (WR - IMR - BUR).
/// - Tầng 2: Bảng Tổng hợp theo Tháng (ẩn các tháng trống) &lt;-&gt; Chi tiết từng cây trục.
/// </summary>
public sealed class ReportAndForecastTabControl : UserControl
{
    private readonly RollBusinessService _rollService;

    // Thanh Toast Banner thông báo phi gián đoạn
    private readonly Panel _pnlToastBanner;
    private readonly Label _lblToastMessage;
    private readonly System.Windows.Forms.Timer _toastTimer;

    // Điều khiển chọn chế độ báo cáo
    private readonly ComboBox _cboViewMode;

    // Điều khiển Chế độ 1: Theo Tháng / Năm
    private readonly FlowLayoutPanel _pnlModeYear;
    private readonly ComboBox _cboYear;
    private readonly ComboBox _cboFocusMonth;
    private readonly NumericUpDown _nudQuickMonthTons;
    private readonly Button _btnQuickSaveMonthTons;
    private bool _suppressMonthSelectorChange;

    // Điều khiển Chế độ 2: Khoảng ngày tùy chỉnh (Từ ngày - Đến ngày & Sản lượng Tấn)
    private readonly FlowLayoutPanel _pnlModeCustomRange;
    private readonly DateTimePicker _dtpFromDate;
    private readonly DateTimePicker _dtpToDate;
    private readonly NumericUpDown _nudProductionTons;
    private readonly Button _btnCalculate;

    // Nút chức năng chung bên phải
    private readonly Button _btnConfigProcurement;
    private readonly Button _btnExportExcel;
    private readonly Button _btnExportPdf;

    // Tầng 1: Bảng Tổng hợp theo Chủng loại (WR, IMR, BUR) & Cảnh báo đặt hàng
    private readonly Label _lblTypeSummaryTitle;
    private readonly DataGridView _dgvTypeSummary;

    // Tầng 2: Không gian chi tiết chuyển đổi giữa [Bảng Tổng hợp Tháng] và [Chi tiết từng cây trục]
    private readonly Button _btnShowMatrixView;
    private readonly Button _btnShowRollDetailView;
    private readonly CheckBox _chkHideEmptyMonths;
    private readonly Label _lblFilterRollType;
    private readonly ComboBox _cboDetailFilterType;
    private readonly Label _lblInlineEditHint;

    private readonly DataGridView _dgvMonthlyMatrix;
    private readonly DataGridView _dgvRollWearDetail;
    private bool _suppressMatrixSelectionChange;
    private int _activeBottomSubTab; // 0 = Matrix, 1 = Roll Detail

    // Bộ nhớ đệm dữ liệu đang hiển thị
    private List<MonthlyConsumptionMatrixDto> _lastYearlyMatrix = new();
    private List<TypeConsumptionAndForecastDto> _lastTypeSummaries = new();
    private List<RollPeriodWearDto> _lastRollDetails = new();
    private MonthlyConsumptionMatrixDto? _selectedMonthDto;
    private DateTime _currentActiveFromDate;
    private DateTime _currentActiveToDate;
    private decimal _currentActiveProductionTons;
    private string _currentActivePeriodLabel = string.Empty;

    public ReportAndForecastTabControl()
    {
        _rollService = new RollBusinessService();

        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        BackColor = IndustrialTheme.AppBackground;
        Font = IndustrialTheme.BodyFont;
        Padding = new Padding(14, 8, 14, 10);

        // =====================================================================
        // TOAST BANNER THÔNG BÁO PHI GIÁN ĐOẠN
        // =====================================================================
        _pnlToastBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = IndustrialTheme.SafeBg,
            Padding = new Padding(12, 4, 12, 4),
            Visible = false
        };
        _lblToastMessage = new Label
        {
            Dock = DockStyle.Fill,
            Font = IndustrialTheme.BodyBoldFont,
            ForeColor = IndustrialTheme.SafeText,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        _pnlToastBanner.Controls.Add(_lblToastMessage);

        _toastTimer = new System.Windows.Forms.Timer { Interval = 3200 };
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer.Stop();
            _pnlToastBanner.Visible = false;
        };

        // =====================================================================
        // BỐ CỤC CHÍNH 3 TẦNG GỌN GÀNG (ĐÃ LƯỢC BỎ 4 THẺ KPI DƯ THỪA)
        // =====================================================================
        TableLayoutPanel rootLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));  // Tầng 0: Thanh công cụ & Bộ lọc kỳ
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 238)); // Tầng 1: Bảng Định mức & Dự báo đặt hàng (4 dòng)
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Tầng 2: Bảng Tổng hợp Tháng / Chi tiết từng cây trục
        Controls.Add(rootLayout);
        Controls.Add(_pnlToastBanner);

        // =====================================================================
        // TẦNG 0: THANH ĐIỀU KHIỂN GỌN GÀNG (TOP FILTER BAR)
        // =====================================================================
        CardPanel topCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(0, 0, 0, 6)
        };
        rootLayout.Controls.Add(topCard, 0, 0);

        TableLayoutPanel topBarGrid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        topBarGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        topBarGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        topCard.Controls.Add(topBarGrid);

        FlowLayoutPanel pnlLeftControls = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        topBarGrid.Controls.Add(pnlLeftControls, 0, 0);

        Label lblMode = new() { Text = "Chế độ:", AutoSize = true, Margin = new Padding(0, 7, 4, 0), Font = IndustrialTheme.BodyBoldFont };
        _cboViewMode = new ComboBox { Width = 210, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 3, 10, 0) };
        IndustrialTheme.StyleComboBox(_cboViewMode);
        _cboViewMode.Items.Add("📅 Theo Tháng Trong Năm");
        _cboViewMode.Items.Add("🔍 Theo Khoảng Ngày Tùy Chọn");
        _cboViewMode.SelectedIndex = 0;
        _cboViewMode.SelectedIndexChanged += (s, e) => SwitchViewMode(_cboViewMode.SelectedIndex);

        pnlLeftControls.Controls.Add(lblMode);
        pnlLeftControls.Controls.Add(_cboViewMode);

        // --- Cụm điều khiển Chế độ 1: Theo Tháng / Năm ---
        _pnlModeYear = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };

        Label lblYear = new() { Text = "Năm:", AutoSize = true, Margin = new Padding(0, 7, 4, 0), Font = IndustrialTheme.BodyBoldFont };
        _cboYear = new ComboBox { Width = 76, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 3, 8, 0) };
        IndustrialTheme.StyleComboBox(_cboYear);
        int currentYear = DateTime.Today.Year;
        for (int y = currentYear - 3; y <= currentYear + 2; y++)
        {
            _cboYear.Items.Add(y);
        }
        _cboYear.SelectedItem = currentYear;
        _cboYear.SelectedIndexChanged += (s, e) => LoadMonthlyMatrix();

        Label lblFocusMonth = new() { Text = "Kỳ:", AutoSize = true, Margin = new Padding(0, 7, 4, 0), Font = IndustrialTheme.BodyBoldFont };
        _cboFocusMonth = new ComboBox { Width = 135, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 3, 10, 0) };
        IndustrialTheme.StyleComboBox(_cboFocusMonth);
        for (int m = 1; m <= 12; m++)
        {
            _cboFocusMonth.Items.Add($"Tháng {m:D2}");
        }
        _cboFocusMonth.Items.Add("Cả Năm (Tổng hợp)");
        _cboFocusMonth.SelectedIndex = DateTime.Today.Month - 1;
        _cboFocusMonth.SelectedIndexChanged += OnFocusMonthComboChanged;

        Label lblQuickTons = new() { Text = "Sản lượng (Tấn):", AutoSize = true, Margin = new Padding(0, 7, 4, 0), Font = IndustrialTheme.BodyBoldFont };
        _nudQuickMonthTons = new NumericUpDown
        {
            Width = 100,
            Minimum = 0,
            Maximum = 10_000_000,
            Increment = 500,
            ThousandsSeparator = true,
            Font = IndustrialTheme.BodyBoldFont,
            BackColor = Color.FromArgb(254, 249, 195),
            Margin = new Padding(0, 4, 6, 0)
        };

        _btnQuickSaveMonthTons = new Button { Width = 140, Height = 40, Margin = new Padding(0, 3, 1, 0) };
        IndustrialTheme.StylePrimaryButton(_btnQuickSaveMonthTons, "💾 Lưu Sản Lượng");
        _btnQuickSaveMonthTons.Click += OnQuickSaveMonthTonsClick;

        _pnlModeYear.Controls.Add(lblYear);
        _pnlModeYear.Controls.Add(_cboYear);
        _pnlModeYear.Controls.Add(lblFocusMonth);
        _pnlModeYear.Controls.Add(_cboFocusMonth);
        _pnlModeYear.Controls.Add(lblQuickTons);
        _pnlModeYear.Controls.Add(_nudQuickMonthTons);
        _pnlModeYear.Controls.Add(_btnQuickSaveMonthTons);
        pnlLeftControls.Controls.Add(_pnlModeYear);

        // --- Cụm điều khiển Chế độ 2: Khoảng ngày tùy chỉnh (Chỉ giữ Từ - Đến & Sản lượng Tấn) ---
        _pnlModeCustomRange = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            BackColor = Color.Transparent,
            Visible = false
        };

        Label lblFrom = new() { Text = "Từ ngày:", AutoSize = true, Margin = new Padding(0, 7, 4, 0), Font = IndustrialTheme.BodyBoldFont };
        _dtpFromDate = new DateTimePicker { Width = 106, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Font = IndustrialTheme.BodyFont, Margin = new Padding(0, 3, 8, 0) };
        Label lblTo = new() { Text = "Đến ngày:", AutoSize = true, Margin = new Padding(0, 7, 4, 0), Font = IndustrialTheme.BodyBoldFont };
        _dtpToDate = new DateTimePicker { Width = 106, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Font = IndustrialTheme.BodyFont, Margin = new Padding(0, 3, 10, 0) };

        Label lblCustomTons = new() { Text = "Sản lượng (Tấn):", AutoSize = true, Margin = new Padding(0, 7, 4, 0), Font = IndustrialTheme.BodyBoldFont };
        _nudProductionTons = new NumericUpDown
        {
            Width = 100,
            Minimum = 1,
            Maximum = 10_000_000,
            Value = 16200,
            Increment = 500,
            ThousandsSeparator = true,
            Font = IndustrialTheme.BodyBoldFont,
            Margin = new Padding(0, 4, 8, 0)
        };

        _btnCalculate = new Button { Width = 116, Height = 30, Margin = new Padding(0, 3, 0, 0) };
        IndustrialTheme.StylePrimaryButton(_btnCalculate, "⚡ Tính Báo Cáo");
        _btnCalculate.Click += (s, e) => CalculateCustomRangeReport();

        _pnlModeCustomRange.Controls.Add(lblFrom);
        _pnlModeCustomRange.Controls.Add(_dtpFromDate);
        _pnlModeCustomRange.Controls.Add(lblTo);
        _pnlModeCustomRange.Controls.Add(_dtpToDate);
        _pnlModeCustomRange.Controls.Add(lblCustomTons);
        _pnlModeCustomRange.Controls.Add(_nudProductionTons);
        _pnlModeCustomRange.Controls.Add(_btnCalculate);
        pnlLeftControls.Controls.Add(_pnlModeCustomRange);

        // --- Cụm nút bên phải: Định mức vật tư, Xuất Excel, Xuất PDF (hiển thị đầy đủ chữ) ---
        FlowLayoutPanel pnlRightButtons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        topBarGrid.Controls.Add(pnlRightButtons, 1, 0);

        _btnExportPdf = new Button { Width = 118, Height = 30, Margin = new Padding(6, 3, 0, 0) };
        IndustrialTheme.StylePrimaryButton(_btnExportPdf, "📄 Xuất PDF", IndustrialTheme.HoaSenBrandRed);
        _btnExportPdf.Click += OnExportPdfClick;

        _btnExportExcel = new Button { Width = 128, Height = 30, Margin = new Padding(6, 3, 0, 0) };
        IndustrialTheme.StylePrimaryButton(_btnExportExcel, "📊 Xuất Excel", IndustrialTheme.SuccessAccent);
        _btnExportExcel.Click += OnExportExcelClick;

        _btnConfigProcurement = new Button { Width = 152, Height = 30, Margin = new Padding(0, 3, 0, 0) };
        IndustrialTheme.StyleOutlineButton(_btnConfigProcurement, "⚙️ Định Mức Vật Tư", IndustrialTheme.SecondarySlate);
        _btnConfigProcurement.Click += OnConfigProcurementClick;

        pnlRightButtons.Controls.Add(_btnExportPdf);
        pnlRightButtons.Controls.Add(_btnExportExcel);
        pnlRightButtons.Controls.Add(_btnConfigProcurement);

        // =====================================================================
        // TẦNG 1: BẢNG ĐỊNH MỨC TIÊU HAO & DỰ BÁO ĐẶT HÀNG (WR - IMR - BUR)
        // =====================================================================
        CardPanel summaryCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 8),
            Margin = new Padding(0, 0, 0, 6)
        };
        rootLayout.Controls.Add(summaryCard, 0, 1);

        TableLayoutPanel summaryLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        summaryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        summaryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        summaryCard.Controls.Add(summaryLayout);

        _lblTypeSummaryTitle = new Label
        {
            Text = "I. BẢNG ĐỊNH MỨC TIÊU HAO & DỰ BÁO ĐẶT HÀNG (WR - IMR - BUR)",
            Font = IndustrialTheme.BodyBoldFont,
            ForeColor = IndustrialTheme.PrimaryAccent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        summaryLayout.Controls.Add(_lblTypeSummaryTitle, 0, 0);

        _dgvTypeSummary = new DataGridView { Dock = DockStyle.Fill };
        ConfigureTypeSummaryColumns();
        IndustrialTheme.StyleDataGridView(_dgvTypeSummary);
        _dgvTypeSummary.RowTemplate.Height = 36;
        _dgvTypeSummary.ColumnHeadersHeight = 42;
        _dgvTypeSummary.RowPrePaint += OnSummaryRowPrePaint;
        summaryLayout.Controls.Add(_dgvTypeSummary, 0, 1);

        // =====================================================================
        // TẦNG 2: KHÔNG GIAN CHI TIẾT (TỔNG HỢP THEO THÁNG <-> CHI TIẾT TỪNG TRỤC)
        // =====================================================================
        CardPanel bottomCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 8),
            Margin = new Padding(0)
        };
        rootLayout.Controls.Add(bottomCard, 0, 2);

        TableLayoutPanel bottomLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bottomCard.Controls.Add(bottomLayout);

        // Thanh chuyển đổi Sub-Tab ở Tầng 2
        FlowLayoutPanel bottomSwitchBar = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        bottomLayout.Controls.Add(bottomSwitchBar, 0, 0);

        _btnShowMatrixView = new Button { Width = 235, Height = 30, Margin = new Padding(0, 2, 6, 0) };
        _btnShowMatrixView.Click += (s, e) => SwitchBottomSubTab(0);

        _btnShowRollDetailView = new Button { Width = 250, Height = 30, Margin = new Padding(0, 2, 12, 0) };
        _btnShowRollDetailView.Click += (s, e) => SwitchBottomSubTab(1);

        _chkHideEmptyMonths = new CheckBox
        {
            Text = "Ẩn các tháng chưa có dữ liệu",
            Checked = true,
            AutoSize = true,
            Font = IndustrialTheme.BodyBoldFont,
            ForeColor = IndustrialTheme.SecondarySlate,
            Margin = new Padding(4, 6, 12, 0)
        };
        _chkHideEmptyMonths.CheckedChanged += (s, e) => BindMonthlyMatrixGrid();

        _lblFilterRollType = new Label { Text = "Lọc loại trục:", AutoSize = true, Margin = new Padding(0, 7, 4, 0), Visible = false };
        _cboDetailFilterType = new ComboBox { Width = 95, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 3, 10, 0), Visible = false };
        IndustrialTheme.StyleComboBox(_cboDetailFilterType);
        _cboDetailFilterType.Items.AddRange(new object[] { "Tất cả", "WR", "IMR", "BUR" });
        _cboDetailFilterType.SelectedIndex = 0;
        _cboDetailFilterType.SelectedIndexChanged += (s, e) => ApplyDetailFilter();

        _lblInlineEditHint = new Label
        {
            Text = "💡 Nhấp dòng tháng để xem định mức ở bảng trên • Nhấp đúp dòng tháng để xem chi tiết trục",
            AutoSize = true,
            Margin = new Padding(4, 8, 0, 0),
            Font = IndustrialTheme.SmallCaptionFont,
            ForeColor = IndustrialTheme.SecondarySlate
        };

        bottomSwitchBar.Controls.Add(_btnShowMatrixView);
        bottomSwitchBar.Controls.Add(_btnShowRollDetailView);
        bottomSwitchBar.Controls.Add(_chkHideEmptyMonths);
        bottomSwitchBar.Controls.Add(_lblFilterRollType);
        bottomSwitchBar.Controls.Add(_cboDetailFilterType);
        bottomSwitchBar.Controls.Add(_lblInlineEditHint);

        // Panel chứa 2 bảng
        Panel gridContainer = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        bottomLayout.Controls.Add(gridContainer, 0, 1);

        // 1. Bảng Tổng hợp theo Tháng
        _dgvMonthlyMatrix = new DataGridView { Dock = DockStyle.Fill };
        ConfigureMonthlyMatrixColumns();
        IndustrialTheme.StyleDataGridView(_dgvMonthlyMatrix);
        _dgvMonthlyMatrix.RowTemplate.Height = 36;
        _dgvMonthlyMatrix.ColumnHeadersHeight = 42;
        ApplyMatrixEditPermissions();
        _dgvMonthlyMatrix.CellEndEdit += OnMonthlyMatrixCellEndEdit;
        _dgvMonthlyMatrix.CellFormatting += OnMonthlyMatrixCellFormatting;
        _dgvMonthlyMatrix.SelectionChanged += OnMonthlyMatrixSelectionChanged;
        _dgvMonthlyMatrix.CellDoubleClick += OnMonthlyMatrixCellDoubleClick;
        gridContainer.Controls.Add(_dgvMonthlyMatrix);

        // 2. Bảng Chi tiết từng cây trục trong kỳ
        _dgvRollWearDetail = new DataGridView { Dock = DockStyle.Fill, Visible = false };
        ConfigureDetailGridColumns();
        IndustrialTheme.StyleDataGridView(_dgvRollWearDetail);
        _dgvRollWearDetail.RowTemplate.Height = 36;
        _dgvRollWearDetail.ColumnHeadersHeight = 42;
        _dgvRollWearDetail.CellContentClick += OnDetailCellContentClick;
        _dgvRollWearDetail.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && _dgvRollWearDetail.Rows[e.RowIndex].DataBoundItem is RollPeriodWearDto item)
            {
                ShowRollWearChart(item.RollCode);
            }
        };
        gridContainer.Controls.Add(_dgvRollWearDetail);

        // Khởi tạo ngày mặc định cho chế độ tùy chọn
        DateTime today = DateTime.Today;
        _dtpFromDate.Value = new DateTime(today.Year, today.Month, 1);
        _dtpToDate.Value = today;
        SyncReasonableProductionTonsForRange(_dtpFromDate.Value, _dtpToDate.Value);

        SwitchBottomSubTab(0);
        AppSession.RoleChanged += (s, e) => ApplyRolePermissions();
        ApplyRolePermissions();
    }

    private void ShowToastBanner(string message)
    {
        _lblToastMessage.Text = message;
        _pnlToastBanner.Visible = true;
        _pnlToastBanner.BringToFront();
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    // =========================================================================
    // CẤU HÌNH CÁC CỘT RÚT GỌN, RÕ RÀNG CHO 3 BẢNG
    // =========================================================================

    private void ConfigureTypeSummaryColumns()
    {
        _dgvTypeSummary.Columns.Clear();
        _dgvTypeSummary.AutoGenerateColumns = false;

        _dgvTypeSummary.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "RollType",
            HeaderText = "Chủng Loại",
            DataPropertyName = "RollType",
            Width = 95
        });
        _dgvTypeSummary.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "GrindSummaryDisplay",
            HeaderText = "Số Ca Mài",
            DataPropertyName = "GrindSummaryDisplay",
            Width = 120
        });
        _dgvTypeSummary.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "TotalWearMmInPeriod",
            HeaderText = "Tổng Hao (mm)",
            DataPropertyName = "TotalWearMmInPeriod",
            Width = 110,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "#,##0.00", Font = IndustrialTheme.BodyBoldFont }
        });
        _dgvTypeSummary.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "WearMmPerTon",
            HeaderText = "Định Mức (mm/Tấn)",
            DataPropertyName = "WearMmPerTon",
            Width = 125,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "0.0000", Font = IndustrialTheme.BodyBoldFont }
        });
        _dgvTypeSummary.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "RemainingInventoryDisplay",
            HeaderText = "Tồn Kho Còn Lại (mm • Cây)",
            DataPropertyName = "RemainingInventoryDisplay",
            Width = 165
        });
        _dgvTypeSummary.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "MonthlyBurnRateRolls",
            HeaderText = "Tốc Độ Hao (Cây/Tháng)",
            DataPropertyName = "MonthlyBurnRateRolls",
            Width = 125,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Font = IndustrialTheme.BodyBoldFont }
        });
        _dgvTypeSummary.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "OrderScheduleDisplay",
            HeaderText = "Hạn Đặt Hàng Khuyến Nghị",
            DataPropertyName = "OrderScheduleDisplay",
            Width = 160
        });
        _dgvTypeSummary.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "AlertStatus",
            HeaderText = "Trạng Thái Cảnh Báo",
            DataPropertyName = "AlertStatus",
            Width = 160
        });
    }

    private void ConfigureMonthlyMatrixColumns()
    {
        _dgvMonthlyMatrix.Columns.Clear();
        _dgvMonthlyMatrix.AutoGenerateColumns = false;

        _dgvMonthlyMatrix.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "MonthDisplay",
            HeaderText = "Kỳ Tháng",
            DataPropertyName = "MonthDisplay",
            Width = 125,
            ReadOnly = true
        });
        _dgvMonthlyMatrix.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ProductionTons",
            HeaderText = "Sản Lượng (Tấn)\n[Gõ trực tiếp ✍️]",
            DataPropertyName = "ProductionTons",
            Width = 130,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "#,##0", Font = IndustrialTheme.BodyBoldFont }
        });

        // Cụm WR
        _dgvMonthlyMatrix.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "WrTotalWearMm",
            HeaderText = "WR Hao (mm)",
            DataPropertyName = "WrTotalWearMm",
            Width = 95,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "#,##0.00" }
        });
        _dgvMonthlyMatrix.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "WrWearPerTon",
            HeaderText = "WR Định Mức\n(mm/Tấn)",
            DataPropertyName = "WrWearPerTon",
            Width = 100,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "0.0000" }
        });

        // Cụm IMR
        _dgvMonthlyMatrix.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ImrTotalWearMm",
            HeaderText = "IMR Hao (mm)",
            DataPropertyName = "ImrTotalWearMm",
            Width = 95,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "#,##0.00" }
        });
        _dgvMonthlyMatrix.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ImrWearPerTon",
            HeaderText = "IMR Định Mức\n(mm/Tấn)",
            DataPropertyName = "ImrWearPerTon",
            Width = 100,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "0.0000" }
        });

        // Cụm BUR
        _dgvMonthlyMatrix.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "BurTotalWearMm",
            HeaderText = "BUR Hao (mm)",
            DataPropertyName = "BurTotalWearMm",
            Width = 95,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "#,##0.00" }
        });
        _dgvMonthlyMatrix.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "BurWearPerTon",
            HeaderText = "BUR Định Mức\n(mm/Tấn)",
            DataPropertyName = "BurWearPerTon",
            Width = 100,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "0.0000" }
        });

        // Cụm Tổng hợp
        _dgvMonthlyMatrix.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "TotalAllGrindCount",
            HeaderText = "Tổng Ca Mài",
            DataPropertyName = "TotalAllGrindCount",
            Width = 90,
            ReadOnly = true
        });
        _dgvMonthlyMatrix.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "TotalAllRollsWearMm",
            HeaderText = "Tổng Hao (mm)",
            DataPropertyName = "TotalAllRollsWearMm",
            Width = 105,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "#,##0.00", Font = IndustrialTheme.BodyBoldFont }
        });
        _dgvMonthlyMatrix.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "TotalWearPerTon",
            HeaderText = "Định Mức Chung\n(mm/Tấn)",
            DataPropertyName = "TotalWearPerTon",
            Width = 110,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "0.0000", Font = IndustrialTheme.BodyBoldFont }
        });
    }

    private void ConfigureDetailGridColumns()
    {
        _dgvRollWearDetail.Columns.Clear();
        _dgvRollWearDetail.AutoGenerateColumns = false;

        _dgvRollWearDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "RollCode", HeaderText = "Mã Trục", DataPropertyName = "RollCode", Width = 100 });
        _dgvRollWearDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "RollType", HeaderText = "Loại", DataPropertyName = "RollType", Width = 75 });
        _dgvRollWearDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "GrindSummaryDisplay", HeaderText = "Số Ca Mài Trong Kỳ", DataPropertyName = "GrindSummaryDisplay", Width = 135 });
        _dgvRollWearDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalWearMmInPeriod", HeaderText = "Tổng Hao Trong Kỳ (mm)", DataPropertyName = "TotalWearMmInPeriod", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Font = IndustrialTheme.BodyBoldFont } });
        _dgvRollWearDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentDiameter", HeaderText = "ĐK Hiện Tại (mm)", DataPropertyName = "CurrentDiameter", Width = 115, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" } });
        _dgvRollWearDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "RemainingUsableMm", HeaderText = "Vỏ Hợp Kim Còn (mm)", DataPropertyName = "RemainingUsableMm", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" } });
        _dgvRollWearDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "RemainingLifetimePercent", HeaderText = "% Tuổi Thọ Còn Lại", DataPropertyName = "RemainingLifetimePercent", Width = 150 });
        _dgvRollWearDetail.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Trạng Thái", DataPropertyName = "Status", Width = 135 });

        DataGridViewButtonColumn btnPassportCol = new()
        {
            Name = "ActionPassport",
            HeaderText = "Lý Lịch Trục",
            Text = "📄 In Passport",
            UseColumnTextForButtonValue = true,
            Width = 115,
            FlatStyle = FlatStyle.Flat
        };
        _dgvRollWearDetail.Columns.Add(btnPassportCol);
    }

    private void ApplyMatrixEditPermissions()
    {
        bool isAdmin = AppSession.IsAdmin;
        _dgvMonthlyMatrix.ReadOnly = !isAdmin;

        foreach (DataGridViewColumn col in _dgvMonthlyMatrix.Columns)
        {
            col.ReadOnly = !(isAdmin && col.Name == "ProductionTons");
        }
    }

    // =========================================================================
    // CHUYỂN ĐỔI CHẾ ĐỘ XEM & SUB-TAB
    // =========================================================================

    private void SwitchViewMode(int modeIndex)
    {
        if (modeIndex == 0) // Mode 0: Theo Tháng / Năm
        {
            _pnlModeYear.Visible = true;
            _pnlModeCustomRange.Visible = false;
            SwitchBottomSubTab(0);
            LoadMonthlyMatrix();
        }
        else // Mode 1: Khoảng ngày tùy chọn
        {
            _pnlModeYear.Visible = false;
            _pnlModeCustomRange.Visible = true;
            SwitchBottomSubTab(1);
            CalculateCustomRangeReport();
        }
    }

    private void SwitchBottomSubTab(int subTabIndex)
    {
        _activeBottomSubTab = subTabIndex;
        if (subTabIndex == 0)
        {
            IndustrialTheme.StylePrimaryButton(_btnShowMatrixView, "📅 [1] Tổng Hợp Theo Tháng", IndustrialTheme.PrimaryAccent);
            IndustrialTheme.StyleOutlineButton(_btnShowRollDetailView, $"🔩 [2] Chi Tiết Từng Trục ({_lastRollDetails.Count} trục)", IndustrialTheme.SecondarySlate);

            _dgvMonthlyMatrix.Visible = true;
            _dgvRollWearDetail.Visible = false;
            _chkHideEmptyMonths.Visible = true;
            _lblFilterRollType.Visible = false;
            _cboDetailFilterType.Visible = false;
            _lblInlineEditHint.Visible = true;
        }
        else
        {
            IndustrialTheme.StyleOutlineButton(_btnShowMatrixView, "📅 [1] Tổng Hợp Theo Tháng", IndustrialTheme.SecondarySlate);
            IndustrialTheme.StylePrimaryButton(_btnShowRollDetailView, $"🔩 [2] Chi Tiết Từng Trục ({_lastRollDetails.Count} trục)", IndustrialTheme.PrimaryAccent);

            _dgvMonthlyMatrix.Visible = false;
            _dgvRollWearDetail.Visible = true;
            _chkHideEmptyMonths.Visible = false;
            _lblFilterRollType.Visible = true;
            _cboDetailFilterType.Visible = true;
            _lblInlineEditHint.Visible = false;
        }
    }

    public void RefreshData()
    {
        if (_cboViewMode.SelectedIndex == 0)
        {
            LoadMonthlyMatrix(_selectedMonthDto?.MonthIndex);
        }
        else
        {
            CalculateCustomRangeReport();
        }
    }

    // =========================================================================
    // NGHIỆP VỤ CHẾ ĐỘ 1: TỔNG HỢP THEO THÁNG & DRILL DOWN
    // =========================================================================

    private void LoadMonthlyMatrix(int? keepSelectedMonth = null)
    {
        if (_cboYear.SelectedItem is not int year)
        {
            year = DateTime.Today.Year;
        }

        try
        {
            _lastYearlyMatrix = _rollService.GetYearlyMonthlyMatrix(year).ToList();
            BindMonthlyMatrixGrid(keepSelectedMonth);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi nạp bảng tổng hợp năm {year}: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BindMonthlyMatrixGrid(int? keepSelectedMonth = null)
    {
        int year = _cboYear.SelectedItem is int y ? y : DateTime.Today.Year;
        int focusedMonthFromCombo = _cboFocusMonth.SelectedIndex < 12 ? (_cboFocusMonth.SelectedIndex + 1) : 0;
        int targetMonth = keepSelectedMonth ?? focusedMonthFromCombo;

        try
        {
            _suppressMatrixSelectionChange = true;

            List<MonthlyConsumptionMatrixDto> filteredMatrix;
            if (_chkHideEmptyMonths.Checked)
            {
                filteredMatrix = _lastYearlyMatrix
                    .Where(r =>
                        r.IsSummaryRow ||
                        r.TotalAllGrindCount > 0 ||
                        r.TotalAllRollsWearMm > 0m ||
                        r.ProductionTons > 0m ||
                        r.MonthIndex == targetMonth ||
                        (year == DateTime.Today.Year && r.MonthIndex == DateTime.Today.Month))
                    .ToList();
            }
            else
            {
                filteredMatrix = _lastYearlyMatrix.ToList();
            }

            _dgvMonthlyMatrix.DataSource = filteredMatrix;
            ApplyMatrixEditPermissions();

            int selectedRowIndex = -1;
            for (int i = 0; i < _dgvMonthlyMatrix.Rows.Count; i++)
            {
                if (_dgvMonthlyMatrix.Rows[i].DataBoundItem is MonthlyConsumptionMatrixDto rowDto &&
                    rowDto.MonthIndex == targetMonth)
                {
                    selectedRowIndex = i;
                    break;
                }
            }

            if (selectedRowIndex >= 0 && _dgvMonthlyMatrix.Rows.Count > selectedRowIndex)
            {
                _dgvMonthlyMatrix.ClearSelection();
                _dgvMonthlyMatrix.Rows[selectedRowIndex].Selected = true;
                _dgvMonthlyMatrix.CurrentCell = _dgvMonthlyMatrix.Rows[selectedRowIndex].Cells[0];
            }
        }
        finally
        {
            _suppressMatrixSelectionChange = false;
        }

        if (_dgvMonthlyMatrix.CurrentRow?.DataBoundItem is MonthlyConsumptionMatrixDto cur)
        {
            DrillDownMonthDetail(cur);
        }
        else if (_lastYearlyMatrix.Count > 0)
        {
            DrillDownMonthDetail(_lastYearlyMatrix[0]);
        }
    }

    private void OnFocusMonthComboChanged(object? sender, EventArgs e)
    {
        if (_suppressMonthSelectorChange) return;

        int targetMonthIndex = _cboFocusMonth.SelectedIndex < 12 ? (_cboFocusMonth.SelectedIndex + 1) : 0;
        BindMonthlyMatrixGrid(targetMonthIndex);
    }

    private void OnQuickSaveMonthTonsClick(object? sender, EventArgs e)
    {
        if (!AppSession.IsAdmin)
        {
            MessageBox.Show("Vui lòng bấm F9 mở khóa quyền Quản lý (Admin) để cập nhật sản lượng tháng.", "Yêu cầu quyền Admin", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_selectedMonthDto is null || _selectedMonthDto.IsSummaryRow)
        {
            MessageBox.Show("Vui lòng chọn một tháng cụ thể (Tháng 01 .. Tháng 12) để cập nhật sản lượng.", "Chọn tháng", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        decimal newTons = _nudQuickMonthTons.Value;
        SaveMonthTonnageAndRefresh(_selectedMonthDto.YearMonth, _selectedMonthDto.MonthDisplay, _selectedMonthDto.MonthIndex, newTons);
    }

    private void SaveMonthTonnageAndRefresh(string yearMonth, string monthDisplay, int monthIndex, decimal newTons)
    {
        try
        {
            _rollService.SaveMonthlyProduction(yearMonth, newTons, null);
            ShowToastBanner($"✅ Đã lưu sản lượng {monthDisplay}: {newTons:N0} Tấn ➔ Đã tính lại định mức mm/Tấn & dự báo vật tư!");
            LoadMonthlyMatrix(keepSelectedMonth: monthIndex);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi lưu sản lượng: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            LoadMonthlyMatrix(keepSelectedMonth: monthIndex);
        }
    }

    private void OnMonthlyMatrixSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressMatrixSelectionChange) return;
        if (_dgvMonthlyMatrix.CurrentRow is null || _dgvMonthlyMatrix.CurrentRow.Index < 0) return;

        if (_dgvMonthlyMatrix.CurrentRow.DataBoundItem is MonthlyConsumptionMatrixDto rowDto)
        {
            DrillDownMonthDetail(rowDto);
        }
    }

    private void OnMonthlyMatrixCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        if (_dgvMonthlyMatrix.Columns[e.ColumnIndex].Name == "ProductionTons" && AppSession.IsAdmin)
        {
            _dgvMonthlyMatrix.BeginEdit(true);
            return;
        }

        SwitchBottomSubTab(1);
    }

    private void DrillDownMonthDetail(MonthlyConsumptionMatrixDto rowDto)
    {
        _selectedMonthDto = rowDto;
        int year = _cboYear.SelectedItem is int y ? y : DateTime.Today.Year;

        _suppressMonthSelectorChange = true;
        try
        {
            _cboFocusMonth.SelectedIndex = rowDto.IsSummaryRow ? 12 : Math.Clamp(rowDto.MonthIndex - 1, 0, 11);
            _nudQuickMonthTons.Enabled = AppSession.IsAdmin && !rowDto.IsSummaryRow;
            _btnQuickSaveMonthTons.Enabled = AppSession.IsAdmin && !rowDto.IsSummaryRow;
            _nudQuickMonthTons.Value = Math.Clamp(rowDto.ProductionTons, _nudQuickMonthTons.Minimum, _nudQuickMonthTons.Maximum);
        }
        finally
        {
            _suppressMonthSelectorChange = false;
        }

        DateTime fromDate;
        DateTime toDate;
        decimal effectiveTons = rowDto.ProductionTons > 0m ? rowDto.ProductionTons : 1m;

        if (rowDto.IsSummaryRow)
        {
            fromDate = new DateTime(year, 1, 1);
            toDate = new DateTime(year, 12, 31);
            _currentActivePeriodLabel = $"CẢ NĂM {year}";
        }
        else
        {
            int month = rowDto.MonthIndex;
            fromDate = new DateTime(year, month, 1);
            toDate = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            _currentActivePeriodLabel = rowDto.MonthDisplay.ToUpperInvariant();
        }

        _currentActiveFromDate = fromDate;
        _currentActiveToDate = toDate;
        _currentActiveProductionTons = rowDto.ProductionTons;

        try
        {
            _lastTypeSummaries = _rollService.GetConsumptionAndForecast(fromDate, toDate, effectiveTons).ToList();
            if (rowDto.ProductionTons <= 0m)
            {
                foreach (var s in _lastTypeSummaries)
                {
                    s.ProductionTons = 0m;
                    s.WearMmPerTon = 0m;
                    s.WearMmPer1000Tons = 0m;
                }
            }

            _dgvTypeSummary.DataSource = BuildSummaryWithTotalRow(_lastTypeSummaries, rowDto.ProductionTons);
            _dgvTypeSummary.ClearSelection();

            _lastRollDetails = _rollService.GetPeriodWearDetails(fromDate, toDate).ToList();
            ApplyDetailFilter();

            string tonsBadge = rowDto.ProductionTons > 0m
                ? $"Sản lượng: {rowDto.ProductionTons:N0} Tấn"
                : "Chưa nhập sản lượng";
            _lblTypeSummaryTitle.Text = $"I. ĐỊNH MỨC TIÊU HAO & DỰ BÁO ĐẶT HÀNG [{_currentActivePeriodLabel} — {tonsBadge}]";

            SwitchBottomSubTab(_activeBottomSubTab);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Drill-down error: {ex.Message}");
        }
    }

    private void OnMonthlyMatrixCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _dgvMonthlyMatrix.Rows.Count || e.CellStyle is null) return;

        if (_dgvMonthlyMatrix.Rows[e.RowIndex].DataBoundItem is MonthlyConsumptionMatrixDto rowDto)
        {
            string colName = _dgvMonthlyMatrix.Columns[e.ColumnIndex].Name;

            if (rowDto.IsSummaryRow)
            {
                e.CellStyle.BackColor = Color.FromArgb(226, 232, 240);
                e.CellStyle.Font = IndustrialTheme.BodyBoldFont;
                return;
            }

            if (colName.StartsWith("Wr", StringComparison.Ordinal))
            {
                e.CellStyle.BackColor = Color.FromArgb(240, 249, 255);
            }
            else if (colName.StartsWith("Imr", StringComparison.Ordinal))
            {
                e.CellStyle.BackColor = Color.FromArgb(240, 253, 250);
            }
            else if (colName.StartsWith("Bur", StringComparison.Ordinal))
            {
                e.CellStyle.BackColor = Color.FromArgb(255, 251, 235);
            }

            if (colName == "ProductionTons")
            {
                if (AppSession.IsAdmin)
                {
                    e.CellStyle.BackColor = Color.FromArgb(254, 249, 195);
                    e.CellStyle.SelectionBackColor = Color.FromArgb(253, 224, 71);
                    e.CellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);
                }

                if (rowDto.ProductionTons <= 0m)
                {
                    e.Value = AppSession.IsAdmin ? "0 (Nhập tấn)" : "—";
                    e.CellStyle.ForeColor = IndustrialTheme.MutedText;
                    e.FormattingApplied = true;
                }
                return;
            }

            if (e.Value is decimal decVal && decVal == 0m)
            {
                e.Value = "—";
                e.CellStyle.ForeColor = Color.FromArgb(148, 163, 184);
                e.FormattingApplied = true;
            }
            else if (e.Value is int intVal && intVal == 0)
            {
                e.Value = "—";
                e.CellStyle.ForeColor = Color.FromArgb(148, 163, 184);
                e.FormattingApplied = true;
            }
        }
    }

    private void OnMonthlyMatrixCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (_dgvMonthlyMatrix.Columns[e.ColumnIndex].Name != "ProductionTons") return;

        if (_dgvMonthlyMatrix.Rows[e.RowIndex].DataBoundItem is MonthlyConsumptionMatrixDto rowDto)
        {
            if (rowDto.IsSummaryRow) return;

            object? val = _dgvMonthlyMatrix.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            string rawText = val?.ToString()?.Replace("Tấn", "", StringComparison.OrdinalIgnoreCase).Trim() ?? "0";

            if (!decimal.TryParse(rawText, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal newTons) &&
                !decimal.TryParse(rawText, NumberStyles.Any, CultureInfo.InvariantCulture, out newTons) || newTons < 0m)
            {
                MessageBox.Show("Sản lượng cán thép phải là số lớn hơn hoặc bằng 0.", "Sai định dạng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LoadMonthlyMatrix(rowDto.MonthIndex);
                return;
            }

            SaveMonthTonnageAndRefresh(rowDto.YearMonth, rowDto.MonthDisplay, rowDto.MonthIndex, newTons);
        }
    }

    // =========================================================================
    // NGHIỆP VỤ CHẾ ĐỘ 2: TRA CỨU TÙY CHỈNH TỪ NGÀY A -> NGÀY B
    // =========================================================================

    private void SyncReasonableProductionTonsForRange(DateTime from, DateTime to)
    {
        try
        {
            IReadOnlyList<ProductionPeriodEntity> periods = _rollService.GetProductionPeriods();
            ProductionPeriodEntity? matched = periods.FirstOrDefault(p => p.FromDate.Date == from.Date && p.ToDate.Date == to.Date);
            if (matched is not null)
            {
                _nudProductionTons.Value = Math.Clamp(matched.ProductionTons, _nudProductionTons.Minimum, _nudProductionTons.Maximum);
                return;
            }
        }
        catch
        {
            // Bỏ qua nếu chưa có dữ liệu
        }

        int days = Math.Max(1, (to.Date - from.Date).Days + 1);
        decimal estimatedTons = Math.Round((days * 550m) / 100m, 0) * 100m;
        _nudProductionTons.Value = Math.Clamp(Math.Max(500m, estimatedTons), _nudProductionTons.Minimum, _nudProductionTons.Maximum);
    }

    private void CalculateCustomRangeReport()
    {
        DateTime fromDate = _dtpFromDate.Value.Date;
        DateTime toDate = _dtpToDate.Value.Date;
        decimal productionTons = _nudProductionTons.Value;

        if (fromDate > toDate)
        {
            MessageBox.Show("Từ ngày không được lớn hơn Đến ngày.", "Sai khoảng thời gian", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _currentActiveFromDate = fromDate;
            _currentActiveToDate = toDate;
            _currentActiveProductionTons = productionTons;
            _currentActivePeriodLabel = $"{fromDate:dd/MM/yyyy} ➔ {toDate:dd/MM/yyyy}";

            _lastTypeSummaries = _rollService.GetConsumptionAndForecast(fromDate, toDate, productionTons).ToList();
            _dgvTypeSummary.DataSource = BuildSummaryWithTotalRow(_lastTypeSummaries, productionTons);
            _dgvTypeSummary.ClearSelection();

            _lastRollDetails = _rollService.GetPeriodWearDetails(fromDate, toDate).ToList();
            ApplyDetailFilter();

            _lblTypeSummaryTitle.Text = $"I. ĐỊNH MỨC TIÊU HAO & DỰ BÁO ĐẶT HÀNG [KỲ: {_currentActivePeriodLabel} — Sản lượng: {productionTons:N0} Tấn]";
            SwitchBottomSubTab(_activeBottomSubTab);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tính toán báo cáo: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static List<TypeConsumptionAndForecastDto> BuildSummaryWithTotalRow(
        IReadOnlyList<TypeConsumptionAndForecastDto> summaries,
        decimal productionTons)
    {
        var list = summaries.ToList();
        if (list.Count == 0)
        {
            return list;
        }

        decimal totalNormal = list.Sum(s => s.NormalWearMm);
        decimal totalAbnormal = list.Sum(s => s.AbnormalWearMm);
        decimal totalWear = totalNormal + totalAbnormal;
        decimal wearPerTon = productionTons > 0m ? totalWear / productionTons : 0m;

        string worstAlert = ProcurementAlertStatuses.Safe;
        if (list.Any(s => s.AlertStatus == ProcurementAlertStatuses.Critical))
        {
            worstAlert = ProcurementAlertStatuses.Critical;
        }
        else if (list.Any(s => s.AlertStatus == ProcurementAlertStatuses.Warning))
        {
            worstAlert = ProcurementAlertStatuses.Warning;
        }

        TypeConsumptionAndForecastDto? urgentItem = list
            .Where(s => s.DaysUntilOrderDeadline.HasValue)
            .OrderBy(s => s.DaysUntilOrderDeadline!.Value)
            .FirstOrDefault();

        list.Add(new TypeConsumptionAndForecastDto
        {
            RollType = "TỔNG CỘNG",
            GrindCountInPeriod = list.Sum(s => s.GrindCountInPeriod),
            AbnormalGrindCountInPeriod = list.Sum(s => s.AbnormalGrindCountInPeriod),
            NormalWearMm = totalNormal,
            AbnormalWearMm = totalAbnormal,
            TotalWearMmInPeriod = totalWear,
            WearMmPerTon = RollCalculator.RoundRatio(wearPerTon),
            WearMmPer1000Tons = RollCalculator.RoundRatio(wearPerTon * 1000m),
            EquivalentRollsConsumed = RollCalculator.RoundRatio(list.Sum(s => s.EquivalentRollsConsumed)),
            ActiveRollCount = list.Sum(s => s.ActiveRollCount),
            LowLifeRollCount = list.Sum(s => s.LowLifeRollCount),
            TotalRemainingMmInShop = list.Sum(s => s.TotalRemainingMmInShop),
            EquivalentRollsRemaining = RollCalculator.RoundRatio(list.Sum(s => s.EquivalentRollsRemaining)),
            DailyBurnRateRolls = RollCalculator.RoundRatio(list.Sum(s => s.DailyBurnRateRolls)),
            MonthlyBurnRateMm = RollCalculator.RoundRatio(list.Sum(s => s.MonthlyBurnRateMm)),
            MonthlyBurnRateRolls = RollCalculator.RoundRatio(list.Sum(s => s.MonthlyBurnRateRolls)),
            RunwayDays = urgentItem?.RunwayDays,
            LeadTimeDays = urgentItem?.LeadTimeDays ?? list.Max(s => s.LeadTimeDays),
            DaysUntilOrderDeadline = urgentItem?.DaysUntilOrderDeadline,
            RecommendedOrderDate = urgentItem?.RecommendedOrderDate,
            AlertStatus = worstAlert
        });

        return list;
    }

    private void ApplyDetailFilter()
    {
        string filter = _cboDetailFilterType.SelectedItem as string ?? "Tất cả";
        IEnumerable<RollPeriodWearDto> query = _lastRollDetails;

        if (filter != "Tất cả")
        {
            query = query.Where(r => r.RollType == filter);
        }

        _dgvRollWearDetail.DataSource = query.ToList();
        _dgvRollWearDetail.ClearSelection();
    }

    private void OnSummaryRowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _dgvTypeSummary.Rows.Count) return;

        if (_dgvTypeSummary.Rows[e.RowIndex].DataBoundItem is TypeConsumptionAndForecastDto summary)
        {
            if (summary.RollType == "TỔNG CỘNG")
            {
                _dgvTypeSummary.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(226, 232, 240);
                _dgvTypeSummary.Rows[e.RowIndex].DefaultCellStyle.Font = IndustrialTheme.BodyBoldFont;
            }
            else if (summary.AlertStatus.Contains("NGUY CẤP"))
            {
                _dgvTypeSummary.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
            }
            else if (summary.AlertStatus.Contains("CẢNH BÁO"))
            {
                _dgvTypeSummary.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(254, 249, 195);
            }
        }
    }

    private void ShowRollWearChart(string rollCode)
    {
        try
        {
            RollLifetimeViewDto lifetime = _rollService.GetRollLifetime(rollCode);
            IReadOnlyList<GrindingLogViewDto> history = _rollService.GetGrindingLogViewsByRoll(rollCode);
            using RollWearChartDialog dialog = new(lifetime, history);
            dialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi mở biểu đồ mòn trục: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnDetailCellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        if (_dgvRollWearDetail.Columns[e.ColumnIndex].Name == "ActionPassport")
        {
            if (_dgvRollWearDetail.Rows[e.RowIndex].DataBoundItem is RollPeriodWearDto item)
            {
                ExportRollPassport(item.RollCode);
            }
        }
    }

    private void ExportRollPassport(string rollCode)
    {
        try
        {
            RollLifetimeViewDto lifetime = _rollService.GetRollLifetime(rollCode);
            IReadOnlyList<GrindingLogViewDto> logs = _rollService.GetGrindingLogViewsByRoll(rollCode);

            using SaveFileDialog sfd = new()
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"LyLichTruc_{rollCode}_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                ExportService.ExportRollPassportToPdf(sfd.FileName, lifetime, logs);
                DialogResult openNow = MessageBox.Show(
                    $"Xuất Phiếu Lý Lịch Trục {rollCode} thành công!\n\nĐường dẫn: {sfd.FileName}\n\nBạn có muốn mở file ngay không?",
                    "Xuất PDF hoàn tất",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (openNow == DialogResult.Yes)
                {
                    ExportService.OpenFileWithDefaultApp(sfd.FileName);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi in lý lịch trục: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnConfigProcurementClick(object? sender, EventArgs e)
    {
        using ProcurementConfigDialog dialog = new();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            RefreshData();
            ShowToastBanner("✅ Đã cập nhật cấu hình định mức vật tư & tính toán lại dự báo đặt hàng!");
        }
    }

    private void OnExportExcelClick(object? sender, EventArgs e)
    {
        int year = _cboYear.SelectedItem is int y ? y : DateTime.Today.Year;
        DateTime fromDate = _currentActiveFromDate != default ? _currentActiveFromDate : _dtpFromDate.Value.Date;
        DateTime toDate = _currentActiveToDate != default ? _currentActiveToDate : _dtpToDate.Value.Date;
        decimal tons = _currentActiveProductionTons > 0m ? _currentActiveProductionTons : _nudProductionTons.Value;

        if (_lastTypeSummaries.Count == 0)
        {
            RefreshData();
        }

        try
        {
            using SaveFileDialog sfd = new()
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"BaoCaoTieuHaoTruc_{year}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                IReadOnlyList<RollLifetimeViewDto> allRolls = _rollService.GetRollLifetimes();
                IReadOnlyList<GrindingLogViewDto> wrLogs = _rollService.GetGrindingLogViews(RollTypes.WR, newestFirst: true);
                IReadOnlyList<GrindingLogViewDto> burLogs = _rollService.GetGrindingLogViews(RollTypes.BUR, newestFirst: true);
                IReadOnlyList<GrindingLogViewDto> imrLogs = _rollService.GetGrindingLogViews(RollTypes.IMR, newestFirst: true);
                IReadOnlyList<MonthlyConsumptionMatrixDto> matrix = _lastYearlyMatrix.Count > 0
                    ? _lastYearlyMatrix
                    : _rollService.GetYearlyMonthlyMatrix(year);

                ExportService.ExportFullWorkbookToExcel(
                    sfd.FileName,
                    fromDate,
                    toDate,
                    tons,
                    allRolls,
                    wrLogs,
                    burLogs,
                    imrLogs,
                    _lastTypeSummaries,
                    _lastRollDetails,
                    matrix);

                DialogResult openNow = MessageBox.Show(
                    $"Xuất Excel thành công!\n\nĐường dẫn: {sfd.FileName}\n\nBạn có muốn mở file ngay không?",
                    "Xuất Excel hoàn tất",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (openNow == DialogResult.Yes)
                {
                    ExportService.OpenFileWithDefaultApp(sfd.FileName);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi xuất Excel: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnExportPdfClick(object? sender, EventArgs e)
    {
        int year = _cboYear.SelectedItem is int y ? y : DateTime.Today.Year;
        DateTime fromDate = _currentActiveFromDate != default ? _currentActiveFromDate : _dtpFromDate.Value.Date;
        DateTime toDate = _currentActiveToDate != default ? _currentActiveToDate : _dtpToDate.Value.Date;
        decimal tons = _currentActiveProductionTons > 0m ? _currentActiveProductionTons : _nudProductionTons.Value;

        if (_lastTypeSummaries.Count == 0)
        {
            RefreshData();
        }

        try
        {
            using SaveFileDialog sfd = new()
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"BaoCaoTieuHaoTruc_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                IReadOnlyList<MonthlyConsumptionMatrixDto> matrix = _lastYearlyMatrix.Count > 0
                    ? _lastYearlyMatrix
                    : _rollService.GetYearlyMonthlyMatrix(year);

                ExportService.ExportPeriodReportToPdf(
                    sfd.FileName,
                    fromDate,
                    toDate,
                    tons,
                    _lastTypeSummaries,
                    _lastRollDetails,
                    matrix);

                DialogResult openNow = MessageBox.Show(
                    $"Xuất Báo Cáo PDF thành công!\n\nĐường dẫn: {sfd.FileName}\n\nBạn có muốn mở file ngay không?",
                    "Xuất PDF hoàn tất",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (openNow == DialogResult.Yes)
                {
                    ExportService.OpenFileWithDefaultApp(sfd.FileName);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi xuất PDF: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyRolePermissions()
    {
        bool isAdmin = AppSession.IsAdmin;
        bool isSpecificMonth = _selectedMonthDto is not null && !_selectedMonthDto.IsSummaryRow;

        _btnConfigProcurement.Enabled = isAdmin;
        _nudQuickMonthTons.Enabled = isAdmin && isSpecificMonth;
        _btnQuickSaveMonthTons.Enabled = isAdmin && isSpecificMonth;

        ApplyMatrixEditPermissions();
        _dgvMonthlyMatrix.Invalidate();
    }
}
