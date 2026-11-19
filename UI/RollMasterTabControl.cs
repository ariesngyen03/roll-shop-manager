using System.Globalization;
using RollShopManager.Models;
using RollShopManager.Security;
using RollShopManager.Services;

namespace RollShopManager.UI;

/// <summary>
/// UserControl quản lý danh mục và vòng đời trục (Tab QUAN_LY_TRUC).
/// </summary>
public sealed class RollMasterTabControl : UserControl
{
    private readonly RollBusinessService _rollService;

    // 4 Thẻ KPI Dashboard
    private readonly KpiCardPanel _cardTotalRolls;
    private readonly KpiCardPanel _cardInUseRolls;
    private readonly KpiCardPanel _cardLowLifeRolls;
    private readonly KpiCardPanel _cardScrapRolls;

    // Các control nhập liệu khai báo trục
    private readonly TextBox _txtRollCode;
    private readonly ComboBox _cboRollType;
    private readonly DateTimePicker _dtpCommissionDate;
    private readonly TextBox _txtMaxDia;
    private readonly TextBox _txtMinDia;
    private readonly TextBox _txtInitialDia;
    private readonly NumericUpDown _nudBatchCount;
    private readonly TextBox _txtNotes;
    private readonly Label _lblFormFeedback;
    private bool _suppressTypeDefaultFill;

    // Nút thao tác
    private readonly Button _btnCreateRoll;
    private readonly Button _btnUpdateRoll;
    private readonly Button _btnDeleteRoll;
    private readonly Button _btnClearForm;

    // Bộ lọc danh sách
    private readonly ComboBox _cboFilterType;
    private readonly ComboBox _cboFilterStatus;
    private readonly TextBox _txtSearchCode;

    // Bảng dữ liệu
    private readonly DataGridView _dgvRolls;
    private List<RollLifetimeViewDto> _cachedRolls = new();

