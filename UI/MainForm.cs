using RollShopManager.Database;
using RollShopManager.Models;
using RollShopManager.Security;
using RollShopManager.Services;

namespace RollShopManager.UI;

/// <summary>
/// Cửa sổ chính của ứng dụng RollShopManager:
/// - Header công nghiệp với bảng trạng thái phân quyền Operator/Admin &amp; Đếm ngược tự động khóa.
/// - Hệ thống 6 Tab điều hướng (QUAN_LY_TRUC, MAI_WR, MAI_IMR, MAI_BUR, BAO_CAO, NHAT_KY_HE_THONG).
/// - Tự động Refresh dữ liệu khi chuyển Tab.
/// </summary>
public sealed class MainForm : Form
{
    private readonly Panel _pnlHeader;
    private readonly Label _lblAppTitle;
    private readonly Label _lblSessionStatus;
    private readonly Button _btnLockToggle;
    private readonly Button _btnAdminSettings;

    private readonly TabControl _mainTabControl;
    private readonly RollMasterTabControl _tabRollMaster;
    private readonly GrindingTabControl _tabGrindWr;
    private readonly GrindingTabControl _tabGrindImr;
    private readonly GrindingTabControl _tabGrindBur;
    private readonly ReportAndForecastTabControl _tabReports;
    private readonly AuditLogTabControl _tabAuditLogs;

    private readonly AutoLockMonitor _autoLockMonitor;

    public MainForm()
    {
        Text = "Hệ thống Quản lý Xưởng Mài Trục Cán";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        KeyPreview = true;
        BackColor = IndustrialTheme.AppBackground;
        Font = IndustrialTheme.BodyFont;

        // Bố cục tổng thể: Header cố định phía trên (Dock = Top), TabControl lấp đầy bên dưới (Dock = Fill)
        _pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = IndustrialTheme.HeaderDark,
            Padding = new Padding(20, 10, 20, 10)
        };
        Controls.Add(_pnlHeader);

        TableLayoutPanel headerLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _pnlHeader.Controls.Add(headerLayout);

        // Bên trái Header: Tên phần mềm chuẩn MES Công nghiệp (UseMnemonic = false để không biến dấu & thành gạch chân _)
        _lblAppTitle = new Label
        {
            Text = "HỆ THỐNG QUẢN LÝ TRỤC CÁN NGUỘI",
            Font = IndustrialTheme.TitleFont,
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
            AutoEllipsis = true
        };
        headerLayout.Controls.Add(_lblAppTitle, 0, 0);

        // Bên phải Header: Badge phân quyền & Nút chức năng
        FlowLayoutPanel pnlSecurityControls = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        headerLayout.Controls.Add(pnlSecurityControls, 1, 0);

        _btnAdminSettings = new Button
        {
            Width = 160,
            Height = 36,
            Margin = new Padding(8, 4, 0, 0),
            Visible = false
        };
        IndustrialTheme.StyleSecondaryButton(_btnAdminSettings, "⚙️ Đổi PIN / Backup");
        _btnAdminSettings.Click += OnAdminSettingsClick;
        pnlSecurityControls.Controls.Add(_btnAdminSettings);

        _btnLockToggle = new Button
        {
            Width = 175,
            Height = 36,
            Margin = new Padding(8, 4, 0, 0)
        };
        _btnLockToggle.Click += OnLockToggleClick;
        pnlSecurityControls.Controls.Add(_btnLockToggle);

