using System.Globalization;
using RollShopManager.Models;
using RollShopManager.Services;

namespace RollShopManager.UI;

/// <summary>
/// Hộp thoại cấu hình thông số định mức đặt hàng trục cho 3 loại WR, IMR, BUR (chỉ Admin).
/// </summary>
public sealed class ProcurementConfigDialog : Form
{
    private readonly RollBusinessService _rollService;

    // WR
    private readonly TextBox _txtWrUsableMm;
    private readonly NumericUpDown _nudWrLeadTime;
    private readonly TextBox _txtWrSafetyStock;

    // IMR
    private readonly TextBox _txtImrUsableMm;
    private readonly NumericUpDown _nudImrLeadTime;
    private readonly TextBox _txtImrSafetyStock;

    // BUR
    private readonly TextBox _txtBurUsableMm;
    private readonly NumericUpDown _nudBurLeadTime;
    private readonly TextBox _txtBurSafetyStock;

    private readonly Button _btnSave;
    private readonly Button _btnCancel;

    public ProcurementConfigDialog()
    {
        _rollService = new RollBusinessService();

        Text = "Cấu Hình Định Mức Đặt Hàng Trục (WR - IMR - BUR)";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(620, 480);
        BackColor = IndustrialTheme.AppBackground;
        Font = IndustrialTheme.BodyFont;

        CardPanel container = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 20, 24, 20)
        };
        Controls.Add(container);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Tiêu đề
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Hướng dẫn
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // Card WR
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // Card IMR
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // Card BUR
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Nút bấm
        container.Controls.Add(layout);

        Label lblTitle = new()
        {
            Text = "CẤU HÌNH ĐỊNH MỨC VÀ DỰ BÁO ĐẶT HÀNG",
            Font = IndustrialTheme.TitleFont,
            ForeColor = Color.FromArgb(15, 23, 42),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(lblTitle, 0, 0);

        Label lblSubtitle = new()
        {
            Text = "Dùng để quy đổi tương đương số cây trục và tính ngày báo động đặt hàng:",
            Font = IndustrialTheme.SmallCaptionFont,
            ForeColor = IndustrialTheme.MutedText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(lblSubtitle, 0, 1);

        // Group WR
        layout.Controls.Add(CreateTypeSection("WORK ROLL (WR)", out _txtWrUsableMm, out _nudWrLeadTime, out _txtWrSafetyStock, 50m, 35, 2.0m), 0, 2);

        // Group IMR
        layout.Controls.Add(CreateTypeSection("INTERMEDIATE ROLL (IMR)", out _txtImrUsableMm, out _nudImrLeadTime, out _txtImrSafetyStock, 50m, 35, 2.0m), 0, 3);

        // Group BUR
        layout.Controls.Add(CreateTypeSection("BACKUP ROLL (BUR)", out _txtBurUsableMm, out _nudBurLeadTime, out _txtBurSafetyStock, 80m, 35, 2.0m), 0, 4);

        // Nút lưu / hủy
        FlowLayoutPanel pnlButtons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 10, 0, 0)
        };
        layout.Controls.Add(pnlButtons, 0, 5);

        _btnSave = new Button { Width = 150, Height = 38 };
        IndustrialTheme.StylePrimaryButton(_btnSave, "Lưu Cấu Hình");
        _btnSave.Click += OnSaveClick;
        pnlButtons.Controls.Add(_btnSave);

        _btnCancel = new Button { Width = 90, Height = 38, DialogResult = DialogResult.Cancel, Margin = new Padding(0, 0, 8, 0) };
        IndustrialTheme.StyleSecondaryButton(_btnCancel, "Hủy");
        pnlButtons.Controls.Add(_btnCancel);

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        LoadCurrentConfigs();
    }

    private static Panel CreateTypeSection(
        string title,
        out TextBox txtUsable,
        out NumericUpDown nudLeadTime,
        out TextBox txtSafety,
        decimal defUsable,
        int defLead,
        decimal defSafety)
    {
        Panel pnl = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(12, 8, 12, 8)
        };

        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pnl.Controls.Add(grid);

        Label lblHeader = new()
        {
            Text = title,
            Font = IndustrialTheme.BodyBoldFont,
            ForeColor = IndustrialTheme.PrimaryAccent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        grid.SetColumnSpan(lblHeader, 6);
        grid.Controls.Add(lblHeader, 0, 0);

        Label lblUsable = new() { Text = "Vỏ chuẩn (mm):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        txtUsable = new TextBox { Dock = DockStyle.Fill, Text = defUsable.ToString("0.0##", CultureInfo.InvariantCulture) };
        IndustrialTheme.StyleInputBox(txtUsable);

        Label lblLead = new() { Text = "Lead time (ngày):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        nudLeadTime = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 365,
            Value = defLead,
            Font = IndustrialTheme.BodyFont
        };

        Label lblSafety = new() { Text = "Tồn an toàn (cây):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        txtSafety = new TextBox { Dock = DockStyle.Fill, Text = defSafety.ToString("0.0##", CultureInfo.InvariantCulture) };
        IndustrialTheme.StyleInputBox(txtSafety);

        grid.Controls.Add(lblUsable, 0, 1);
        grid.Controls.Add(txtUsable, 1, 1);
        grid.Controls.Add(lblLead, 2, 1);
        grid.Controls.Add(nudLeadTime, 3, 1);
        grid.Controls.Add(lblSafety, 4, 1);
        grid.Controls.Add(txtSafety, 5, 1);

        return pnl;
    }

    private void LoadCurrentConfigs()
    {
        try
        {
            IReadOnlyList<ProcurementConfigEntity> configs = _rollService.GetProcurementConfigs();
            foreach (ProcurementConfigEntity cfg in configs)
            {
                switch (cfg.RollType)
                {
                    case RollTypes.WR:
                        _txtWrUsableMm.Text = cfg.StandardUsableMmPerRoll.ToString("0.0##", CultureInfo.InvariantCulture);
                        _nudWrLeadTime.Value = Math.Clamp(cfg.LeadTimeDays, 0, 365);
                        _txtWrSafetyStock.Text = cfg.SafetyStockRolls.ToString("0.0##", CultureInfo.InvariantCulture);
                        break;
                    case RollTypes.IMR:
                        _txtImrUsableMm.Text = cfg.StandardUsableMmPerRoll.ToString("0.0##", CultureInfo.InvariantCulture);
                        _nudImrLeadTime.Value = Math.Clamp(cfg.LeadTimeDays, 0, 365);
                        _txtImrSafetyStock.Text = cfg.SafetyStockRolls.ToString("0.0##", CultureInfo.InvariantCulture);
                        break;
                    case RollTypes.BUR:
                        _txtBurUsableMm.Text = cfg.StandardUsableMmPerRoll.ToString("0.0##", CultureInfo.InvariantCulture);
                        _nudBurLeadTime.Value = Math.Clamp(cfg.LeadTimeDays, 0, 365);
                        _txtBurSafetyStock.Text = cfg.SafetyStockRolls.ToString("0.0##", CultureInfo.InvariantCulture);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải cấu hình đặt hàng: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        if (!ParseSection(_txtWrUsableMm, _nudWrLeadTime, _txtWrSafetyStock, RollTypes.WR, out var wrConfig) ||
            !ParseSection(_txtImrUsableMm, _nudImrLeadTime, _txtImrSafetyStock, RollTypes.IMR, out var imrConfig) ||
            !ParseSection(_txtBurUsableMm, _nudBurLeadTime, _txtBurSafetyStock, RollTypes.BUR, out var burConfig))
        {
            return;
        }

        try
        {
            _rollService.UpdateProcurementConfig(wrConfig);
            _rollService.UpdateProcurementConfig(imrConfig);
            _rollService.UpdateProcurementConfig(burConfig);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi lưu cấu hình: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool ParseSection(TextBox txtUsable, NumericUpDown nudLead, TextBox txtSafety, string rollType, out ProcurementConfigEntity config)
    {
        config = new ProcurementConfigEntity { RollType = rollType };

        string usableStr = txtUsable.Text.Trim().Replace(',', '.');
        if (!decimal.TryParse(usableStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal usable) || usable <= 0m)
        {
            MessageBox.Show($"Độ dày vỏ chuẩn 1 cây mới của {rollType} phải là số dương (> 0).", "Sai thông số", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtUsable.Focus();
            return false;
        }

        string safetyStr = txtSafety.Text.Trim().Replace(',', '.');
        if (!decimal.TryParse(safetyStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal safety) || safety < 0m)
        {
            MessageBox.Show($"Tồn kho an toàn của {rollType} không được âm (>= 0).", "Sai thông số", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtSafety.Focus();
            return false;
        }

        config.StandardUsableMmPerRoll = usable;
        config.LeadTimeDays = (int)nudLead.Value;
        config.SafetyStockRolls = safety;
        return true;
    }
}