    public RollMasterTabControl()
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
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));  // 4 Thẻ KPI
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230)); // Form khai báo / chỉnh sửa
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Bảng dữ liệu + Bộ lọc
        Controls.Add(rootLayout);

        // =====================================================================
        // PHẦN 1: 4 THẺ KPI QUÂN SỐ TRỤC (TOP)
        // =====================================================================
        TableLayoutPanel kpiLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 10)
        };
        kpiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        kpiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        kpiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        kpiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        rootLayout.Controls.Add(kpiLayout, 0, 0);

        _cardTotalRolls = new KpiCardPanel
        {
            CardTitle = "TỔNG SỐ TRỤC",
            CardValue = "0",
            CardSubText = "Toàn xưởng cán (WR, IMR, BUR)",
            AccentColor = IndustrialTheme.PrimaryAccent,
            Margin = new Padding(0, 0, 8, 0)
        };
        kpiLayout.Controls.Add(_cardTotalRolls, 0, 0);

        _cardInUseRolls = new KpiCardPanel
        {
            CardTitle = "ĐANG HOẠT ĐỘNG",
            CardValue = "0",
            CardSubText = "Trục sẵn sàng trên giá cán",
            AccentColor = IndustrialTheme.SafeBar,
            Margin = new Padding(4, 0, 4, 0)
        };
        kpiLayout.Controls.Add(_cardInUseRolls, 1, 0);

        _cardLowLifeRolls = new KpiCardPanel
        {
            CardTitle = "SẮP HẾT ĐỜI (< 15%)",
            CardValue = "0",
            CardSubText = "Cần theo dõi sát để thay thế",
            AccentColor = IndustrialTheme.WarnBar,
            Margin = new Padding(4, 0, 4, 0)
        };
        kpiLayout.Controls.Add(_cardLowLifeRolls, 2, 0);

        _cardScrapRolls = new KpiCardPanel
        {
            CardTitle = "HẾT ĐỜI (SCRAP)",
            CardValue = "0",
            CardSubText = "Đã chạm ĐK phế phẩm",
            AccentColor = IndustrialTheme.CritBar,
            Margin = new Padding(8, 0, 0, 0)
        };
        kpiLayout.Controls.Add(_cardScrapRolls, 3, 0);

        // =====================================================================
        // PHẦN 2: FORM KHAI BÁO / CHỈNH SỬA TRỤC (CARD CONTAINER)
        // =====================================================================
        CardPanel pnlFormCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0, 0, 0, 10)
        };
        rootLayout.Controls.Add(pnlFormCard, 0, 1);

        TableLayoutPanel formGrid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 5,
            BackColor = Color.Transparent
        };
        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

        formGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Tiêu đề
        formGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Mã, Loại, Ngày SD
        formGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // ĐK Max, Min, Khởi tạo
        formGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Số lần đã mài, Ghi chú
        formGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Hàng nút bấm
        pnlFormCard.Controls.Add(formGrid);

        Label lblFormHeader = new()
        {
            Text = "KHAI BÁO & CẬP NHẬT TRỤC CÁN — TỰ ĐỘNG ĐIỀN MẪU CÙNG LOẠI & BẮT ĐẦU THEO DÕI TỪ ĐK HIỆN TRẠNG",
            Font = IndustrialTheme.BodyBoldFont,
            ForeColor = IndustrialTheme.PrimaryAccent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        formGrid.SetColumnSpan(lblFormHeader, 6);
        formGrid.Controls.Add(lblFormHeader, 0, 0);

        // Hàng 1: Loại trục (chọn trước để tự điền thông số mẫu), Mã trục (tự sinh mã kế tiếp), Ngày đưa vào SD
        Label lblType = IndustrialTheme.CreateFormLabel("Loại trục:", bold: true);
        _cboRollType = new ComboBox { Dock = DockStyle.Fill };
        IndustrialTheme.StyleComboBox(_cboRollType);
        _cboRollType.Items.AddRange(RollTypes.All.ToArray());

        Label lblCode = IndustrialTheme.CreateFormLabel("Mã trục:", bold: true, leftMargin: 12);
        _txtRollCode = new TextBox { Dock = DockStyle.Fill, MaxLength = 50 };
        IndustrialTheme.StyleInputBox(_txtRollCode);
        _txtRollCode.KeyDown += OnQuickSubmitKeyDown;

        _cboRollType.SelectedIndex = 0;
        _cboRollType.SelectedIndexChanged += (s, e) =>
        {
            if (!_suppressTypeDefaultFill && !_txtRollCode.ReadOnly)
            {
                ApplySmartDefaultsForSelectedType(keepCurrentDimensionsIfSameType: false);
            }
        };

        Label lblDate = IndustrialTheme.CreateFormLabel("Ngày theo dõi:", leftMargin: 12);
        _dtpCommissionDate = new DateTimePicker { Dock = DockStyle.Fill };
        IndustrialTheme.StyleDateTimePicker(_dtpCommissionDate, "dd/MM/yyyy");

        formGrid.Controls.Add(lblType, 0, 1);
        formGrid.Controls.Add(_cboRollType, 1, 1);
        formGrid.Controls.Add(lblCode, 2, 1);
        formGrid.Controls.Add(_txtRollCode, 3, 1);
        formGrid.Controls.Add(lblDate, 4, 1);
        formGrid.Controls.Add(_dtpCommissionDate, 5, 1);

        // Hàng 2: ĐK Max gốc, ĐK Min Scrap, ĐK Hiện trạng (bắt đầu theo dõi luôn)
        Label lblInit = IndustrialTheme.CreateFormLabel("ĐK Hiện trạng (mm):", bold: true, color: IndustrialTheme.PrimaryAccent, leftMargin: 12);
        _txtInitialDia = new TextBox { Dock = DockStyle.Fill };
        IndustrialTheme.StyleInputBox(_txtInitialDia);
        _txtInitialDia.Font = IndustrialTheme.BodyBoldFont;
        _txtInitialDia.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter)
            {
                _txtInitialDia.Tag = "manual";
            }
            OnQuickSubmitKeyDown(s, e);
        };

        Label lblMax = IndustrialTheme.CreateFormLabel("ĐK Max gốc (mm):", bold: true);
        _txtMaxDia = new TextBox { Dock = DockStyle.Fill };
        IndustrialTheme.StyleInputBox(_txtMaxDia);
        _txtMaxDia.TextChanged += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(_txtInitialDia.Text) || string.Equals(_txtInitialDia.Tag as string, "auto", StringComparison.Ordinal))
            {
                _txtInitialDia.Text = _txtMaxDia.Text;
                _txtInitialDia.Tag = "auto";
            }
        };
        _txtMaxDia.KeyDown += OnQuickSubmitKeyDown;

        Label lblMin = IndustrialTheme.CreateFormLabel("ĐK Min Scrap (mm):", bold: true, leftMargin: 12);
        _txtMinDia = new TextBox { Dock = DockStyle.Fill };
        IndustrialTheme.StyleInputBox(_txtMinDia);
        _txtMinDia.KeyDown += OnQuickSubmitKeyDown;

        formGrid.Controls.Add(lblMax, 0, 2);
        formGrid.Controls.Add(_txtMaxDia, 1, 2);
        formGrid.Controls.Add(lblMin, 2, 2);
        formGrid.Controls.Add(_txtMinDia, 3, 2);
        formGrid.Controls.Add(lblInit, 4, 2);
        formGrid.Controls.Add(_txtInitialDia, 5, 2);

        // Hàng 3: Số lượng tạo cùng lúc (hỗ trợ tạo nhanh hàng loạt cây giống nhau) & Ghi chú kỹ thuật
        Label lblBatch = IndustrialTheme.CreateFormLabel("Số lượng tạo (cây):");
        _nudBatchCount = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 1,
            Maximum = 50,
            Value = 1
        };
        IndustrialTheme.StyleNumericUpDown(_nudBatchCount);

        Label lblNotes = IndustrialTheme.CreateFormLabel("Ghi chú kỹ thuật:", leftMargin: 12);
        _txtNotes = new TextBox { Dock = DockStyle.Fill, MaxLength = 2000 };
        IndustrialTheme.StyleInputBox(_txtNotes);
        _txtNotes.KeyDown += OnQuickSubmitKeyDown;

        formGrid.Controls.Add(lblBatch, 0, 3);
        formGrid.Controls.Add(_nudBatchCount, 1, 3);
        formGrid.Controls.Add(lblNotes, 2, 3);
        formGrid.SetColumnSpan(_txtNotes, 3);
        formGrid.Controls.Add(_txtNotes, 3, 3);

        // Hàng 4: Nút thao tác + Nhãn phản hồi tức thời (Không dùng MessageBox làm ngắt quãng nhập liệu)
        FlowLayoutPanel pnlButtons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 6, 0, 0)
        };
        formGrid.SetColumnSpan(pnlButtons, 6);
        formGrid.Controls.Add(pnlButtons, 0, 4);

        _btnCreateRoll = new Button { Width = 175, Height = 34, Margin = new Padding(0, 2, 10, 0) };
        IndustrialTheme.StylePrimaryButton(_btnCreateRoll, "➕ Thêm Trục (Enter)");
        _btnCreateRoll.Click += OnCreateRollClick;
        pnlButtons.Controls.Add(_btnCreateRoll);

        _btnUpdateRoll = new Button { Width = 155, Height = 34, Margin = new Padding(0, 2, 10, 0) };
        IndustrialTheme.StyleOutlineButton(_btnUpdateRoll, "✏️ Lưu Cập Nhật", IndustrialTheme.PrimaryAccent);
        _btnUpdateRoll.Click += OnUpdateRollClick;
        pnlButtons.Controls.Add(_btnUpdateRoll);

        _btnDeleteRoll = new Button { Width = 125, Height = 34, Margin = new Padding(0, 2, 10, 0) };
        IndustrialTheme.StyleOutlineButton(_btnDeleteRoll, "🗑️ Xóa Trục", IndustrialTheme.DangerAccent);
        _btnDeleteRoll.Click += OnDeleteRollClick;
        pnlButtons.Controls.Add(_btnDeleteRoll);

        _btnClearForm = new Button { Width = 115, Height = 34, Margin = new Padding(0, 2, 14, 0) };
        IndustrialTheme.StyleSecondaryButton(_btnClearForm, "🔄 Nhập Mới");
        _btnClearForm.Click += (s, e) => ResetInputForm(keepTypeAndDimensions: false);
        pnlButtons.Controls.Add(_btnClearForm);

        _lblFormFeedback = new Label
        {
            AutoSize = true,
            Font = IndustrialTheme.BodyBoldFont,
            ForeColor = IndustrialTheme.SafeText,
            Margin = new Padding(0, 9, 0, 0),
            UseMnemonic = false,
            Text = "Mẹo: Chọn Loại trục ➔ Hệ thống tự điền ĐK Max/Min chuẩn & gợi ý Mã trục kế tiếp."
        };
        pnlButtons.Controls.Add(_lblFormFeedback);

        // =====================================================================
        // PHẦN 3: BỘ LỌC & BẢNG DỮ LIỆU LIFETIME (BOTTOM)
        // =====================================================================
        CardPanel pnlGridCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0)
        };
        rootLayout.Controls.Add(pnlGridCard, 0, 2);

        TableLayoutPanel tableArea = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        tableArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Bộ lọc
        tableArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Bảng dữ liệu
        pnlGridCard.Controls.Add(tableArea);

        // Thanh công cụ lọc dữ liệu
        FlowLayoutPanel filterBar = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        tableArea.Controls.Add(filterBar, 0, 0);

        Label lblFilType = new() { Text = "Loại trục:", AutoSize = true, Margin = new Padding(0, 8, 6, 0), Font = IndustrialTheme.BodyBoldFont };
        _cboFilterType = new ComboBox { Width = 105, Margin = new Padding(0, 4, 16, 0) };
        IndustrialTheme.StyleComboBox(_cboFilterType);
        _cboFilterType.Margin = new Padding(0, 4, 16, 0);
        _cboFilterType.Items.AddRange(new object[] { "Tất cả", "WR", "IMR", "BUR" });
        _cboFilterType.SelectedIndex = 0;
        _cboFilterType.SelectedIndexChanged += (s, e) => ApplyFilter();

        Label lblFilStatus = new() { Text = "Trạng thái:", AutoSize = true, Margin = new Padding(0, 8, 6, 0), Font = IndustrialTheme.BodyBoldFont };
        _cboFilterStatus = new ComboBox { Width = 175, Margin = new Padding(0, 4, 16, 0) };
        IndustrialTheme.StyleComboBox(_cboFilterStatus);
        _cboFilterStatus.Margin = new Padding(0, 4, 16, 0);
        _cboFilterStatus.Items.AddRange(new object[] { "Tất cả", RollStatusTexts.InUse, RollStatusTexts.NeedInspection, RollStatusTexts.Scrap });
        _cboFilterStatus.SelectedIndex = 0;
        _cboFilterStatus.SelectedIndexChanged += (s, e) => ApplyFilter();

        Label lblSearch = new() { Text = "Tìm mã trục:", AutoSize = true, Margin = new Padding(0, 8, 6, 0), Font = IndustrialTheme.BodyBoldFont };
        _txtSearchCode = new TextBox { Width = 135, Margin = new Padding(0, 4, 12, 0) };
        IndustrialTheme.StyleInputBox(_txtSearchCode);
        _txtSearchCode.Margin = new Padding(0, 4, 12, 0);
        _txtSearchCode.TextChanged += (s, e) => ApplyFilter();

        filterBar.Controls.Add(lblFilType);
        filterBar.Controls.Add(_cboFilterType);
        filterBar.Controls.Add(lblFilStatus);
        filterBar.Controls.Add(_cboFilterStatus);
        filterBar.Controls.Add(lblSearch);
        filterBar.Controls.Add(_txtSearchCode);

        Button btnShowWearChart = new()
        {
            Width = 175,
            Height = 32,
            Margin = new Padding(4, 2, 8, 0)
        };
        IndustrialTheme.StyleOutlineButton(btnShowWearChart, "📈 Biểu Đồ Mòn Trục", IndustrialTheme.SecondarySlate);
        btnShowWearChart.Click += (s, e) => ShowSelectedRollWearChart();
        filterBar.Controls.Add(btnShowWearChart);

        Button btnExportPassport = new()
        {
            Width = 205,
            Height = 32,
            Margin = new Padding(0, 2, 0, 0)
        };
        IndustrialTheme.StyleOutlineButton(btnExportPassport, "📄 Xuất Lý Lịch Trục (PDF)", IndustrialTheme.PrimaryAccent);
        btnExportPassport.Click += (s, e) => ExportSelectedRollPassport();
        filterBar.Controls.Add(btnExportPassport);

        // Bảng dữ liệu
        _dgvRolls = new DataGridView { Dock = DockStyle.Fill };
        ConfigureDataGridColumns();
        IndustrialTheme.StyleDataGridView(_dgvRolls);
        _dgvRolls.SelectionChanged += OnGridSelectionChanged;
        _dgvRolls.CellClick += (s, e) =>
        {
            if (e.RowIndex >= 0)
            {
                OnGridSelectionChanged(_dgvRolls, EventArgs.Empty);
            }
        };
        _dgvRolls.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0)
            {
                ShowSelectedRollWearChart();
            }
        };
        _dgvRolls.RowPrePaint += OnGridRowPrePaint;
        tableArea.Controls.Add(_dgvRolls, 0, 1);

        AppSession.RoleChanged += (s, e) => ApplyRolePermissions();
        ApplyRolePermissions();
    }

    private bool _suppressSelectionChanged;

    private void ConfigureDataGridColumns()
    {
        _dgvRolls.Columns.Clear();
        _dgvRolls.Columns.Add(new DataGridViewTextBoxColumn { Name = "RollCode", HeaderText = "Mã Trục", DataPropertyName = "RollCode", Width = 100 });
        _dgvRolls.Columns.Add(new DataGridViewTextBoxColumn { Name = "RollType", HeaderText = "Loại", DataPropertyName = "RollType", Width = 68 });
        _dgvRolls.Columns.Add(new DataGridViewTextBoxColumn { Name = "CommissionDate", HeaderText = "Ngày Theo Dõi", DataPropertyName = "CommissionDate", Width = 105, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
        _dgvRolls.Columns.Add(new DataGridViewTextBoxColumn { Name = "MaxDiameter", HeaderText = "ĐK Max Gốc", DataPropertyName = "MaxDiameter", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" } });
        _dgvRolls.Columns.Add(new DataGridViewTextBoxColumn { Name = "MinDiameter", HeaderText = "ĐK Min Scrap", DataPropertyName = "MinDiameter", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" } });
        _dgvRolls.Columns.Add(new DataGridViewTextBoxColumn { Name = "InitialDiameter", HeaderText = "ĐK Hiện Trạng Đầu", DataPropertyName = "InitialDiameter", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" } });
        _dgvRolls.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentDiameter", HeaderText = "ĐK Hiện Tại", DataPropertyName = "CurrentDiameter", Width = 105, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Font = IndustrialTheme.BodyBoldFont } });
        _dgvRolls.Columns.Add(new DataGridViewTextBoxColumn { Name = "LoggedGrindCount", HeaderText = "Số Ca Đã Mài", DataPropertyName = "LoggedGrindCount", Width = 90 });
        _dgvRolls.Columns.Add(new DataGridViewTextBoxColumn { Name = "RemainingUsableMm", HeaderText = "Lượng Dư (mm)", DataPropertyName = "RemainingUsableMm", Width = 105, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" } });
        _dgvRolls.Columns.Add(new DataGridViewTextBoxColumn { Name = "RemainingLifetimePercent", HeaderText = "% Lifetime Còn Lại", DataPropertyName = "RemainingLifetimePercent", Width = 145 });
        _dgvRolls.Columns.Add(new DataGridViewTextBoxColumn { Name = "EstimatedRemainingGrindsText", HeaderText = "Ước Còn Mài", DataPropertyName = "EstimatedRemainingGrindsText", Width = 100 });
        _dgvRolls.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Trạng Thái", DataPropertyName = "Status", Width = 135 });
        _dgvRolls.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "Ghi Chú Kỹ Thuật", DataPropertyName = "Notes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
    }

    public void RefreshData()
    {
        _cachedRolls = _rollService.GetRollLifetimes().ToList();
        UpdateKpiSummary(_cachedRolls);
        ApplyFilter();

        if (!_txtRollCode.ReadOnly && string.IsNullOrWhiteSpace(_txtRollCode.Text))
        {
            ApplySmartDefaultsForSelectedType(keepCurrentDimensionsIfSameType: false);
        }
    }

    private void UpdateKpiSummary(List<RollLifetimeViewDto> rolls)
    {
        int total = rolls.Count;
        int inUse = rolls.Count(r => r.Status == RollStatusTexts.InUse);
        int lowLife = rolls.Count(r => !r.IsScrap && r.RemainingLifetimePercent < 15m);
        int scrap = rolls.Count(r => r.IsScrap);

        _cardTotalRolls.CardValue = total.ToString();
        _cardInUseRolls.CardValue = inUse.ToString();
        _cardLowLifeRolls.CardValue = lowLife.ToString();
        _cardScrapRolls.CardValue = scrap.ToString();
    }

    private void ApplyFilter()
    {
        string filterType = _cboFilterType.SelectedItem as string ?? "Tất cả";
        string filterStatus = _cboFilterStatus.SelectedItem as string ?? "Tất cả";
        string search = _txtSearchCode.Text.Trim().ToUpperInvariant();

        IEnumerable<RollLifetimeViewDto> query = _cachedRolls;

        if (filterType != "Tất cả")
        {
            query = query.Where(r => r.RollType == filterType);
        }

        if (filterStatus != "Tất cả")
        {
            query = query.Where(r => r.Status == filterStatus);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(r => r.RollCode.ToUpperInvariant().Contains(search));
        }

        _suppressSelectionChanged = true;
        try
        {
            _dgvRolls.DataSource = query.ToList();
            _dgvRolls.ClearSelection();
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
    }

    private void OnGridSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressSelectionChanged)
        {
            return;
        }

        if (_dgvRolls.CurrentRow?.DataBoundItem is not RollLifetimeViewDto roll)
        {
            return;
        }

        _suppressTypeDefaultFill = true;
        try
        {
            _txtRollCode.Text = roll.RollCode;
            _cboRollType.SelectedItem = roll.RollType;
            _dtpCommissionDate.Value = roll.CommissionDate;
            _txtMaxDia.Text = roll.MaxDiameter.ToString("0.00", CultureInfo.InvariantCulture);
            _txtMinDia.Text = roll.MinDiameter.ToString("0.00", CultureInfo.InvariantCulture);
            _txtInitialDia.Text = roll.InitialDiameter.ToString("0.00", CultureInfo.InvariantCulture);
            _txtInitialDia.Tag = "manual";
            _nudBatchCount.Value = 1;
            _nudBatchCount.Enabled = false;
            _txtNotes.Text = roll.Notes ?? string.Empty;

            _txtRollCode.ReadOnly = true; // Không cho sửa mã trục khi cập nhật
            SetFeedback($"Đang chọn trục {roll.RollCode} (ĐK hiện trạng đầu: {roll.InitialDiameter:0.00} mm | ĐK hiện tại: {roll.CurrentDiameter:0.00} mm).", isWarning: false);
            ApplyRolePermissions();
        }
        finally
        {
            _suppressTypeDefaultFill = false;
        }
    }

    private void OnQuickSubmitKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.SuppressKeyPress = true;
        if (_txtRollCode.ReadOnly && _btnUpdateRoll.Enabled)
        {
            _btnUpdateRoll.PerformClick();
        }
        else if (!_txtRollCode.ReadOnly && _btnCreateRoll.Enabled)
        {
            _btnCreateRoll.PerformClick();
        }
    }

    private void OnGridRowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _dgvRolls.Rows.Count) return;

        if (_dgvRolls.Rows[e.RowIndex].DataBoundItem is RollLifetimeViewDto roll)
        {
            if (roll.IsScrap)
            {
                _dgvRolls.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242); // Đỏ nhạt
            }
            else if (roll.RemainingLifetimePercent < 15m)
            {
                _dgvRolls.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(254, 249, 195); // Vàng nhạt
            }
        }
    }

    private void OnCreateRollClick(object? sender, EventArgs e)
    {
        if (!ValidateForm(out RollEntity baseRoll)) return;

        int batchCount = (int)_nudBatchCount.Value;

        try
        {
            if (batchCount <= 1)
            {
                _rollService.CreateRoll(baseRoll);
                RefreshData();
                // Giữ nguyên Loại trục & ĐK Max/Min vừa nhập, tự nhảy sang Mã trục kế tiếp để nhập liên tục không cần gõ lại!
                ResetInputForm(keepTypeAndDimensions: true);
                SetFeedback($"✓ Đã thêm trục '{baseRoll.RollCode}' (ĐK hiện trạng: {baseRoll.InitialDiameter:0.00} mm) — Sẵn sàng thêm tiếp '{_txtRollCode.Text}'!", isWarning: false);
                _txtInitialDia.Focus();
                _txtInitialDia.SelectAll();
            }
            else
            {
                var createdCodes = new List<string>(batchCount);
                string currentCode = baseRoll.RollCode;

                for (int i = 0; i < batchCount; i++)
                {
                    var item = baseRoll.Clone();
                    item.RollCode = currentCode;
                    _rollService.CreateRoll(item);
                    createdCodes.Add(currentCode);
                    currentCode = IncrementRollCode(currentCode, baseRoll.RollType);
                }

                RefreshData();
                ResetInputForm(keepTypeAndDimensions: true);
                SetFeedback($"✓ Đã tạo nhanh lô {createdCodes.Count} cây trục ({createdCodes.First()} ➔ {createdCodes.Last()}) thành công!", isWarning: false);
            }
        }
        catch (Exception ex)
        {
            SetFeedback($"⛔ Lỗi tạo trục: {ex.Message}", isWarning: true);
            MessageBox.Show($"Lỗi tạo trục: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnUpdateRollClick(object? sender, EventArgs e)
    {
        if (!ValidateForm(out RollEntity roll)) return;

        try
        {
            _rollService.UpdateRoll(roll);
            RefreshData();
            ResetInputForm(keepTypeAndDimensions: true);
            SetFeedback($"✓ Đã lưu cập nhật trục '{roll.RollCode}' thành công!", isWarning: false);
        }
        catch (Exception ex)
        {
            SetFeedback($"⛔ Lỗi cập nhật: {ex.Message}", isWarning: true);
            MessageBox.Show($"Lỗi cập nhật trục: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnDeleteRollClick(object? sender, EventArgs e)
    {
        string rollCode = _txtRollCode.Text.Trim();
        if (string.IsNullOrEmpty(rollCode))
        {
            SetFeedback("⚠️ Vui lòng chọn một trục cần xóa từ bảng bên dưới.", isWarning: true);
            return;
        }

        DialogResult dr = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa trục '{rollCode}' không?\nNếu trục đã có nhật ký mài, toàn bộ nhật ký mài liên quan sẽ bị xóa vĩnh viễn!",
            "Xác nhận xóa trục",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (dr != DialogResult.Yes) return;

        try
        {
            _rollService.DeleteRoll(rollCode, "Xóa từ giao diện QUAN_LY_TRUC");
            RefreshData();
            ResetInputForm(keepTypeAndDimensions: true);
            SetFeedback($"✓ Đã xóa trục '{rollCode}' khỏi danh mục.", isWarning: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi xóa trục: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool ValidateForm(out RollEntity roll)
    {
        roll = new RollEntity
        {
            RollCode = _txtRollCode.Text.Trim().ToUpperInvariant(),
            RollType = _cboRollType.SelectedItem as string ?? "WR",
            CommissionDate = _dtpCommissionDate.Value.Date,
            InitialGrindCount = 0,
            Notes = string.IsNullOrWhiteSpace(_txtNotes.Text) ? null : _txtNotes.Text.Trim()
        };

        if (string.IsNullOrEmpty(roll.RollCode))
        {
            SetFeedback("⚠️ Vui lòng nhập mã trục.", isWarning: true);
            _txtRollCode.Focus();
            return false;
        }

        string maxStr = _txtMaxDia.Text.Trim().Replace(',', '.');
        if (!decimal.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal maxDia) || maxDia <= 0m)
        {
            SetFeedback("⚠️ Đường kính Max gốc phải là số lớn hơn 0.", isWarning: true);
            _txtMaxDia.Focus();
            return false;
        }

        string minStr = _txtMinDia.Text.Trim().Replace(',', '.');
        if (!decimal.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal minDia) || minDia <= 0m)
        {
            SetFeedback("⚠️ Đường kính Min (Scrap) phải là số lớn hơn 0.", isWarning: true);
            _txtMinDia.Focus();
            return false;
        }

        if (minDia >= maxDia)
        {
            SetFeedback($"⚠️ Đường kính Min ({minDia:0.00}) phải nhỏ hơn đường kính Max ({maxDia:0.00})!", isWarning: true);
            _txtMinDia.Focus();
            return false;
        }

        string initStr = _txtInitialDia.Text.Trim().Replace(',', '.');
        if (!decimal.TryParse(initStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal initDia) || initDia <= 0m)
        {
            initDia = maxDia; // Mặc định nếu không nhập thì bằng ĐK Max gốc
        }

        if (initDia > maxDia || initDia <= minDia)
        {
            SetFeedback($"⚠️ ĐK hiện trạng ({initDia:0.00}) phải nằm trong khoảng ({minDia:0.00}, {maxDia:0.00}]!", isWarning: true);
            _txtInitialDia.Focus();
            return false;
        }

        roll.MaxDiameter = maxDia;
        roll.MinDiameter = minDia;
        roll.InitialDiameter = initDia;
        return true;
    }

    private void ResetInputForm(bool keepTypeAndDimensions = false)
    {
        _suppressSelectionChanged = true;
        try
        {
            _dgvRolls.ClearSelection();
        }
        finally
        {
            _suppressSelectionChanged = false;
        }

        _txtRollCode.ReadOnly = false;
        _nudBatchCount.Value = 1;
        _nudBatchCount.Enabled = AppSession.IsAdmin;
        _txtNotes.Clear();

        if (!keepTypeAndDimensions)
        {
            _dtpCommissionDate.Value = DateTime.Today;
        }

        ApplySmartDefaultsForSelectedType(keepCurrentDimensionsIfSameType: keepTypeAndDimensions);
        ApplyRolePermissions();
    }

    /// <summary>
    /// Tự động điền ĐK Max/Min chuẩn theo loại trục (lấy từ cây cùng loại gần nhất trong xưởng hoặc thông số chuẩn nhà máy)
    /// và gợi ý ngay Mã trục tiếp theo (VD: WR-001 -> WR-002) để người dùng không phải gõ lại từng ô!
    /// </summary>
    private void ApplySmartDefaultsForSelectedType(bool keepCurrentDimensionsIfSameType)
    {
        string selectedType = _cboRollType.SelectedItem as string ?? RollTypes.WR;

        if (!keepCurrentDimensionsIfSameType || string.IsNullOrWhiteSpace(_txtMaxDia.Text) || string.IsNullOrWhiteSpace(_txtMinDia.Text))
        {
            RollLifetimeViewDto? templateRoll = _cachedRolls
                .Where(r => string.Equals(r.RollType, selectedType, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.CommissionDate)
                .ThenByDescending(r => r.RollCode, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            decimal stdMax;
            decimal stdMin;
            if (templateRoll != null)
            {
                stdMax = templateRoll.MaxDiameter;
                stdMin = templateRoll.MinDiameter;
            }
            else
            {
                (stdMax, stdMin) = selectedType switch
                {
                    RollTypes.IMR => (650.00m, 600.00m),
                    RollTypes.BUR => (1250.00m, 1170.00m),
                    _ => (450.00m, 400.00m)
                };
            }

            _txtMaxDia.Text = stdMax.ToString("0.00", CultureInfo.InvariantCulture);
            _txtMinDia.Text = stdMin.ToString("0.00", CultureInfo.InvariantCulture);
            _txtInitialDia.Text = stdMax.ToString("0.00", CultureInfo.InvariantCulture);
            _txtInitialDia.Tag = "auto";
        }
        else
        {
            // Giữ nguyên ĐK Max/Min người dùng vừa nhập cho cây trước, đặt lại ĐK Hiện trạng = ĐK Max để sẵn sàng cho cây tiếp theo
            _txtInitialDia.Text = _txtMaxDia.Text;
            _txtInitialDia.Tag = "auto";
        }

        _txtRollCode.Text = SuggestNextRollCode(selectedType);
    }

    private string SuggestNextRollCode(string rollType)
    {
        string prefix = $"{rollType.ToUpperInvariant()}-";
        int maxNumber = 0;

        foreach (RollLifetimeViewDto r in _cachedRolls.Where(x => string.Equals(x.RollType, rollType, StringComparison.OrdinalIgnoreCase)))
        {
            string code = r.RollCode.Trim().ToUpperInvariant();
            if (code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                string digits = new(code[prefix.Length..].TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int num) && num > maxNumber)
                {
                    maxNumber = num;
                }
            }
        }

        return $"{prefix}{maxNumber + 1:D3}";
    }

    private static string IncrementRollCode(string currentCode, string rollType)
    {
        string trimmed = currentCode.Trim().ToUpperInvariant();
        int lastDash = trimmed.LastIndexOf('-');
        if (lastDash >= 0 && lastDash + 1 < trimmed.Length)
        {
            string prefix = trimmed[..(lastDash + 1)];
            string numberPart = trimmed[(lastDash + 1)..];
            if (int.TryParse(numberPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            {
                int padLen = Math.Max(3, numberPart.Length);
                return $"{prefix}{(n + 1).ToString($"D{padLen}", CultureInfo.InvariantCulture)}";
            }
        }

        return $"{rollType.ToUpperInvariant()}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
    }

    private void SetFeedback(string message, bool isWarning)
    {
        _lblFormFeedback.Text = message;
        _lblFormFeedback.ForeColor = isWarning ? IndustrialTheme.CritText : IndustrialTheme.SafeText;
    }

    private void ApplyRolePermissions()
    {
        bool isAdmin = AppSession.IsAdmin;
        bool isEditing = _txtRollCode.ReadOnly;

        _btnCreateRoll.Enabled = isAdmin && !isEditing;
        _btnUpdateRoll.Enabled = isAdmin && isEditing;
        _btnDeleteRoll.Enabled = isAdmin && isEditing;
        _nudBatchCount.Enabled = isAdmin && !isEditing;
    }

    private void ShowSelectedRollWearChart()
    {
        if (_dgvRolls.CurrentRow?.DataBoundItem is not RollLifetimeViewDto roll)
        {
            MessageBox.Show(
                "Vui lòng click chọn một cây trục từ bảng danh sách bên dưới để xem Biểu đồ đường cong hao mòn.",
                "Chưa chọn trục",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            IReadOnlyList<GrindingLogViewDto> history = _rollService.GetGrindingLogViewsByRoll(roll.RollCode);
            using RollWearChartDialog dialog = new(roll, history);
            dialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi mở biểu đồ mòn trục: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportSelectedRollPassport()
    {
        if (_dgvRolls.CurrentRow?.DataBoundItem is not RollLifetimeViewDto roll)
        {
            MessageBox.Show(
                "Vui lòng chọn một cây trục từ danh sách bảng bên dưới để xuất Phiếu Lý Lịch Trục (PDF).",
                "Chưa chọn trục",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            using SaveFileDialog sfd = new()
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"LyLichTruc_{roll.RollCode}_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                var history = _rollService.GetGrindingLogViewsByRoll(roll.RollCode);
                ExportService.ExportRollPassportToPdf(sfd.FileName, roll, history);

                DialogResult openNow = MessageBox.Show(
                    $"Xuất Lý Lịch Trục ({roll.RollCode}) thành công!\n\nĐường dẫn: {sfd.FileName}\n\nBạn có muốn mở file PDF ngay không?",
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
            MessageBox.Show($"Lỗi xuất PDF lý lịch trục: {ex.Message}", "Lỗi xuất file", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
