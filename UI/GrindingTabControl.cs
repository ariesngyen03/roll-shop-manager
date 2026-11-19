using System.Globalization;
using RollShopManager.Models;
using RollShopManager.Security;
using RollShopManager.Services;

namespace RollShopManager.UI;

/// <summary>
/// UserControl quản lý ca mài dùng chung cho 3 tab MAI_WR, MAI_BUR, MAI_IMR.
/// Tích hợp 4 triết lý chuẩn quốc tế:
/// - ISA-101 HMI: Tối giản nhiễu thị giác, chỉ tô màu cho ngoại lệ (Color by Exception).
/// - Herkules RSMS: Thẻ "HỘ CHIẾU TRỤC (ROLL PASSPORT)" cập nhật tức thời (mặt cắt lớp vỏ hợp kim, cảnh báo ca trước, 3 ca gần nhất).
/// - Tulip Poka-Yoke: Kiểm tra 3 tầng ngay khi gõ (Three-Tier Live Validation) chống gõ nhầm dấu chấm thập phân.
/// - Linear Keyboard-First: Luồng thao tác 10 giây không dùng chuột + Toast Banner tự ẩn sau 3 giây.
/// </summary>
public sealed class GrindingTabControl : UserControl
{
    private readonly string _rollType;
    private readonly RollBusinessService _rollService;
    private readonly decimal _pokaYokeMaxWearMm;

    // Các control nhập liệu (Cột Trái 55%: Operator Input Console)
    private readonly ComboBox _cboRollCode;
    private readonly DateTimePicker _dtpGrindDate;
    private readonly TextBox _txtPreDia;
    private readonly TextBox _txtPostDia;
    private readonly ComboBox _cboCondition;
    private readonly TextBox _txtRemarks;

    // Nhãn kiểm tra 3 tầng Poka-Yoke Live Guardrails
    private readonly Label _lblLiveGuardrail;

    // Thanh thông báo Toast Banner 3 giây (Linear Keyboard-First UX)
    private readonly Panel _pnlToastBanner;
    private readonly Label _lblToastMessage;
    private readonly System.Windows.Forms.Timer _toastTimer;

    // Cột Phải 45%: Thẻ "HỘ CHIẾU TRỤC (ROLL PASSPORT)" - Herkules RSMS Style
    private readonly Label _lblPassportTitle;
    private readonly Label _lblRollStatusBadge;
    private readonly Panel _pnlAlloyShellBar;
    private readonly Label _lblLastGrindAlert;
    private readonly Label _lblRecentThreeGrinds;

    // Dữ liệu vẽ thanh mặt cắt vỏ hợp kim hiện tại
    private decimal _curMaxDia;
    private decimal _curMinDia;
    private decimal _curCurrentDia;

    // Nút thao tác
    private readonly Button _btnSaveNew;
    private readonly Button _btnUpdateSelected;
    private readonly Button _btnDeleteSelected;
    private readonly Button _btnClearForm;

    // Bảng nhật ký mài & bộ lọc nhanh
    private readonly DataGridView _dgvLogs;
    private readonly TextBox _txtFilterRoll;
    private readonly ComboBox _cboFilterCondition;
    private readonly Label _lblGridStats;
    private List<GrindingLogViewDto> _cachedLogs = new();
    private int? _selectedLogId;
    private bool _suppressSelectionChanged;
    private bool _suppressRollCodeChanged;
    private bool _hasPhysicalInputError;

    public GrindingTabControl(string rollType)
    {
        _rollType = RollTypes.Normalize(rollType);
        if (!RollTypes.IsValid(_rollType))
        {
            throw new ArgumentException($"Loại trục '{rollType}' không hợp lệ. Chỉ nhận WR, BUR hoặc IMR.", nameof(rollType));
        }

        _rollService = new RollBusinessService();
        // Ngưỡng Poka-Yoke cảnh báo mài quá sâu / gõ nhầm dấu chấm thập phân: > 3.0mm cho WR/IMR, > 5.0mm cho BUR
        _pokaYokeMaxWearMm = _rollType == RollTypes.BUR ? 5.0m : 3.0m;

        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        BackColor = IndustrialTheme.AppBackground;
        Font = IndustrialTheme.BodyFont;
        Padding = new Padding(16, 12, 16, 12);

        // Thanh thông báo Toast Banner (Linear UX - Không chặn luồng gõ phím bằng MessageBox)
        _pnlToastBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = IndustrialTheme.SafeBg,
            Padding = new Padding(14, 4, 14, 4),
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

        // Timer tự động ẩn Toast Banner sau 3 giây
        _toastTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer.Stop();
            _pnlToastBanner.Visible = false;
        };