        _lblSessionStatus = new Label
        {
            AutoSize = true,
            Font = IndustrialTheme.BodyBoldFont,
            ForeColor = Color.White,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0, 4, 4, 0)
        };
        pnlSecurityControls.Controls.Add(_lblSessionStatus);

        // =====================================================================
        // TAB CONTROL 6 PHÂN HỆ (CUSTOM SCADA TABS)
        // =====================================================================
        _mainTabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };
        IndustrialTheme.StyleTabControl(_mainTabControl);
        _mainTabControl.SelectedIndexChanged += OnTabSelectedIndexChanged;
        Controls.Add(_mainTabControl);

        // Tab 1: QUẢN LÝ TRỤC
        TabPage tab1 = new("[F1] QUẢN LÝ TRỤC") { BackColor = IndustrialTheme.AppBackground };
        _tabRollMaster = new RollMasterTabControl { Dock = DockStyle.Fill };
        tab1.Controls.Add(_tabRollMaster);
        _mainTabControl.TabPages.Add(tab1);

        // Tab 2: MÀI WR
        TabPage tab2 = new("[F2] MÀI WR") { BackColor = IndustrialTheme.AppBackground };
        _tabGrindWr = new GrindingTabControl(RollTypes.WR) { Dock = DockStyle.Fill };
        tab2.Controls.Add(_tabGrindWr);
        _mainTabControl.TabPages.Add(tab2);

        // Tab 3: MÀI IMR (Đưa IMR lên F3 theo thứ tự công nghệ WR -> IMR -> BUR)
        TabPage tab3 = new("[F3] MÀI IMR") { BackColor = IndustrialTheme.AppBackground };
        _tabGrindImr = new GrindingTabControl(RollTypes.IMR) { Dock = DockStyle.Fill };
        tab3.Controls.Add(_tabGrindImr);
        _mainTabControl.TabPages.Add(tab3);

        // Tab 4: MÀI BUR
        TabPage tab4 = new("[F4] MÀI BUR") { BackColor = IndustrialTheme.AppBackground };
        _tabGrindBur = new GrindingTabControl(RollTypes.BUR) { Dock = DockStyle.Fill };
        tab4.Controls.Add(_tabGrindBur);
        _mainTabControl.TabPages.Add(tab4);

        // Tab 5: BÁO CÁO & DỰ BÁO ĐẶT HÀNG
        TabPage tab5 = new("[F5] BÁO CÁO & DỰ BÁO") { BackColor = IndustrialTheme.AppBackground };
        _tabReports = new ReportAndForecastTabControl { Dock = DockStyle.Fill };
        tab5.Controls.Add(_tabReports);
        _mainTabControl.TabPages.Add(tab5);

        // Tab 6: NHẬT KÝ HỆ THỐNG (AUDIT LOGS & BẢO TRÌ)
        TabPage tab6 = new("[F6] NHẬT KÝ HỆ THỐNG") { BackColor = IndustrialTheme.AppBackground };
        _tabAuditLogs = new AuditLogTabControl { Dock = DockStyle.Fill };
        tab6.Controls.Add(_tabAuditLogs);
        _mainTabControl.TabPages.Add(tab6);

        // Quan trọng trong WinForms: Đưa _mainTabControl (Dock = Fill) lên đầu Z-order (Controls[0])
        // để _pnlHeader (Dock = Top) được neo trước ở cạnh trên, phần còn lại bên dưới mới dành cho _mainTabControl.
        _mainTabControl.BringToFront();

        // =====================================================================
        // GIÁM SÁT TỰ ĐỘNG KHÓA (3 PHÚT KHÔNG THAO TÁC)
        // =====================================================================
        _autoLockMonitor = new AutoLockMonitor(1000);
        _autoLockMonitor.CountdownTick += OnAutoLockCountdownTick;
        _autoLockMonitor.AutoLocked += OnAutoLocked;

        AppSession.RoleChanged += OnRoleChanged;

        Shown += (s, e) =>
        {
            _autoLockMonitor.Start();
            UpdateSecurityHeader();
            RefreshCurrentTab();
        };

        FormClosing += (s, e) =>
        {
            _autoLockMonitor.Dispose();
            AppSession.Lock(RoleChangeReason.ApplicationShutdown);
        };
    }

    private void OnTabSelectedIndexChanged(object? sender, EventArgs e)
    {
        RefreshCurrentTab();
    }

    private void RefreshCurrentTab()
    {
        // Khi chuyển tab, tự động refresh tab đó để dữ liệu luôn mới nhất
        switch (_mainTabControl.SelectedIndex)
        {
            case 0:
                _tabRollMaster.RefreshData();
                break;
            case 1:
                _tabGrindWr.RefreshData();
                break;
            case 2:
                _tabGrindImr.RefreshData();
                break;
            case 3:
                _tabGrindBur.RefreshData();
                break;
            case 4:
                _tabReports.RefreshData();
                break;
            case 5:
                _tabAuditLogs.RefreshData();
                break;
        }
    }

    private void OnRoleChanged(object? sender, RoleChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnRoleChanged(sender, e)));
            return;
        }

        UpdateSecurityHeader();
    }

    private void UpdateSecurityHeader()
    {
        if (AppSession.IsAdmin)
        {
            TimeSpan? remaining = AppSession.TimeUntilAutoLock;
            string timeStr = remaining.HasValue ? $"{remaining.Value.Minutes:D2}:{remaining.Value.Seconds:D2}" : "03:00";

            _lblSessionStatus.Text = $"🔓 QUẢN LÝ (Tự khóa: {timeStr})";
            _lblSessionStatus.BackColor = Color.FromArgb(180, 83, 9); // Amber-700
            IndustrialTheme.StylePrimaryButton(_btnLockToggle, "[F9] Khóa Quyền Ngay", IndustrialTheme.DangerAccent);
            _btnAdminSettings.Visible = true;
        }
        else
        {
            _lblSessionStatus.Text = "🔒 NHÂN VIÊN (Chỉ nhập ca mài)";
            _lblSessionStatus.BackColor = Color.FromArgb(51, 65, 85); // Slate-700
            IndustrialTheme.StylePrimaryButton(_btnLockToggle, "[F9] Mở Khóa Quản Lý", IndustrialTheme.PrimaryAccent);
            _btnAdminSettings.Visible = false;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.KeyCode)
        {
            case Keys.F1:
                _mainTabControl.SelectedIndex = 0;
                e.Handled = true;
                break;
            case Keys.F2:
                _mainTabControl.SelectedIndex = 1;
                e.Handled = true;
                break;
            case Keys.F3:
                _mainTabControl.SelectedIndex = 2;
                e.Handled = true;
                break;
            case Keys.F4:
                _mainTabControl.SelectedIndex = 3;
                e.Handled = true;
                break;
            case Keys.F5:
                _mainTabControl.SelectedIndex = 4;
                e.Handled = true;
                break;
            case Keys.F6:
                _mainTabControl.SelectedIndex = 5;
                e.Handled = true;
                break;
            case Keys.F9:
                OnLockToggleClick(this, EventArgs.Empty);
                e.Handled = true;
                break;
        }
    }

    private void OnAutoLockCountdownTick(object? sender, TimeSpan remaining)
    {
        if (AppSession.IsAdmin)
        {
            _lblSessionStatus.Text = $"🔓 QUẢN LÝ (Tự khóa: {remaining.Minutes:D2}:{remaining.Seconds:D2})";
        }
    }

    private void OnAutoLocked(object? sender, EventArgs e)
    {
        UpdateSecurityHeader();
    }

    private void OnLockToggleClick(object? sender, EventArgs e)
    {
        if (AppSession.IsAdmin)
        {
            AppSession.Lock(RoleChangeReason.ManualLock);
        }
        else
        {
            using PinPromptDialog dialog = new();
            dialog.ShowDialog(this);
        }
    }

    private void OnAdminSettingsClick(object? sender, EventArgs e)
    {
        using ChangePinDialog dialog = new();
        dialog.ShowDialog(this);
        if (dialog.DatabaseRestored)
        {
            RefreshCurrentTab();
        }
    }
}
