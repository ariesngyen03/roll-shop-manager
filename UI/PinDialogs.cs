using RollShopManager.Database;
using RollShopManager.Models;
using RollShopManager.Security;

namespace RollShopManager.UI;

/// <summary>
/// Hộp thoại nhập mã PIN để mở khóa quyền Admin (chế độ bảo vệ bằng UseSystemPasswordChar).
/// </summary>
public sealed class PinPromptDialog : Form
{
    private readonly TextBox _txtPin;
    private readonly Label _lblError;
    private readonly Button _btnUnlock;
    private readonly Button _btnCancel;

    public PinPromptDialog()
    {
        Text = "Xác thực Quản lý";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 260);
        BackColor = IndustrialTheme.AppBackground;
        Font = IndustrialTheme.BodyFont;

        // Container chính dạng Card bo góc
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
            RowCount = 5,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Tiêu đề
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Hướng dẫn
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Ô nhập PIN
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Thông báo lỗi
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Nút bấm
        container.Controls.Add(layout);

        Label lblTitle = new()
        {
            Text = "MỞ KHÓA QUYỀN QUẢN LÝ",
            Font = IndustrialTheme.TitleFont,
            ForeColor = Color.FromArgb(15, 23, 42),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(lblTitle, 0, 0);

        Label lblInstruction = new()
        {
            Text = "Vui lòng nhập mã PIN xác thực quyền Quản lý:",
            Font = IndustrialTheme.BodyFont,
            ForeColor = IndustrialTheme.MutedText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(lblInstruction, 0, 1);

        _btnUnlock = new Button
        {
            Width = 110,
            Height = 38
        };
        IndustrialTheme.StylePrimaryButton(_btnUnlock, "Mở khóa");
        _btnUnlock.Click += OnUnlockClick;

        _btnCancel = new Button
        {
            Width = 90,
            Height = 38,
            DialogResult = DialogResult.Cancel
        };
        IndustrialTheme.StyleSecondaryButton(_btnCancel, "Đóng");

        _txtPin = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            MaxLength = 12,
            TextAlign = HorizontalAlignment.Center
        };
        IndustrialTheme.StyleInputBox(_txtPin);
        _txtPin.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _btnUnlock.PerformClick();
            }
        };
        layout.Controls.Add(_txtPin, 0, 2);

        _lblError = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = IndustrialTheme.CritText,
            Font = IndustrialTheme.SmallCaptionFont,
            Text = string.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(_lblError, 0, 3);

        FlowLayoutPanel btnPanel = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 0)
        };
        layout.Controls.Add(btnPanel, 0, 4);
        btnPanel.Controls.Add(_btnUnlock);
        btnPanel.Controls.Add(_btnCancel);

        AcceptButton = _btnUnlock;
        CancelButton = _btnCancel;

        Shown += (s, e) =>
        {
            _txtPin.Focus();
            CheckLockoutStatus();
        };
    }

    private void CheckLockoutStatus()
    {
        TimeSpan? lockout = AppSession.RemainingUnlockLockout;
        if (lockout.HasValue)
        {
            _lblError.Text = $"Tạm khóa mở khóa! Còn {lockout.Value.TotalSeconds:0}s.";
            _txtPin.Enabled = false;
            _btnUnlock.Enabled = false;
        }
    }

    private void OnUnlockClick(object? sender, EventArgs e)
    {
        string pin = _txtPin.Text.Trim();
        if (string.IsNullOrEmpty(pin))
        {
            _lblError.Text = "Vui lòng nhập mã PIN.";
            _txtPin.Focus();
            return;
        }

        UnlockResult result = AppSession.TryUnlockAdmin(pin);
        switch (result)
        {
            case UnlockResult.Success:
            case UnlockResult.AlreadyAdmin:
                DialogResult = DialogResult.OK;
                Close();
                break;

            case UnlockResult.InvalidFormat:
                _lblError.Text = "Mã PIN chỉ gồm 4 - 12 chữ số.";
                _txtPin.SelectAll();
                _txtPin.Focus();
                break;

            case UnlockResult.InvalidPin:
                int attempts = AppSession.FailedUnlockAttempts;
                int remaining = AppSession.MaxFailedUnlockAttempts - attempts;
                _lblError.Text = remaining > 0
                    ? $"PIN không đúng! Còn {remaining} lần thử trước khi bị khóa."
                    : "Nhập sai quá số lần quy định. Tạm khóa trong 60 giây.";
                _txtPin.SelectAll();
                _txtPin.Focus();
                CheckLockoutStatus();
                break;

            case UnlockResult.TemporarilyLockedOut:
                CheckLockoutStatus();
                break;
        }
    }
}