        // Bố cục chính chia làm 2 phần: Top (55% Input Console | 45% Roll Passport), Bottom (Bảng lịch sử)
        TableLayoutPanel rootLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 276)); // Khu vực Console & Hộ chiếu trục
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Bảng lịch sử mài
        Controls.Add(rootLayout);
        Controls.Add(_pnlToastBanner);
        rootLayout.BringToFront();

        // =====================================================================
        // PHẦN 1: 2 CỘT ĐỐI XỨNG (55% OPERATOR CONSOLE | 45% ROLL PASSPORT)
        // =====================================================================
        TableLayoutPanel topSplit = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        topSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f)); // Cột Trái (55%): Khu vực Nhập Ca Mài
        topSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f)); // Cột Phải (45%): Hộ chiếu Trục (Roll Passport)
        rootLayout.Controls.Add(topSplit, 0, 0);

        // ---------------------------------------------------------------------
        // CỘT TRÁI (55%): KHU VỰC NHẬP CA MÀI (OPERATOR INPUT CONSOLE)
        // ---------------------------------------------------------------------
        CardPanel pnlInputCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0, 0, 8, 10)
        };
        topSplit.Controls.Add(pnlInputCard, 0, 0);

        TableLayoutPanel formGrid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 6,
            BackColor = Color.Transparent
        };
        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
        formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        formGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Hàng 0: Tiêu đề Console
        formGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Hàng 1: Mã trục & Thời gian
        formGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Hàng 2: ĐK trước & ĐK sau mài
        formGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Hàng 3: Tình trạng ca & Ghi chú lỗi
        formGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Hàng 4: Poka-Yoke 3-Tier Live Guardrail Banner
        formGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Hàng 5: Nút thao tác
        pnlInputCard.Controls.Add(formGrid);

        Label lblFormTitle = new()
        {
            Text = $"NHẬP CA MÀI TRỤC {_rollType} ({RollTypes.GetDisplayName(_rollType)}) — GÕ MÃ TRỤC ➔ ENTER ➔ NHẬP ĐK SAU ➔ ENTER",
            Font = IndustrialTheme.CardTitleFont,
            ForeColor = IndustrialTheme.PrimaryAccent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
            AutoEllipsis = true
        };
        formGrid.SetColumnSpan(lblFormTitle, 4);
        formGrid.Controls.Add(lblFormTitle, 0, 0);

        // Khởi tạo các nút thao tác trước để gắn sự kiện phím tắt Enter
        _btnSaveNew = new Button { Width = 165, Height = 34, Margin = new Padding(0, 2, 8, 0) };
        IndustrialTheme.StylePrimaryButton(_btnSaveNew, "💾 Ghi Nhận Ca Mài");
        _btnSaveNew.Click += OnSaveNewClick;

        _btnUpdateSelected = new Button { Width = 152, Height = 34, Margin = new Padding(0, 2, 8, 0) };
        IndustrialTheme.StyleOutlineButton(_btnUpdateSelected, "✏️ Sửa Dòng Chọn", IndustrialTheme.PrimaryAccent);
        _btnUpdateSelected.Click += OnUpdateSelectedClick;

        _btnDeleteSelected = new Button { Width = 152, Height = 34, Margin = new Padding(0, 2, 8, 0) };
        IndustrialTheme.StyleOutlineButton(_btnDeleteSelected, "🗑️ Xóa Dòng Chọn", IndustrialTheme.DangerAccent);
        _btnDeleteSelected.Click += OnDeleteSelectedClick;

        _btnClearForm = new Button { Width = 112, Height = 34, Margin = new Padding(0, 2, 0, 0) };
        IndustrialTheme.StyleSecondaryButton(_btnClearForm, "🔄 Làm Mới");
        _btnClearForm.Click += (s, e) => ResetInputForm(focusRollSelector: true);

        // Hàng 1: Mã trục (AutoComplete SuggestAppend) & Thời gian mài
        Label lblRoll = IndustrialTheme.CreateFormLabel("Mã trục:", bold: true);
        _cboRollCode = new ComboBox { Dock = DockStyle.Fill };
        IndustrialTheme.StyleComboBox(_cboRollCode);
        // Bật SuggestAppend để công nhân gõ nhanh mã trục bằng bàn phím
        _cboRollCode.DropDownStyle = ComboBoxStyle.DropDown;
        _cboRollCode.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _cboRollCode.AutoCompleteSource = AutoCompleteSource.ListItems;
        _cboRollCode.SelectedIndexChanged += OnRollCodeChanged;
        _cboRollCode.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                CommitTypedRollCodeAndFocusPostDia();
            }
        };

        Label lblDate = IndustrialTheme.CreateFormLabel("Thời gian mài:", leftMargin: 12);
        _dtpGrindDate = new DateTimePicker { Dock = DockStyle.Fill };
        IndustrialTheme.StyleDateTimePicker(_dtpGrindDate, "dd/MM/yyyy HH:mm");

        formGrid.Controls.Add(lblRoll, 0, 1);
        formGrid.Controls.Add(_cboRollCode, 1, 1);
        formGrid.Controls.Add(lblDate, 2, 1);
        formGrid.Controls.Add(_dtpGrindDate, 3, 1);

        // Hàng 2: ĐK trước mài & ĐK sau mài
        Label lblPre = IndustrialTheme.CreateFormLabel("ĐK trước (mm):");
        _txtPreDia = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            TabStop = false
        };
        IndustrialTheme.StyleInputBox(_txtPreDia);
        _txtPreDia.BackColor = Color.FromArgb(241, 245, 249);
        _txtPreDia.Font = IndustrialTheme.BodyBoldFont;
        _txtPreDia.ForeColor = Color.FromArgb(30, 41, 59);

        Label lblPost = IndustrialTheme.CreateFormLabel("ĐK sau mài (mm):", bold: true, color: IndustrialTheme.PrimaryAccent, leftMargin: 12);
        _txtPostDia = new TextBox { Dock = DockStyle.Fill };
        IndustrialTheme.StyleInputBox(_txtPostDia);
        _txtPostDia.Font = IndustrialTheme.BodyBoldFont;
        _txtPostDia.TextChanged += OnPostDiaTextChanged;
        _txtPostDia.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                if (_selectedLogId.HasValue && AppSession.IsAdmin)
                {
                    _btnUpdateSelected.PerformClick();
                }
                else if (_btnSaveNew.Enabled)
                {
                    _btnSaveNew.PerformClick();
                }
            }
        };

        formGrid.Controls.Add(lblPre, 0, 2);
        formGrid.Controls.Add(_txtPreDia, 1, 2);
        formGrid.Controls.Add(lblPost, 2, 2);
        formGrid.Controls.Add(_txtPostDia, 3, 2);

        // Hàng 3: Tình trạng ca & Ghi chú lỗi
        Label lblCond = IndustrialTheme.CreateFormLabel("Tình trạng ca:");
        _cboCondition = new ComboBox { Dock = DockStyle.Fill };
        IndustrialTheme.StyleComboBox(_cboCondition);
        _cboCondition.Items.AddRange(new object[] { GrindConditionTexts.Normal, GrindConditionTexts.Abnormal });
        _cboCondition.SelectedIndex = 0;

        Label lblRem = IndustrialTheme.CreateFormLabel("Ghi chú lỗi:", leftMargin: 12);
        _txtRemarks = new TextBox { Dock = DockStyle.Fill, MaxLength = 1000 };
        IndustrialTheme.StyleInputBox(_txtRemarks);
        _txtRemarks.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && _btnSaveNew.Enabled)
            {
                e.SuppressKeyPress = true;
                _btnSaveNew.PerformClick();
            }
        };

        formGrid.Controls.Add(lblCond, 0, 3);
        formGrid.Controls.Add(_cboCondition, 1, 3);
        formGrid.Controls.Add(lblRem, 2, 3);
        formGrid.Controls.Add(_txtRemarks, 3, 3);

        // Hàng 4: Nhãn kiểm tra 3 tầng Tulip Poka-Yoke Live Guardrail
        _lblLiveGuardrail = new Label
        {
            Dock = DockStyle.Fill,
            Font = IndustrialTheme.SmallCaptionFont,
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = IndustrialTheme.MutedText,
            Padding = new Padding(8, 2, 8, 2),
            Margin = new Padding(0, 2, 8, 2),
            Text = "Nhập ĐK sau mài (mm) để hệ thống kiểm tra tự động 3 tầng an toàn (Poka-Yoke)...",
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
            AutoEllipsis = true
        };
        formGrid.SetColumnSpan(_lblLiveGuardrail, 4);
        formGrid.Controls.Add(_lblLiveGuardrail, 0, 4);

        // Hàng 5: Nút bấm
        FlowLayoutPanel pnlActionButtons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 0)
        };
        formGrid.SetColumnSpan(pnlActionButtons, 4);
        formGrid.Controls.Add(pnlActionButtons, 0, 5);
        pnlActionButtons.Controls.Add(_btnSaveNew);
        pnlActionButtons.Controls.Add(_btnUpdateSelected);
        pnlActionButtons.Controls.Add(_btnDeleteSelected);
        pnlActionButtons.Controls.Add(_btnClearForm);

        // ---------------------------------------------------------------------
        // CỘT PHẢI (45%): THẺ "HỘ CHIẾU TRỤC (ROLL PASSPORT)" - HERKULES RSMS
        // ---------------------------------------------------------------------
        CardPanel pnlPassportCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0, 0, 0, 10)
        };
        topSplit.Controls.Add(pnlPassportCard, 1, 0);

        TableLayoutPanel passportLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };
        passportLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Hàng 1: Tiêu đề Passport + Status Badge
        passportLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); // Hàng 2: Thanh cấu trúc lớp vỏ hợp kim (Alloy Shell)
        passportLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Hàng 3: Cảnh báo nổi lần mài gần nhất
        passportLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Hàng 4: Lịch sử 3 ca mài gần nhất của trục
        pnlPassportCard.Controls.Add(passportLayout);

        // Hàng 1: Tiêu đề Hộ chiếu trục & Badge trạng thái
        TableLayoutPanel passportHeader = new() { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.Transparent };
        passportHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
        passportHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
        passportLayout.Controls.Add(passportHeader, 0, 0);

        _lblPassportTitle = new Label
        {
            Text = "HỘ CHIẾU TRỤC (ROLL PASSPORT)",
            Font = IndustrialTheme.BodyBoldFont,
            ForeColor = Color.FromArgb(15, 23, 42),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        passportHeader.Controls.Add(_lblPassportTitle, 0, 0);

        _lblRollStatusBadge = new Label
        {
            Text = "Chưa chọn trục",
            Font = IndustrialTheme.BadgeFont,
            ForeColor = IndustrialTheme.MutedText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };
        passportHeader.Controls.Add(_lblRollStatusBadge, 1, 0);

        // Hàng 2: Thanh mặt cắt lớp vỏ hợp kim (ĐK Hiện tại vs ĐK Min & ĐK Max)
        _pnlAlloyShellBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        IndustrialTheme.EnableDoubleBuffer(_pnlAlloyShellBar);
        _pnlAlloyShellBar.Paint += OnAlloyShellBarPaint;
        _pnlAlloyShellBar.Resize += (s, e) => _pnlAlloyShellBar.Invalidate();
        passportLayout.Controls.Add(_pnlAlloyShellBar, 0, 1);

        // Hàng 3: Cảnh báo nổi nếu lần mài gần nhất của trục này là Bất thường
        _lblLastGrindAlert = new Label
        {
            Dock = DockStyle.Fill,
            Font = IndustrialTheme.SmallCaptionFont,
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = IndustrialTheme.MutedText,
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(0, 2, 0, 4),
            Text = "Chọn một mã trục để kiểm tra tình trạng bề mặt ca trước...",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        passportLayout.Controls.Add(_lblLastGrindAlert, 0, 2);

        // Hàng 4: Tóm tắt 3 ca mài gần nhất của riêng cây trục đang chọn
        _lblRecentThreeGrinds = new Label
        {
            Dock = DockStyle.Fill,
            Font = IndustrialTheme.SmallCaptionFont,
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(51, 65, 85),
            Padding = new Padding(8, 6, 8, 6),
            Margin = new Padding(0, 0, 0, 2),
            Text = "3 CA MÀI GẦN NHẤT CỦA TRỤC:\n• (Chưa có dữ liệu)",
            TextAlign = ContentAlignment.TopLeft
        };
        passportLayout.Controls.Add(_lblRecentThreeGrinds, 0, 3);

        // =====================================================================
        // PHẦN 2: BẢNG LỊCH SỬ MÀI & BỘ LỌC NHANH (BOTTOM)
        // =====================================================================
        CardPanel pnlGridCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0)
        };
        rootLayout.Controls.Add(pnlGridCard, 0, 1);

        TableLayoutPanel bottomLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Thanh lọc & thống kê nhanh
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Bảng lịch sử
        pnlGridCard.Controls.Add(bottomLayout);

        FlowLayoutPanel filterBar = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        bottomLayout.Controls.Add(filterBar, 0, 0);

        Label lblSearchRoll = new() { Text = "Lọc mã trục:", AutoSize = true, Margin = new Padding(0, 8, 4, 0), Font = IndustrialTheme.BodyBoldFont };
        _txtFilterRoll = new TextBox { Width = 120 };
        IndustrialTheme.StyleInputBox(_txtFilterRoll);
        _txtFilterRoll.Margin = new Padding(0, 4, 14, 0);
        _txtFilterRoll.TextChanged += (s, e) => ApplyGridFilter();

        Label lblFilCond = new() { Text = "Tình trạng:", AutoSize = true, Margin = new Padding(0, 8, 4, 0), Font = IndustrialTheme.BodyBoldFont };
        _cboFilterCondition = new ComboBox { Width = 135 };
        IndustrialTheme.StyleComboBox(_cboFilterCondition);
        _cboFilterCondition.Margin = new Padding(0, 4, 14, 0);
        _cboFilterCondition.Items.AddRange(new object[] { "Tất cả", GrindConditionTexts.Normal, GrindConditionTexts.Abnormal });
        _cboFilterCondition.SelectedIndex = 0;
        _cboFilterCondition.SelectedIndexChanged += (s, e) => ApplyGridFilter();

        Button btnChart = new() { Width = 175, Height = 32, Margin = new Padding(0, 2, 16, 0) };
        IndustrialTheme.StyleOutlineButton(btnChart, "📈 Biểu Đồ Mòn Trục", IndustrialTheme.PrimaryAccent);
        btnChart.Click += (s, e) => ShowCurrentRollWearChart();

        _lblGridStats = new Label
        {
            AutoSize = true,
            Font = IndustrialTheme.BodyBoldFont,
            ForeColor = IndustrialTheme.MutedText,
            Margin = new Padding(0, 8, 0, 0),
            Text = "Tổng: 0 ca mài"
        };

        filterBar.Controls.Add(lblSearchRoll);
        filterBar.Controls.Add(_txtFilterRoll);
        filterBar.Controls.Add(lblFilCond);
        filterBar.Controls.Add(_cboFilterCondition);
        filterBar.Controls.Add(btnChart);
        filterBar.Controls.Add(_lblGridStats);

        _dgvLogs = new DataGridView
        {
            Dock = DockStyle.Fill
        };
        ConfigureDataGridColumns();
        IndustrialTheme.StyleDataGridView(_dgvLogs);
        _dgvLogs.SelectionChanged += OnGridSelectionChanged;
        _dgvLogs.CellClick += (s, e) =>
        {
            if (e.RowIndex >= 0)
            {
                OnGridSelectionChanged(_dgvLogs, EventArgs.Empty);
            }
        };
        _dgvLogs.RowPrePaint += OnGridRowPrePaint;
        bottomLayout.Controls.Add(_dgvLogs, 0, 1);

        AppSession.RoleChanged += (s, e) =>
        {
            if (!AppSession.IsAdmin && _selectedLogId.HasValue)
            {
                ResetInputForm(focusRollSelector: false);
            }
            else
            {
                ApplyRolePermissions();
            }
        };

        ApplyRolePermissions();
    }

    private void OnAlloyShellBarPaint(object? sender, PaintEventArgs e)
    {
        Rectangle bounds = new(0, 0, _pnlAlloyShellBar.Width, _pnlAlloyShellBar.Height);
        IndustrialTheme.DrawAlloyShellBar(e.Graphics, bounds, _curMaxDia, _curMinDia, _curCurrentDia);
    }

    private void ConfigureDataGridColumns()
    {
        _dgvLogs.Columns.Clear();
        _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "Mã Ca", DataPropertyName = "Id", Width = 65 });
        _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "RollCode", HeaderText = "Mã Trục", DataPropertyName = "RollCode", Width = 95 });
        _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "GrindSequence", HeaderText = "Lần Mài", DataPropertyName = "GrindSequence", Width = 75 });
        _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "GrindDate", HeaderText = "Thời Gian Mài", DataPropertyName = "GrindDate", Width = 135, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy HH:mm" } });
        _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "PreGrindDiameter", HeaderText = "ĐK Trước (mm)", DataPropertyName = "PreGrindDiameter", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" } });
        _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "PostGrindDiameter", HeaderText = "ĐK Sau (mm)", DataPropertyName = "PostGrindDiameter", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" } });
        _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "GrindConsumption", HeaderText = "Tiêu Hao (mm)", DataPropertyName = "GrindConsumption", Width = 105, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" } });
        _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "CumulativeWear", HeaderText = "Lũy Kế (mm)", DataPropertyName = "CumulativeWear", Width = 105, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" } });
        _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "RemainingLifetimePercent", HeaderText = "% Lifetime Còn Lại", DataPropertyName = "RemainingLifetimePercent", Width = 140 });
        _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "EstimatedRemainingGrinds", HeaderText = "Còn Mài (Lần)", DataPropertyName = "EstimatedRemainingGrinds", Width = 105 });
        _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "ConditionText", HeaderText = "Tình Trạng", DataPropertyName = "ConditionText", Width = 115 });
        _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Remarks", HeaderText = "Ghi Chú Kỹ Thuật / Nguyên Nhân", DataPropertyName = "Remarks", Width = 220 });
    }

    public void RefreshData()
    {
        LoadRollList();
        LoadGridData();
    }

    private void LoadRollList()
    {
        string? currentSelected = _cboRollCode.SelectedValue as string ?? _cboRollCode.Text?.Trim();

        IReadOnlyList<RollLifetimeViewDto> lifetimes = _rollService.GetRollLifetimes(_rollType);

        List<RollLifetimeViewDto> availableRolls = lifetimes
            .Where(r => AppSession.IsAdmin || !r.IsScrap || string.Equals(r.RollCode, currentSelected, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.RollCode)
            .ToList();

        _suppressRollCodeChanged = true;
        try
        {
            _cboRollCode.DisplayMember = "RollCode";
            _cboRollCode.ValueMember = "RollCode";
            _cboRollCode.DataSource = availableRolls;

            if (availableRolls.Count > 0)
            {
                var matched = availableRolls.FirstOrDefault(r => string.Equals(r.RollCode, currentSelected, StringComparison.OrdinalIgnoreCase));
                if (matched != null)
                {
                    _cboRollCode.SelectedValue = matched.RollCode;
                }
                else
                {
                    _cboRollCode.SelectedIndex = 0;
                }
            }
        }
        finally
        {
            _suppressRollCodeChanged = false;
        }

        if (availableRolls.Count > 0)
        {
            RefreshSelectedRollPassport(focusPostDia: false);
        }
        else
        {
            ResetPassportCard();
        }
    }

    private void LoadGridData()
    {
        _cachedLogs = _rollService.GetGrindingLogViews(_rollType, newestFirst: true).ToList();
        ApplyGridFilter();
    }

    private void ApplyGridFilter()
    {
        string searchCode = _txtFilterRoll.Text.Trim().ToUpperInvariant();
        string condFilter = _cboFilterCondition.SelectedItem as string ?? "Tất cả";

        IEnumerable<GrindingLogViewDto> query = _cachedLogs;

        if (!string.IsNullOrEmpty(searchCode))
        {
            query = query.Where(l => l.RollCode.ToUpperInvariant().Contains(searchCode));
        }

        if (condFilter != "Tất cả")
        {
            query = query.Where(l => l.ConditionText == condFilter);
        }

        var list = query.ToList();
        int totalCount = list.Count;
        decimal totalWear = list.Sum(l => l.GrindConsumption);
        int abnormalCount = list.Count(l => l.IsAbnormal);
        _lblGridStats.Text = $"Tổng: {totalCount} ca  |  Tổng mài: {totalWear:0.00} mm  |  Sự cố: {abnormalCount} ca";

        _suppressSelectionChanged = true;
        try
        {
            _dgvLogs.DataSource = list;
            _dgvLogs.ClearSelection();
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
    }

    private void CommitTypedRollCodeAndFocusPostDia()
    {
        string typed = _cboRollCode.Text.Trim().ToUpperInvariant();
        if (_cboRollCode.DataSource is List<RollLifetimeViewDto> list)
        {
            var match = list.FirstOrDefault(r => string.Equals(r.RollCode, typed, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                _cboRollCode.SelectedValue = match.RollCode;
            }
        }

        RefreshSelectedRollPassport(focusPostDia: true);
    }

    private void OnRollCodeChanged(object? sender, EventArgs e)
    {
        if (_suppressRollCodeChanged)
        {
            return;
        }

        RefreshSelectedRollPassport(focusPostDia: true);
    }

    private void RefreshSelectedRollPassport(bool focusPostDia)
    {
        if (_cboRollCode.SelectedValue is not string rollCode || string.IsNullOrEmpty(rollCode))
        {
            ResetPassportCard();
            return;
        }

        try
        {
            // 1. Tự động lấy ĐK trước mài
            decimal preDia = _rollService.GetPreGrindDiameter(rollCode);
            _txtPreDia.Text = preDia.ToString("0.0##", CultureInfo.InvariantCulture);

            // 2. Cập nhật Hộ chiếu trục (Herkules RSMS Contextual Roll Passport)
            RollLifetimeViewDto lifetime = _rollService.GetRollLifetime(rollCode);
            IReadOnlyList<GrindingLogViewDto> history = _rollService.GetGrindingLogViewsByRoll(rollCode);
            UpdatePassportCard(lifetime, history);

            // 3. Công thái học Linear Keyboard-First: Tự động chuyển focus sang ô ĐK sau mài và bôi đen sẵn
            if (focusPostDia && !_selectedLogId.HasValue)
            {
                _txtPostDia.Clear();
                _txtPostDia.Focus();
                _txtPostDia.SelectAll();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải hộ chiếu trục: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Triết lý Herkules RSMS: Cập nhật tức thời Thẻ Hộ Chiếu Trục (Roll Passport)
    /// gồm mặt cắt vỏ hợp kim, cảnh báo nổi nếu ca trước bất thường, và 3 ca mài gần nhất.
    /// </summary>
    private void UpdatePassportCard(RollLifetimeViewDto lifetime, IReadOnlyList<GrindingLogViewDto> history)
    {
        _curMaxDia = lifetime.MaxDiameter;
        _curMinDia = lifetime.MinDiameter;
        _curCurrentDia = lifetime.CurrentDiameter;

        string remainingGrindInfo = lifetime.LoggedGrindCount > 0 && lifetime.EstimatedRemainingGrinds > 0
            ? $"Còn ~{lifetime.EstimatedRemainingGrinds} lần mài | TB: {lifetime.AverageWearPerGrind:0.00} mm/ca"
            : (lifetime.IsScrap ? "Đã chạm ngưỡng Scrap" : "Mới theo dõi - Chưa có ca mài");
        _lblPassportTitle.Text = $"HỘ CHIẾU TRỤC: {lifetime.RollCode} ({remainingGrindInfo})";
        _lblRollStatusBadge.Text = $"{lifetime.Status} ({lifetime.RemainingLifetimePercent:0.0}%)";

        (Color _, Color fg) = IndustrialTheme.ResolveStatusColors(lifetime.Status);
        _lblRollStatusBadge.ForeColor = fg;

        _pnlAlloyShellBar.Invalidate();

        // Kiểm tra lần mài gần nhất của trục này có trạng thái "Bất thường" hay không
        GrindingLogViewDto? lastGrind = history.OrderByDescending(h => h.GrindDate).ThenByDescending(h => h.Id).FirstOrDefault();
        if (lastGrind != null && lastGrind.IsAbnormal)
        {
            _lblLastGrindAlert.BackColor = IndustrialTheme.WarnBg;
            _lblLastGrindAlert.ForeColor = IndustrialTheme.WarnText;
            _lblLastGrindAlert.Font = IndustrialTheme.BadgeFont;
            _lblLastGrindAlert.Text = $"⚠️ CA TRƯỚC BẤT THƯỜNG ({lastGrind.GrindDate:dd/MM HH:mm}): {lastGrind.Remarks ?? "Cần kiểm tra kỹ bề mặt trục!"}";
        }
        else if (lastGrind != null)
        {
            _lblLastGrindAlert.BackColor = Color.FromArgb(241, 245, 249);
            _lblLastGrindAlert.ForeColor = IndustrialTheme.SafeText;
            _lblLastGrindAlert.Font = IndustrialTheme.SmallCaptionFont;
            _lblLastGrindAlert.Text = $"✓ Ca gần nhất (Lần #{lastGrind.GrindSequence} - {lastGrind.GrindDate:dd/MM HH:mm}): Mài chuẩn (-{lastGrind.GrindConsumption:0.00} mm). Bề mặt ổn định.";
        }
        else
        {
            _lblLastGrindAlert.BackColor = Color.FromArgb(241, 245, 249);
            _lblLastGrindAlert.ForeColor = IndustrialTheme.MutedText;
            _lblLastGrindAlert.Font = IndustrialTheme.SmallCaptionFont;
            _lblLastGrindAlert.Text = "ℹ Trục chưa có lịch sử mài trên phần mềm — Sẵn sàng ghi nhận ca đầu tiên.";
        }

        // Hiển thị tóm tắt 3 ca mài gần nhất của riêng cây trục đang chọn
        List<GrindingLogViewDto> recent3 = history
            .OrderByDescending(h => h.GrindDate)
            .ThenByDescending(h => h.Id)
            .Take(3)
            .ToList();

        if (recent3.Count == 0)
        {
            _lblRecentThreeGrinds.Text = "3 CA MÀI GẦN NHẤT CỦA TRỤC:\n• Chưa phát sinh ca mài nào.";
        }
        else
        {
            var lines = new List<string> { "3 CA MÀI GẦN NHẤT CỦA TRỤC ĐANG CHỌN:" };
            foreach (var g in recent3)
            {
                string tag = g.IsAbnormal ? $"[SỰ CỐ: {g.Remarks}]" : "[Chuẩn]";
                lines.Add($"• Lần #{g.GrindSequence} ({g.GrindDate:dd/MM HH:mm}): {g.PreGrindDiameter:0.00} ➔ {g.PostGrindDiameter:0.00} mm (-{g.GrindConsumption:0.00} mm) {tag}");
            }
            _lblRecentThreeGrinds.Text = string.Join(Environment.NewLine, lines);
        }
    }

    private void ResetPassportCard()
    {
        _curMaxDia = 0;
        _curMinDia = 0;
        _curCurrentDia = 0;
        _lblPassportTitle.Text = "HỘ CHIẾU TRỤC (ROLL PASSPORT)";
        _lblRollStatusBadge.Text = "Chưa chọn trục";
        _lblRollStatusBadge.ForeColor = IndustrialTheme.MutedText;
        _lblLastGrindAlert.BackColor = Color.FromArgb(248, 250, 252);
        _lblLastGrindAlert.ForeColor = IndustrialTheme.MutedText;
        _lblLastGrindAlert.Text = "Chọn một mã trục để kiểm tra tình trạng bề mặt ca trước...";
        _lblRecentThreeGrinds.Text = "3 CA MÀI GẦN NHẤT CỦA TRỤC:\n• (Chưa có dữ liệu)";
        _pnlAlloyShellBar.Invalidate();
    }

    /// <summary>
    /// Triết lý Tulip Poka-Yoke: Kiểm tra 3 tầng ngay khi công nhân gõ vào ô ĐK sau mài (Three-Tier Live Validation).
    /// </summary>
    private void OnPostDiaTextChanged(object? sender, EventArgs e)
    {
        _hasPhysicalInputError = false;
        string preStr = _txtPreDia.Text.Trim().Replace(',', '.');
        string postStr = _txtPostDia.Text.Trim().Replace(',', '.');

        if (string.IsNullOrEmpty(postStr))
        {
            _txtPostDia.BackColor = IndustrialTheme.CardBackground;
            _lblLiveGuardrail.BackColor = Color.FromArgb(248, 250, 252);
            _lblLiveGuardrail.ForeColor = IndustrialTheme.MutedText;
            _lblLiveGuardrail.Font = IndustrialTheme.SmallCaptionFont;
            _lblLiveGuardrail.Text = "Nhập ĐK sau mài (mm) để hệ thống kiểm tra tự động 3 tầng an toàn (Poka-Yoke)...";
            ApplyRolePermissions();
            return;
        }

        if (!decimal.TryParse(preStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal preDia) ||
            !decimal.TryParse(postStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal postDia) ||
            postDia <= 0m || postDia >= preDia)
        {
            // TẦNG 1: LỖI VẬT LÝ — CHẶN CỨNG NÚT LƯU
            _hasPhysicalInputError = true;
            _txtPostDia.BackColor = IndustrialTheme.CritBg;
            _lblLiveGuardrail.BackColor = IndustrialTheme.CritBg;
            _lblLiveGuardrail.ForeColor = IndustrialTheme.CritText;
            _lblLiveGuardrail.Font = IndustrialTheme.BadgeFont;
            _lblLiveGuardrail.Text = "⛔ LỖI: Đường kính sau mài phải nhỏ hơn đường kính trước mài!";
            ApplyRolePermissions();
            return;
        }

        decimal wear = preDia - postDia;
        string? rollCode = _cboRollCode.SelectedValue as string;
        RollEntity? roll = !string.IsNullOrEmpty(rollCode) ? _rollService.GetRoll(rollCode) : null;

        if (roll != null && postDia <= roll.MinDiameter)
        {
            // TẦNG 3: CHẠM NGƯỠNG PHẾ PHẨM (SCRAP ALERT)
            _txtPostDia.BackColor = IndustrialTheme.CritBg;
            _lblLiveGuardrail.BackColor = IndustrialTheme.CritBg;
            _lblLiveGuardrail.ForeColor = IndustrialTheme.CritText;
            _lblLiveGuardrail.Font = IndustrialTheme.BadgeFont;
            _lblLiveGuardrail.Text = $"🚨 CHẠM NGƯỠNG PHẾ PHẨM (ĐK Min {roll.MinDiameter:0.00} mm): Trục sẽ bị khóa (Scrap) sau ca mài này! (Hao: {wear:F2} mm)";
            ApplyRolePermissions();
            return;
        }

        if (wear > _pokaYokeMaxWearMm)
        {
            // TẦNG 2: CẢNH BÁO GÕ NHẦM DẤU CHẤM / MÀI QUÁ SÂU (POKA-YOKE WARNING)
            _txtPostDia.BackColor = IndustrialTheme.WarnBg;
            _lblLiveGuardrail.BackColor = IndustrialTheme.WarnBg;
            _lblLiveGuardrail.ForeColor = IndustrialTheme.WarnText;
            _lblLiveGuardrail.Font = IndustrialTheme.BadgeFont;
            _lblLiveGuardrail.Text = $"⚠️ CẢNH BÁO: Lượng mài lên tới {wear:F2} mm (Cao bất thường). Hãy kiểm tra lại xem có gõ nhầm dấu chấm thập phân không!";
            ApplyRolePermissions();
            return;
        }

        // TRẠNG THÁI HỢP LỆ CHUẨN (SAFE)
        _txtPostDia.BackColor = IndustrialTheme.CardBackground;
        _lblLiveGuardrail.BackColor = IndustrialTheme.SafeBg;
        _lblLiveGuardrail.ForeColor = IndustrialTheme.SafeText;
        _lblLiveGuardrail.Font = IndustrialTheme.BadgeFont;

        if (roll != null)
        {
            decimal newPercent = RollCalculator.CalculateRemainingLifetimePercent(postDia, roll.MaxDiameter, roll.MinDiameter);
            _lblLiveGuardrail.Text = $"✓ Hợp lệ — Tiêu hao ca này: {wear:0.00} mm  |  Lifetime sau mài: {newPercent:0.0}% (Còn dư: {postDia - roll.MinDiameter:0.00} mm)";
        }
        else
        {
            _lblLiveGuardrail.Text = $"✓ Hợp lệ — Tiêu hao ca này: {wear:0.00} mm";
        }

        ApplyRolePermissions();
    }

    private void ShowToastBanner(string message)
    {
        _lblToastMessage.Text = message;
        _pnlToastBanner.Visible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void OnSaveNewClick(object? sender, EventArgs e)
    {
        if (!ValidateAndConfirmPokaYoke(out string rollCode, out decimal preDia, out decimal postDia, out bool isAbnormal, out string? remarks))
        {
            return;
        }

        try
        {
            decimal wear = preDia - postDia;
            GrindingLogEntity added = _rollService.AddGrindingLog(rollCode, _dtpGrindDate.Value, postDia, isAbnormal, remarks);

            // Triết lý Linear: Hiện Toast Banner màu xanh trong 3 giây (Không chặn bằng MessageBox),
            // cuộn bảng tới dòng vừa thêm và trả con trỏ về cboRollCode sẵn sàng cho cây trục tiếp theo!
            ResetInputForm(focusRollSelector: false);
            RefreshData();
            HighlightAndScrollToLogRow(added.Id);

            ShowToastBanner($"✅ Đã lưu ca mài trục {rollCode}: ĐK {preDia:0.00} ➔ {postDia:0.00} mm (Tiêu hao: {wear:0.00} mm) — Sẵn sàng nhập trục tiếp theo!");

            _cboRollCode.Focus();
            _cboRollCode.SelectAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi ghi ca mài: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnUpdateSelectedClick(object? sender, EventArgs e)
    {
        if (!_selectedLogId.HasValue)
        {
            MessageBox.Show("Vui lòng chọn một dòng nhật ký mài cần sửa từ bảng bên dưới.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!ValidateAndConfirmPokaYoke(out string rollCode, out decimal preDia, out decimal postDia, out bool isAbnormal, out string? remarks))
        {
            return;
        }

        try
        {
            int editedId = _selectedLogId.Value;
            _rollService.UpdateGrindingLog(editedId, _dtpGrindDate.Value, postDia, isAbnormal, remarks);

            ResetInputForm(focusRollSelector: false);
            RefreshData();
            HighlightAndScrollToLogRow(editedId);

            ShowToastBanner($"✅ Đã cập nhật ca mài #{editedId} của trục {rollCode} ({preDia:0.00} ➔ {postDia:0.00} mm) & tự động nối lại chuỗi đường kính!");
            _cboRollCode.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi cập nhật ca mài: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnDeleteSelectedClick(object? sender, EventArgs e)
    {
        if (!_selectedLogId.HasValue)
        {
            MessageBox.Show("Vui lòng chọn một dòng nhật ký mài cần xóa từ bảng bên dưới.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult dr = MessageBox.Show(
            "Bạn có chắc chắn muốn xóa dòng nhật ký mài này không?\nChuỗi đường kính các ca mài tiếp theo sẽ được tự động tính toán nối đuôi lại.",
            "Xác nhận xóa",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (dr != DialogResult.Yes) return;

        try
        {
            int deletedId = _selectedLogId.Value;
            _rollService.DeleteGrindingLog(deletedId, "Xóa từ giao diện");
            ResetInputForm(focusRollSelector: true);
            RefreshData();
            ShowToastBanner($"✅ Đã xóa ca mài #{deletedId} và tự động nối lại chuỗi đường kính thành công!");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi xóa ca mài: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void HighlightAndScrollToLogRow(int logId)
    {
        _suppressSelectionChanged = true;
        try
        {
            foreach (DataGridViewRow row in _dgvLogs.Rows)
            {
                if (row.DataBoundItem is GrindingLogViewDto dto && dto.Id == logId)
                {
                    row.Selected = true;
                    _dgvLogs.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Index);
                    break;
                }
            }
        }
        catch
        {
            // Bỏ qua nếu lưới đang ẩn
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
    }

    private bool ValidateAndConfirmPokaYoke(
        out string rollCode,
        out decimal preDia,
        out decimal postDia,
        out bool isAbnormal,
        out string? remarks)
    {
        rollCode = _cboRollCode.SelectedValue as string ?? _cboRollCode.Text.Trim().ToUpperInvariant();
        preDia = 0m;
        postDia = 0m;
        isAbnormal = _cboCondition.SelectedIndex == 1;
        remarks = string.IsNullOrWhiteSpace(_txtRemarks.Text) ? null : _txtRemarks.Text.Trim();

        if (string.IsNullOrEmpty(rollCode))
        {
            _lblLiveGuardrail.BackColor = IndustrialTheme.WarnBg;
            _lblLiveGuardrail.ForeColor = IndustrialTheme.WarnText;
            _lblLiveGuardrail.Text = "⚠️ Vui lòng chọn hoặc nhập mã trục trước khi lưu.";
            _cboRollCode.Focus();
            return false;
        }

        string preStr = _txtPreDia.Text.Trim().Replace(',', '.');
        string postStr = _txtPostDia.Text.Trim().Replace(',', '.');

        if (!decimal.TryParse(postStr, NumberStyles.Any, CultureInfo.InvariantCulture, out postDia) || postDia <= 0m)
        {
            _lblLiveGuardrail.BackColor = IndustrialTheme.WarnBg;
            _lblLiveGuardrail.ForeColor = IndustrialTheme.WarnText;
            _lblLiveGuardrail.Text = "⚠️ Vui lòng nhập đường kính sau mài hợp lệ (> 0).";
            _txtPostDia.Focus();
            return false;
        }

        if (!decimal.TryParse(preStr, NumberStyles.Any, CultureInfo.InvariantCulture, out preDia) || postDia >= preDia)
        {
            _lblLiveGuardrail.BackColor = IndustrialTheme.CritBg;
            _lblLiveGuardrail.ForeColor = IndustrialTheme.CritText;
            _lblLiveGuardrail.Text = $"⛔ LỖI: Đường kính sau mài ({postDia:0.00}) phải nhỏ hơn đường kính trước mài ({preDia:0.00})!";
            _txtPostDia.Focus();
            return false;
        }

        if (isAbnormal && string.IsNullOrEmpty(remarks))
        {
            _lblLiveGuardrail.BackColor = IndustrialTheme.WarnBg;
            _lblLiveGuardrail.ForeColor = IndustrialTheme.WarnText;
            _lblLiveGuardrail.Text = "⚠️ Ca mài bất thường/sự cố bắt buộc phải nhập ghi chú nguyên nhân lỗi!";
            _txtRemarks.Focus();
            return false;
        }

        decimal wear = preDia - postDia;
        RollEntity? roll = _rollService.GetRoll(rollCode);

        // Xác nhận kép Tầng 2 Poka-Yoke: Cảnh báo mài quá sâu / gõ nhầm dấu chấm thập phân
        if (wear > _pokaYokeMaxWearMm)
        {
            DialogResult confirmWear = MessageBox.Show(
                $"⚠️ CẢNH BÁO POKA-YOKE (MÀI QUÁ SÂU):\n\n" +
                $"• Mã trục: {rollCode}\n" +
                $"• Đường kính trước mài: {preDia:F2} mm\n" +
                $"• Đường kính sau mài vừa nhập: {postDia:F2} mm\n" +
                $"• Lượng mài hao hụt lên tới: {wear:F2} mm (Ngưỡng cảnh báo {_rollType}: > {_pokaYokeMaxWearMm:F1} mm)\n\n" +
                $"Hãy kiểm tra kỹ xem có gõ nhầm dấu chấm thập phân không!\n" +
                $"Bạn có chắc chắn thông số {postDia:F2} mm là chính xác và muốn lưu không?",
                "Xác Nhận Kép Poka-Yoke (Cảnh Báo Mài Sâu)",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirmWear != DialogResult.Yes)
            {
                _txtPostDia.Focus();
                _txtPostDia.SelectAll();
                return false;
            }
        }

        // Xác nhận kép Tầng 3: Chạm ngưỡng Phế phẩm (Scrap)
        if (roll != null && postDia <= roll.MinDiameter)
        {
            DialogResult confirmScrap = MessageBox.Show(
                $"🚨 CẢNH BÁO CHẠM NGƯỠNG PHẾ PHẨM (SCRAP):\n\n" +
                $"Đường kính sau mài ({postDia:F2} mm) đã chạm hoặc nhỏ hơn ĐK giới hạn phế phẩm ({roll.MinDiameter:F2} mm).\n" +
                $"Trục {rollCode} sẽ bị khóa sang trạng thái 'Hết đời (Scrap)' sau ca mài này!\n\n" +
                $"Bạn có chắc chắn muốn ghi nhận không?",
                "Xác Nhận Kép Chạm Ngưỡng Phế Phẩm (Scrap)",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirmScrap != DialogResult.Yes)
            {
                _txtPostDia.Focus();
                _txtPostDia.SelectAll();
                return false;
            }
        }

        return true;
    }

    private void ShowCurrentRollWearChart()
    {
        if (_cboRollCode.SelectedValue is not string rollCode || string.IsNullOrEmpty(rollCode))
        {
            MessageBox.Show("Vui lòng chọn một mã trục để xem biểu đồ đường cong hao mòn.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

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

    private void OnGridSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressSelectionChanged)
        {
            return;
        }

        if (_dgvLogs.CurrentRow?.DataBoundItem is not GrindingLogViewDto log)
        {
            return;
        }

        if (AppSession.IsAdmin)
        {
            _selectedLogId = log.Id;
            _suppressRollCodeChanged = true;
            try
            {
                _cboRollCode.SelectedValue = log.RollCode;
                _cboRollCode.Enabled = false;
            }
            finally
            {
                _suppressRollCodeChanged = false;
            }

            _dtpGrindDate.Value = log.GrindDate;
            _txtPreDia.Text = log.PreGrindDiameter.ToString("0.0##", CultureInfo.InvariantCulture);
            _txtPostDia.Text = log.PostGrindDiameter.ToString("0.0##", CultureInfo.InvariantCulture);
            _cboCondition.SelectedIndex = log.IsAbnormal ? 1 : 0;
            _txtRemarks.Text = log.Remarks ?? string.Empty;

            // Đồng bộ hiển thị Passport của trục đang chọn sửa
            try
            {
                RollLifetimeViewDto lifetime = _rollService.GetRollLifetime(log.RollCode);
                IReadOnlyList<GrindingLogViewDto> history = _rollService.GetGrindingLogViewsByRoll(log.RollCode);
                UpdatePassportCard(lifetime, history);
            }
            catch
            {
                // Bỏ qua nếu không tải được
            }

            ApplyRolePermissions();
        }
    }

    /// <summary>
    /// Triết lý ISA-101 Color by Exception: Dòng bình thường giữ nguyên nền trắng/xám dịu,
    /// chỉ tô nền hồng nhạt cho dòng ngoại lệ có sự cố (Abnormal).
    /// </summary>
    private void OnGridRowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _dgvLogs.Rows.Count) return;

        if (_dgvLogs.Rows[e.RowIndex].DataBoundItem is GrindingLogViewDto log && log.IsAbnormal)
        {
            _dgvLogs.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
        }
    }

    private void ResetInputForm(bool focusRollSelector)
    {
        _selectedLogId = null;
        _hasPhysicalInputError = false;
        _cboRollCode.Enabled = true;

        _suppressSelectionChanged = true;
        try
        {
            _dgvLogs.ClearSelection();
        }
        finally
        {
            _suppressSelectionChanged = false;
        }

        _dtpGrindDate.Value = DateTime.Now;
        _txtPostDia.Clear();
        _cboCondition.SelectedIndex = 0;
        _txtRemarks.Clear();

        ApplyRolePermissions();

        if (_cboRollCode.SelectedValue is string rollCode)
        {
            try
            {
                decimal preDia = _rollService.GetPreGrindDiameter(rollCode);
                _txtPreDia.Text = preDia.ToString("0.0##", CultureInfo.InvariantCulture);
            }
            catch
            {
                // Bỏ qua nếu chưa có trục
            }
        }

        if (focusRollSelector)
        {
            _cboRollCode.Focus();
            _cboRollCode.SelectAll();
        }
    }

    private void ApplyRolePermissions()
    {
        bool isAdmin = AppSession.IsAdmin;
        bool hasSelection = _selectedLogId.HasValue;

        _btnSaveNew.Enabled = !hasSelection && !_hasPhysicalInputError;
        _btnUpdateSelected.Enabled = isAdmin && hasSelection && !_hasPhysicalInputError;
        _btnDeleteSelected.Enabled = isAdmin && hasSelection;
    }
}