/// <summary>
/// Hộp thoại cho Admin: Đổi mã PIN quản lý và Sao lưu Cơ sở dữ liệu (.db).
/// </summary>
public sealed class ChangePinDialog : Form
{
    private readonly TextBox _txtCurrentPin;
    private readonly TextBox _txtNewPin;
    private readonly TextBox _txtConfirmPin;
    private readonly Label _lblStatus;
    private readonly Button _btnSavePin;
    private readonly Button _btnBackupDb;
    private readonly Button _btnRestoreDb;
    private readonly Button _btnClose;
    private readonly PinSecurityService _pinService;

    public bool DatabaseRestored { get; private set; }

    public ChangePinDialog()
    {
        _pinService = new PinSecurityService();

        Text = "Quản trị Hệ thống - Đổi PIN & Sao lưu / Phục hồi CSDL";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, 445);
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
            ColumnCount = 2,
            RowCount = 8,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Tiêu đề
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // PIN hiện tại
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // PIN mới
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Xác nhận
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Thông báo
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); // Nút Lưu PIN
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82)); // Mục Backup / Restore CSDL
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Nút Đóng
        container.Controls.Add(layout);

        Label lblTitle = new()
        {
            Text = "ĐỔI MÃ PIN QUẢN LÝ",
            Font = IndustrialTheme.TitleFont,
            ForeColor = Color.FromArgb(15, 23, 42),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.SetColumnSpan(lblTitle, 2);
        layout.Controls.Add(lblTitle, 0, 0);

        // Hàng 1: PIN hiện tại
        Label lblCur = new() { Text = "Mã PIN hiện tại:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        _txtCurrentPin = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = 12 };
        IndustrialTheme.StyleInputBox(_txtCurrentPin);
        layout.Controls.Add(lblCur, 0, 1);
        layout.Controls.Add(_txtCurrentPin, 1, 1);

        // Hàng 2: PIN mới
        Label lblNew = new() { Text = "Mã PIN mới:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        _txtNewPin = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = 12 };
        IndustrialTheme.StyleInputBox(_txtNewPin);
        layout.Controls.Add(lblNew, 0, 2);
        layout.Controls.Add(_txtNewPin, 1, 2);

        // Hàng 3: Xác nhận PIN mới
        Label lblConfirm = new() { Text = "Xác nhận PIN mới:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        _txtConfirmPin = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = 12 };
        IndustrialTheme.StyleInputBox(_txtConfirmPin);
        layout.Controls.Add(lblConfirm, 0, 3);
        layout.Controls.Add(_txtConfirmPin, 1, 3);

        // Hàng 4: Thông báo
        _lblStatus = new Label
        {
            Text = _pinService.IsUsingDefaultPin() ? "Lưu ý: Hệ thống đang dùng mã PIN mặc định, vui lòng đổi mã PIN mới." : string.Empty,
            ForeColor = _pinService.IsUsingDefaultPin() ? IndustrialTheme.WarnText : IndustrialTheme.MutedText,
            Font = IndustrialTheme.SmallCaptionFont,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.SetColumnSpan(_lblStatus, 2);
        layout.Controls.Add(_lblStatus, 0, 4);

        // Hàng 5: Nút Lưu PIN
        FlowLayoutPanel pnlSave = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.Transparent };
        _btnSavePin = new Button { Width = 140, Height = 38 };
        IndustrialTheme.StylePrimaryButton(_btnSavePin, "Cập nhật PIN");
        _btnSavePin.Click += OnSavePinClick;
        pnlSave.Controls.Add(_btnSavePin);
        layout.SetColumnSpan(pnlSave, 2);
        layout.Controls.Add(pnlSave, 0, 5);

        // Hàng 6: Khu vực Sao lưu & Phục hồi Database
        Panel pnlBackupSection = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0, 6, 0, 6)
        };
        TableLayoutPanel backupLayout = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        backupLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        backupLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        backupLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        backupLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pnlBackupSection.Controls.Add(backupLayout);

        Label lblBackupDesc = new()
        {
            Text = "An toàn dữ liệu SQLite Online Backup API (Sao lưu / Phục hồi .db):",
            Font = IndustrialTheme.SmallCaptionFont,
            ForeColor = IndustrialTheme.MutedText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        backupLayout.SetColumnSpan(lblBackupDesc, 2);
        backupLayout.Controls.Add(lblBackupDesc, 0, 0);

        _btnBackupDb = new Button { Dock = DockStyle.Fill, Height = 36, Margin = new Padding(0, 4, 6, 0) };
        IndustrialTheme.StyleOutlineButton(_btnBackupDb, "💾 Sao lưu Database", IndustrialTheme.PrimaryAccent);
        _btnBackupDb.Click += OnBackupDbClick;
        backupLayout.Controls.Add(_btnBackupDb, 0, 1);

        _btnRestoreDb = new Button { Dock = DockStyle.Fill, Height = 36, Margin = new Padding(6, 4, 0, 0) };
        IndustrialTheme.StyleOutlineButton(_btnRestoreDb, "🔄 Phục hồi từ file .db", IndustrialTheme.SecondarySlate);
        _btnRestoreDb.Click += OnRestoreDbClick;
        backupLayout.Controls.Add(_btnRestoreDb, 1, 1);

        layout.SetColumnSpan(pnlBackupSection, 2);
        layout.Controls.Add(pnlBackupSection, 0, 6);

        // Hàng 7: Nút Đóng
        FlowLayoutPanel pnlClose = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.Transparent };
        _btnClose = new Button { Width = 90, Height = 36, DialogResult = DialogResult.OK };
        IndustrialTheme.StyleSecondaryButton(_btnClose, "Đóng");
        pnlClose.Controls.Add(_btnClose);
        layout.SetColumnSpan(pnlClose, 2);
        layout.Controls.Add(pnlClose, 0, 7);
    }

    private void OnSavePinClick(object? sender, EventArgs e)
    {
        string currentPin = _txtCurrentPin.Text.Trim();
        string newPin = _txtNewPin.Text.Trim();
        string confirmPin = _txtConfirmPin.Text.Trim();

        try
        {
            _pinService.ChangePin(currentPin, newPin, confirmPin);
            _lblStatus.ForeColor = IndustrialTheme.SafeText;
            _lblStatus.Text = "✓ Đã cập nhật mã PIN quản lý thành công!";
            _txtCurrentPin.Clear();
            _txtNewPin.Clear();
            _txtConfirmPin.Clear();
        }
        catch (Exception ex)
        {
            _lblStatus.ForeColor = IndustrialTheme.CritText;
            _lblStatus.Text = ex.Message;
        }
    }

    private void OnBackupDbClick(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Chọn thư mục lưu file sao lưu CSDL RollShop.db",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                string backupPath = DatabaseMaintenance.BackupDatabase(dialog.SelectedPath);
                MessageBox.Show($"Sao lưu cơ sở dữ liệu thành công!\n\nĐường dẫn: {backupPath}", "Sao lưu hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi sao lưu cơ sở dữ liệu: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void OnRestoreDbClick(object? sender, EventArgs e)
    {
        using OpenFileDialog ofd = new()
        {
            Title = "Chọn file sao lưu CSDL RollShop (.db) để phục hồi",
            Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            DialogResult confirm = MessageBox.Show(
                $"Bạn có chắc chắn muốn phục hồi dữ liệu từ file:\n{ofd.FileName}\n\nDữ liệu hiện tại sẽ được thay thế bằng bản sao lưu này.",
                "Xác nhận phục hồi CSDL",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            try
            {
                DatabaseMaintenance.RestoreDatabase(ofd.FileName);
                DatabaseRestored = true;
                MessageBox.Show("Phục hồi cơ sở dữ liệu thành công!", "Phục hồi hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi phục hồi cơ sở dữ liệu: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
