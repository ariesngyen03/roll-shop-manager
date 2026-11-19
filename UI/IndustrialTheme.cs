using System.Drawing.Drawing2D;
using System.Reflection;
using RollShopManager.Models;

namespace RollShopManager.UI;

/// <summary>
/// Hệ thống Màu sắc, Typography và Quy tắc Trang trí Chuẩn Công nghiệp (MES / SCADA Standard).
/// Tuân thủ chuẩn ISA-101 HMI:
/// - Chống nháy hình tuyệt đối (DoubleBuffered).
/// - Tất cả các dòng chẵn &amp; lẻ trong bảng có cùng chuẩn căn lề (Số căn phải, Mã/Ngày căn giữa, Ghi chú căn trái).
/// - Khi phóng to / thu nhỏ cửa sổ, các cột và nhãn co giãn mượt mà, không nhảy dòng.
/// </summary>
public static class IndustrialTheme
{
    // =========================================================================
    // 1. SURFACES & BACKGROUNDS (BẢNG MÀU NỀN & BỀ MẶT SLATE - ISA-101)
    // =========================================================================

    public static readonly Color AppBackground = Color.FromArgb(241, 245, 249);
    public static readonly Color CardBackground = Color.FromArgb(255, 255, 255);
    public static readonly Color HeaderDark = Color.FromArgb(15, 23, 42);
    public static readonly Color HoaSenBrandRed = Color.FromArgb(139, 30, 30);
    public static readonly Color GridHeaderDark = Color.FromArgb(30, 41, 59);
    public static readonly Color BorderLight = Color.FromArgb(203, 213, 225);
    public static readonly Color GridZebraRow = Color.FromArgb(248, 250, 252);
    public static readonly Color InputBorder = Color.FromArgb(203, 213, 225);
    public static readonly Color InputBorderFocused = Color.FromArgb(59, 130, 246);

    // =========================================================================
    // 2. ACTIONS & BUTTONS (MÀU NÚT BẤM & TƯƠNG TÁC)
    // =========================================================================

    public static readonly Color PrimaryAccent = Color.FromArgb(37, 99, 235);
    public static readonly Color PrimaryAccentHover = Color.FromArgb(29, 78, 216);

    public static readonly Color SecondarySlate = Color.FromArgb(71, 85, 105);
    public static readonly Color SecondarySlateHover = Color.FromArgb(51, 65, 85);

    public static readonly Color DangerAccent = Color.FromArgb(185, 28, 28);
    public static readonly Color DangerAccentHover = Color.FromArgb(153, 27, 27);

    public static readonly Color SuccessAccent = Color.FromArgb(5, 150, 105);
    public static readonly Color SuccessAccentHover = Color.FromArgb(4, 120, 87);

    public static readonly Color GridSelectionBg = Color.FromArgb(219, 234, 254);
    public static readonly Color GridSelectionFg = Color.FromArgb(30, 58, 138);

    // =========================================================================
    // 3. SEMANTIC STATUS PALETTE (NGỮ NGHĨA TRẠNG THÁI)
    // =========================================================================

    public static readonly Color SafeBg = Color.FromArgb(220, 252, 231);
    public static readonly Color SafeText = Color.FromArgb(22, 101, 52);
    public static readonly Color SafeBar = Color.FromArgb(22, 163, 74);

    public static readonly Color WarnBg = Color.FromArgb(254, 243, 199);
    public static readonly Color WarnText = Color.FromArgb(146, 64, 14);
    public static readonly Color WarnBar = Color.FromArgb(217, 119, 6);

    public static readonly Color CritBg = Color.FromArgb(254, 226, 226);
    public static readonly Color CritText = Color.FromArgb(153, 27, 27);
    public static readonly Color CritBar = Color.FromArgb(220, 38, 38);

    public static readonly Color MutedBg = Color.FromArgb(241, 245, 249);
    public static readonly Color MutedText = Color.FromArgb(71, 85, 105);

    // =========================================================================
    // 4. TYPOGRAPHY (FONT SEGOE UI CHUẨN CÔNG NGHIỆP)
    // =========================================================================

    public static readonly Font TitleFont = new("Segoe UI", 12.5F, FontStyle.Bold);
    public static readonly Font SubTitleFont = new("Segoe UI", 10F, FontStyle.Bold);
    public static readonly Font CardValueFont = new("Segoe UI", 16F, FontStyle.Bold);
    public static readonly Font CardTitleFont = new("Segoe UI", 8.75F, FontStyle.Bold);
    public static readonly Font BodyFont = new("Segoe UI", 9.5F, FontStyle.Regular);
    public static readonly Font BodyBoldFont = new("Segoe UI", 9.5F, FontStyle.Bold);
    public static readonly Font HeaderGridFont = new("Segoe UI", 9.25F, FontStyle.Bold);
    public static readonly Font BadgeFont = new("Segoe UI", 8.75F, FontStyle.Bold);
    public static readonly Font SmallCaptionFont = new("Segoe UI", 8.5F, FontStyle.Regular);

    // =========================================================================
    // 5. HIỆN ĐẠI HÓA TABCONTROL CHUẨN SCADA
    // =========================================================================

    public static void StyleTabControl(TabControl tabControl)
    {
        ArgumentNullException.ThrowIfNull(tabControl);

        tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabControl.SizeMode = TabSizeMode.Fixed;
        tabControl.ItemSize = new Size(195, 38);
        tabControl.Font = BodyBoldFont;

        tabControl.DrawItem -= OnTabControlDrawItem;
        tabControl.DrawItem += OnTabControlDrawItem;
    }

    private static void OnTabControlDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tc || e.Index < 0 || e.Index >= tc.TabPages.Count)
        {
            return;
        }

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Rectangle tabRect = tc.GetTabRect(e.Index);
        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        string title = tc.TabPages[e.Index].Text.Trim();

        Color bgColor = isSelected ? CardBackground : Color.FromArgb(226, 232, 240);
        Color textColor = isSelected ? PrimaryAccent : Color.FromArgb(51, 65, 85);

        using (SolidBrush bgBrush = new(bgColor))
        {
            g.FillRectangle(bgBrush, tabRect);
        }

        if (isSelected)
        {
            using SolidBrush accentBrush = new(PrimaryAccent);
            g.FillRectangle(accentBrush, tabRect.X, tabRect.Y, tabRect.Width, 3);
        }
        else
        {
            using Pen sepPen = new(Color.FromArgb(203, 213, 225), 1f);
            g.DrawLine(sepPen, tabRect.Right - 1, tabRect.Top + 6, tabRect.Right - 1, tabRect.Bottom - 6);
        }

        using SolidBrush textBrush = new(textColor);
        using StringFormat sf = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };
        g.DrawString(title, BodyBoldFont, textBrush, tabRect, sf);
    }

    // =========================================================================
    // 6. HIỆN ĐẠI HÓA DATAGRIDVIEW (CĂN LỀ THẲNG TẮP MỌI DÒNG + CO GIÃN MƯỢT)
    // =========================================================================

    public static void StyleDataGridView(DataGridView dgv)
    {
        ArgumentNullException.ThrowIfNull(dgv);

        EnableDoubleBuffer(dgv);

        dgv.BackgroundColor = CardBackground;
        dgv.BorderStyle = BorderStyle.None;
        dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        dgv.GridColor = Color.FromArgb(226, 232, 240);
        dgv.RowHeadersVisible = false;
        dgv.MultiSelect = false;
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.AutoGenerateColumns = false;
        dgv.AllowUserToAddRows = false;
        dgv.AllowUserToDeleteRows = false;
        dgv.AllowUserToResizeRows = false;
        dgv.ReadOnly = true;

        // Cho phép toàn bộ cột co giãn tỷ lệ mượt mà theo kích thước cửa sổ
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgv.ShowCellToolTips = true;

        // Header phong cách Slate-800 công nghiệp, cho phép xuống dòng 2 hàng gọn gàng để không bao giờ bị khuyết chữ tiêu đề
        dgv.EnableHeadersVisualStyles = false;
        dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        dgv.ColumnHeadersHeight = 46;
        dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = GridHeaderDark,
            ForeColor = Color.White,
            SelectionBackColor = GridHeaderDark,
            SelectionForeColor = Color.White,
            Font = HeaderGridFont,
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            Padding = new Padding(2, 2, 2, 2),
            WrapMode = DataGridViewTriState.True
        };

        // Dòng dữ liệu chuẩn (cao 40px thoáng mắt, Padding gọn để không lẹm chữ)
        dgv.RowTemplate.Height = 40;
        dgv.RowTemplate.MinimumHeight = 36;
        dgv.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = CardBackground,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = BodyFont,
            SelectionBackColor = GridSelectionBg,
            SelectionForeColor = GridSelectionFg,
            Padding = new Padding(3, 1, 3, 1),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            WrapMode = DataGridViewTriState.False
        };

        // QUAN TRỌNG: KHÔNG gán Alignment vào AlternatingRowsDefaultCellStyle!
        // Nếu gán Alignment ở đây, WinForms sẽ ghi đè Alignment của Column ở các dòng lẻ khiến chữ nhảy trái-phải!
        dgv.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = GridZebraRow,
            ForeColor = Color.FromArgb(15, 23, 42),
            SelectionBackColor = GridSelectionBg,
            SelectionForeColor = GridSelectionFg
        };

        ApplySmartColumnAlignments(dgv);

        dgv.CellPainting -= OnDataGridViewCellPainting;
        dgv.CellPainting += OnDataGridViewCellPainting;
    }

    /// <summary>
    /// Thiết lập căn lề và tỷ lệ co giãn (FillWeight) thông minh cho từng cột:
    /// - Cột số thực (mm, tấn, tỷ lệ): Căn phải (MiddleRight), đồng bộ 2 chữ số thập phân để dấu chấm thẳng hàng dọc.
    /// - Cột Mã, Loại, Ngày, Lần mài: Căn giữa (MiddleCenter).
    /// - Cột Ghi chú / Chi tiết: Căn trái (MiddleLeft) với WrapMode = True và FillWeight lớn hơn.
    /// </summary>
    public static void ApplySmartColumnAlignments(DataGridView dgv)
    {
        foreach (DataGridViewColumn col in dgv.Columns)
        {
            string name = (col.Name ?? string.Empty).ToLowerInvariant();
            string header = (col.HeaderText ?? string.Empty).ToLowerInvariant();
            string format = col.DefaultCellStyle.Format ?? string.Empty;

            // Tối ưu tỷ lệ co giãn FillWeight từ Width thiết kế ban đầu
            if (col.Width > 0)
            {
                col.FillWeight = Math.Max(45f, col.Width);
                col.MinimumWidth = Math.Min(col.Width, 58);
            }

            if (IsLifetimePercentColumn(name, header))
            {
                col.FillWeight = 145f;
                col.MinimumWidth = 125;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            else if (IsStatusBadgeColumn(name, header) || col is DataGridViewButtonColumn)
            {
                col.FillWeight = Math.Max(135f, col.Width);
                col.MinimumWidth = 115;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            else if (name.Equals("runwaydays", StringComparison.OrdinalIgnoreCase))
            {
                col.FillWeight = 150f;
                col.MinimumWidth = 125;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            else if (name.Contains("note") || name.Contains("remark") || name.Contains("detail") ||
                     header.Contains("ghi chú") || header.Contains("chi tiết"))
            {
                col.FillWeight = 235f;
                col.MinimumWidth = 150;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                col.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            }
            else if (!string.IsNullOrEmpty(format) && (format.Contains('0') || format.Contains('#')))
            {
                // Chuẩn hóa định dạng số thập phân 0.00 để hàng đơn vị và phần thập phân luôn thẳng cột dọc
                if (format == "0.0##")
                {
                    col.DefaultCellStyle.Format = "0.00";
                }

                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                col.DefaultCellStyle.Padding = new Padding(2, 0, 5, 0);
            }
            else if (name.Contains("count") || name.Contains("seq") || name.Contains("days") ||
                     name.Contains("grinds") || name == "id" || name.Contains("code") ||
                     name.Contains("type") || name.Contains("date") || name.Contains("time"))
            {
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }
    }

    private static void OnDataGridViewCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics is null || sender is not DataGridView dgv)
        {
            return;
        }

        DataGridViewColumn column = dgv.Columns[e.ColumnIndex];
        string colName = column.Name ?? string.Empty;
        string headerText = column.HeaderText ?? string.Empty;

        // 1. VẼ PROGRESS BAR CHO CỘT % TUỔI THỌ CÒN LẠI
        if (IsLifetimePercentColumn(colName, headerText))
        {
            if (e.Value != null && decimal.TryParse(e.Value.ToString(), out decimal percent))
            {
                e.Handled = true;
                e.PaintBackground(e.CellBounds, (e.State & DataGridViewElementStates.Selected) != 0);
                DrawProgressBarCell(e.Graphics, e.CellBounds, percent, (e.State & DataGridViewElementStates.Selected) != 0);
                return;
            }
        }

        // 2. VẼ PILL BADGE CHO CỘT TRẠNG THÁI / CẢNH BÁO
        if (IsStatusBadgeColumn(colName, headerText))
        {
            string? text = e.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                e.Handled = true;
                e.PaintBackground(e.CellBounds, (e.State & DataGridViewElementStates.Selected) != 0);
                DrawPillBadgeCell(e.Graphics, e.CellBounds, text.Trim());
                return;
            }
        }

        // 3. VẼ ĐƯỜNG BĂNG TỒN KHO (RUNWAY VS LEAD-TIME - PALANTIR SUPPLY CHAIN STYLE)
        if (colName.Equals("RunwayDays", StringComparison.OrdinalIgnoreCase) &&
            dgv.Rows[e.RowIndex].DataBoundItem is TypeConsumptionAndForecastDto forecastDto)
        {
            e.Handled = true;
            e.PaintBackground(e.CellBounds, (e.State & DataGridViewElementStates.Selected) != 0);
            DrawRunwayVsLeadTimeCell(
                e.Graphics,
                e.CellBounds,
                forecastDto.RunwayDays,
                forecastDto.LeadTimeDays,
                forecastDto.RollType == "TỔNG CỘNG");
            return;
        }
    }

    private static bool IsLifetimePercentColumn(string name, string header)
    {
        string n = name.ToLowerInvariant();
        string h = header.ToLowerInvariant();
        if (n.Contains("trend") || n.Contains("mom") || h.Contains("mom") || h.Contains("xu hướng"))
        {
            return false;
        }
        return n.Contains("lifetime") || n.Contains("percent") || h.Contains("%") || h.Contains("tuổi thọ");
    }

    private static bool IsStatusBadgeColumn(string name, string header)
    {
        string n = name.ToLowerInvariant();
        string h = header.ToLowerInvariant();
        return n.Contains("status") || n.Contains("condition") || n.Contains("alert") || n.Contains("actiontype") ||
               h.Contains("trạng thái") || h.Contains("tình trạng") || h.Contains("cảnh báo");
    }

    /// <summary>
    /// Vẽ thanh Progress Bar chuẩn ISA-101 HMI (Side-by-Side Layout):
    /// - Bên trái: Thanh tiến độ bo góc trực quan.
    /// - Bên phải: Con số % được đo động theo DPI hiện tại và vẽ bằng TextRenderer (NoPadding),
    ///   đảm bảo chữ số % thẳng hàng dọc tuyệt đối và KHÔNG BAO GIỜ bị cắt mất dấu %!
    /// </summary>
    public static void DrawProgressBarCell(Graphics g, Rectangle bounds, decimal percent, bool isSelected)
    {
        percent = Math.Clamp(percent, 0m, 100m);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Color barColor = percent > 30m ? SafeBar : (percent >= 15m ? WarnBar : CritBar);
        Color textColor = percent > 30m ? SafeText : (percent >= 15m ? WarnText : CritText);
        Color trackColor = Color.FromArgb(226, 232, 240);

        Size maxPercentSize = TextRenderer.MeasureText(g, "100.0%", BadgeFont, new Size(200, 40), TextFormatFlags.NoPadding);
        int textAreaWidth = Math.Max(54, maxPercentSize.Width + 6);

        int marginX = 6;
        int barHeight = 12;
        int barY = bounds.Y + (bounds.Height - barHeight) / 2;
        int barWidth = Math.Max(18, bounds.Width - marginX * 2 - textAreaWidth - 4);

        Rectangle trackRect = new(bounds.X + marginX, barY, barWidth, barHeight);
        int cornerRadius = barHeight / 2;

        // 1. Vẽ nền Track
        using (GraphicsPath trackPath = CreateRoundedRectanglePath(trackRect, cornerRadius))
        using (SolidBrush trackBrush = new(trackColor))
        {
            g.FillPath(trackBrush, trackPath);
        }

        // 2. Vẽ phần Fill
        int fillWidth = (int)Math.Round(trackRect.Width * (float)(percent / 100m));
        if (fillWidth > 2)
        {
            Rectangle fillRect = new(trackRect.X, trackRect.Y, fillWidth, trackRect.Height);
            using GraphicsPath fillPath = CreateRoundedRectanglePath(fillRect, cornerRadius);
            using SolidBrush fillBrush = new(barColor);
            g.FillPath(fillBrush, fillPath);
        }

        // 3. Vẽ con số % căn phải bên cạnh thanh bằng TextRenderer (Không bao giờ bị cắt mất dấu %)
        Rectangle textRect = new(trackRect.Right + 3, bounds.Y, textAreaWidth, bounds.Height);
        string percentText = percent.ToString("0.0") + "%";
        TextRenderer.DrawText(
            g,
            percentText,
            BadgeFont,
            textRect,
            textColor,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }

    /// <summary>
    /// Triết lý Palantir Supply Chain: Vẽ thanh trực quan hóa Đường băng Tồn kho (RunwayDays)
    /// so với vạch mốc Thời gian chờ hàng về (LeadTimeDays).
    /// Sử dụng tông nền Pastel tương phản cao để chữ màu Xanh Đen (#0F172A) đồng nhất 100%, không bị chia 2 màu.
    /// </summary>
    public static void DrawRunwayVsLeadTimeCell(Graphics g, Rectangle bounds, int? runwayDays, int leadTimeDays, bool isTotalRow)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (!runwayDays.HasValue)
        {
            TextRenderer.DrawText(
                g,
                "Chưa phát sinh",
                SmallCaptionFont,
                bounds,
                MutedText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            return;
        }

        int runway = Math.Max(0, runwayDays.Value);
        int lead = Math.Max(1, leadTimeDays);
        int maxScale = Math.Max(90, Math.Max(lead * 2, Math.Min(180, runway + 15)));

        int marginX = 6;
        int marginY = 7;
        Rectangle trackRect = new(
            bounds.X + marginX,
            bounds.Y + marginY,
            Math.Max(24, bounds.Width - marginX * 2),
            Math.Max(12, bounds.Height - marginY * 2));

        int cornerRadius = Math.Min(4, trackRect.Height / 2);

        int bufferDays = runway - lead;
        Color fillPastel = bufferDays <= 0
            ? Color.FromArgb(252, 165, 165) // Red-300
            : (bufferDays <= 15 ? Color.FromArgb(253, 224, 71) : Color.FromArgb(134, 239, 172)); // Amber-300 : Green-300
        Color borderAccent = bufferDays <= 0 ? CritBar : (bufferDays <= 15 ? WarnBar : SafeBar);

        using (GraphicsPath trackPath = CreateRoundedRectanglePath(trackRect, cornerRadius))
        using (SolidBrush trackBrush = new(Color.FromArgb(241, 245, 249)))
        using (Pen trackPen = new(Color.FromArgb(203, 213, 225), 1f))
        {
            g.FillPath(trackBrush, trackPath);
            g.DrawPath(trackPen, trackPath);
        }

        float ratio = Math.Clamp((float)runway / maxScale, 0f, 1f);
        int fillWidth = (int)Math.Round(trackRect.Width * ratio);
        if (fillWidth > 2)
        {
            Rectangle fillRect = new(trackRect.X, trackRect.Y, fillWidth, trackRect.Height);
            using GraphicsPath fillPath = CreateRoundedRectanglePath(fillRect, cornerRadius);
            using SolidBrush fillBrush = new(fillPastel);
            using Pen fillPen = new(borderAccent, 1f);
            g.FillPath(fillBrush, fillPath);
            g.DrawPath(fillPen, fillPath);
        }

        // Vạch mốc LeadTime
        float leadRatio = Math.Clamp((float)lead / maxScale, 0.08f, 0.92f);
        int leadX = trackRect.X + (int)Math.Round(trackRect.Width * leadRatio);
        using (Pen leadMarkerPen = new(Color.FromArgb(185, 28, 28), 2f))
        {
            g.DrawLine(leadMarkerPen, leadX, trackRect.Top - 2, leadX, trackRect.Bottom + 2);
        }

        string fullLabel = $"{runway}d (LT:{lead}d)";
        Size fullLabelSize = TextRenderer.MeasureText(g, fullLabel, BadgeFont, new Size(300, 40), TextFormatFlags.NoPadding);
        string displayLabel = fullLabelSize.Width + 6 <= trackRect.Width ? fullLabel : $"{runway} ngày";

        TextRenderer.DrawText(
            g,
            displayLabel,
            BadgeFont,
            trackRect,
            Color.FromArgb(15, 23, 42),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }

    /// <summary>
    /// Triết lý Herkules RSMS: Vẽ trực quan mặt cắt Lớp vỏ hợp kim (Alloy Shell Cross-Section).
    /// Sử dụng nền Pastel có viền đậm để dòng chữ ở giữa luôn có màu Xanh Đen (#0F172A) đồng nhất,
    /// và 3 mốc thông số bên dưới cố định ở Trái - Giữa - Phải.
    /// </summary>
    public static void DrawAlloyShellBar(Graphics g, Rectangle bounds, decimal maxDia, decimal minDia, decimal currentDia)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (maxDia <= minDia || bounds.Width < 40 || bounds.Height < 18)
        {
            return;
        }

        decimal usableSpan = maxDia - minDia;
        decimal remainingMm = Math.Clamp(currentDia - minDia, 0m, usableSpan);
        decimal percent = Math.Round((remainingMm / usableSpan) * 100m, 1);

        int barHeight = Math.Min(22, Math.Max(16, bounds.Height - 22));
        Rectangle barRect = new(bounds.X, bounds.Y + 2, bounds.Width - 1, barHeight);
        int radius = 5;

        // 1. Nền toàn bộ lớp vỏ (Phần đã mài mòn từ Current -> Max: kẻ sọc chéo xám nhạt)
        using (GraphicsPath fullPath = CreateRoundedRectanglePath(barRect, radius))
        using (HatchBrush wornBrush = new(HatchStyle.ForwardDiagonal, Color.FromArgb(203, 213, 225), Color.FromArgb(241, 245, 249)))
        using (Pen borderPen = new(Color.FromArgb(148, 163, 184), 1f))
        {
            g.FillPath(wornBrush, fullPath);
            g.DrawPath(borderPen, fullPath);
        }

        // 2. Phần lớp vỏ hợp kim còn lại (Từ Min -> Current) dùng Pastel + Viền đậm
        Color fillPastel = percent > 30m
            ? Color.FromArgb(134, 239, 172)
            : (percent >= 15m ? Color.FromArgb(253, 224, 71) : Color.FromArgb(252, 165, 165));
        Color strokeColor = percent > 30m ? SafeBar : (percent >= 15m ? WarnBar : CritBar);

        int fillWidth = (int)Math.Round(barRect.Width * (float)(percent / 100m));
        if (fillWidth > 2)
        {
            Rectangle fillRect = new(barRect.X, barRect.Y, fillWidth, barRect.Height);
            using GraphicsPath fillPath = CreateRoundedRectanglePath(fillRect, radius);
            using SolidBrush shellBrush = new(fillPastel);
            using Pen shellPen = new(strokeColor, 1.2f);
            g.FillPath(shellBrush, fillPath);
            g.DrawPath(shellPen, fillPath);
        }

        // 3. Vạch ranh giới ĐK Hiện tại
        if (fillWidth > 4 && fillWidth < barRect.Width - 4)
        {
            using Pen curPen = new(Color.FromArgb(15, 23, 42), 2f);
            g.DrawLine(curPen, barRect.X + fillWidth, barRect.Top, barRect.X + fillWidth, barRect.Bottom);
        }

        // 4. Text căn giữa trên thanh màu Xanh Đen đồng nhất 100%
        string centerText = $"Vỏ hợp kim còn: {remainingMm:0.00} / {usableSpan:0.00} mm ({percent:0.0}%)";
        using (SolidBrush txtBrush = new(Color.FromArgb(15, 23, 42)))
        using (StringFormat centerTextSf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
        {
            g.DrawString(centerText, BadgeFont, txtBrush, barRect, centerTextSf);
        }

        // 5. Thước đo 3 mốc cố định Trái - Giữa - Phải (Không bao giờ nhảy chữ khi phóng to/thu nhỏ)
        Rectangle footerRect = new(barRect.Left, barRect.Bottom + 3, barRect.Width, 18);
        using SolidBrush minBrush = new(CritText);
        using SolidBrush curBrush = new(PrimaryAccent);
        using SolidBrush maxBrush = new(MutedText);

        using StringFormat leftSf = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        using StringFormat centerSf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        using StringFormat rightSf = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

        g.DrawString($"Scrap (Min): {minDia:0.00} mm", SmallCaptionFont, minBrush, footerRect, leftSf);
        g.DrawString($"ĐK Hiện tại: {currentDia:0.00} mm", SmallCaptionFont, curBrush, footerRect, centerSf);
        g.DrawString($"Gốc (Max): {maxDia:0.00} mm", SmallCaptionFont, maxBrush, footerRect, rightSf);
    }

    public static void DrawPillBadgeCell(Graphics g, Rectangle bounds, string text)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        (Color bg, Color fg) = ResolveStatusColors(text);

        int maxAvailableWidth = Math.Max(30, bounds.Width - 8);
        string displayText = GetAdaptiveBadgeText(g, text, BadgeFont, maxAvailableWidth - 14);
        Size textSize = TextRenderer.MeasureText(g, displayText, BadgeFont, new Size(500, 40), TextFormatFlags.NoPadding);

        int badgeWidth = Math.Min(maxAvailableWidth, textSize.Width + 14);
        int badgeHeight = Math.Min(24, bounds.Height - 10);

        int badgeX = bounds.X + (bounds.Width - badgeWidth) / 2;
        int badgeY = bounds.Y + (bounds.Height - badgeHeight) / 2;
        Rectangle badgeRect = new(badgeX, badgeY, Math.Max(20, badgeWidth), Math.Max(16, badgeHeight));

        int cornerRadius = badgeRect.Height / 2;

        using (GraphicsPath path = CreateRoundedRectanglePath(badgeRect, cornerRadius))
        using (SolidBrush brush = new(bg))
        using (Pen pen = new(Color.FromArgb(60, fg), 1f))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        TextRenderer.DrawText(
            g,
            displayText,
            BadgeFont,
            badgeRect,
            fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }

    private static string GetAdaptiveBadgeText(Graphics g, string text, Font font, int maxTextWidth)
    {
        Size fullSize = TextRenderer.MeasureText(g, text, font, new Size(500, 40), TextFormatFlags.NoPadding);
        if (fullSize.Width <= maxTextWidth)
        {
            return text;
        }

        // Nếu cột hẹp hơn chuỗi có ngoặc đơn dài, tự động rút gọn thông minh để hiển thị trọn vẹn không bị cắt dấu "..."
        string s = text.Trim();
        if (s.Contains("NGUY CẤP", StringComparison.OrdinalIgnoreCase))
        {
            string med = "NGUY CẤP - ĐẶT NGAY";
            if (TextRenderer.MeasureText(g, med, font, new Size(500, 40), TextFormatFlags.NoPadding).Width <= maxTextWidth)
            {
                return med;
            }
            return "NGUY CẤP";
        }

        if (s.Contains("CẢNH BÁO", StringComparison.OrdinalIgnoreCase))
        {
            string med = "CẢNH BÁO ĐẶT HÀNG";
            if (TextRenderer.MeasureText(g, med, font, new Size(500, 40), TextFormatFlags.NoPadding).Width <= maxTextWidth)
            {
                return med;
            }
            return "CẢNH BÁO";
        }

        if (s.Contains("Cần kiểm tra", StringComparison.OrdinalIgnoreCase))
        {
            return "Cần kiểm tra";
        }

        if (s.Contains("Hết đời", StringComparison.OrdinalIgnoreCase))
        {
            return "Hết đời (Scrap)";
        }

        int parenIdx = s.IndexOf('(');
        if (parenIdx > 2)
        {
            return s[..parenIdx].Trim();
        }

        return s;
    }

    public static (Color Bg, Color Fg) ResolveStatusColors(string statusText)
    {
        string s = statusText.Trim().ToUpperInvariant();

        if (s.Contains("NGUY CẤP") || s.Contains("SCRAP") || s.Contains("HẾT ĐỜI") || s.Contains("ĐẶT HÀNG NGAY") ||
            s.Contains("DELETE") || s.Contains("FAILED") || s.Contains("LOCKOUT"))
        {
            return (CritBg, CritText);
        }

        if (s.Contains("CẢNH BÁO") || s.Contains("BẤT THƯỜNG") || s.Contains("KIỂM TRA") || s.Contains("SẮP ĐẾN HẠN") ||
            s.Contains("UPDATE") || s.Contains("CHANGE") || s.Contains("RECALCULATE") || s.Contains("RESTORE") || s.Contains("AUTO_LOCK"))
        {
            return (WarnBg, WarnText);
        }

        if (s.Contains("AN TOÀN") || s.Contains("BÌNH THƯỜNG") || s.Contains("ĐANG SỬ DỤNG") || s.Contains("HOẠT ĐỘNG") ||
            s.Contains("CREATE") || s.Contains("ADD") || s.Contains("UNLOCK") || s.Contains("BACKUP") || s.Contains("SEED") || s.Contains("INIT"))
        {
            return (SafeBg, SafeText);
        }

        return (MutedBg, MutedText);
    }

    public static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        GraphicsPath path = new();
        if (radius <= 0 || rect.Width <= 0 || rect.Height <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        int diameter = radius * 2;
        if (diameter > rect.Width) diameter = rect.Width;
        if (diameter > rect.Height) diameter = rect.Height;

        Rectangle arc = new(rect.X, rect.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    // =========================================================================
    // 7. TRANG TRÍ CONTROL & CĂN CHỈNH THẲNG HÀNG (FORM CONTROLS)
    // =========================================================================

    public static Label CreateFormLabel(string text, bool bold = false, Color? color = null, int leftMargin = 0)
    {
        return new Label
        {
            Text = text,
            Font = bold ? BodyBoldFont : BodyFont,
            ForeColor = color ?? Color.FromArgb(51, 65, 85),
            Dock = DockStyle.Fill,
            AutoSize = false,
            UseMnemonic = false,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Margin = new Padding(leftMargin, 0, 4, 0)
        };
    }

    public static void StylePrimaryButton(Button btn, string text, Color? customBg = null)
    {
        Color baseBg = customBg ?? PrimaryAccent;
        btn.Text = text;
        btn.Font = BodyBoldFont;
        btn.ForeColor = Color.White;
        btn.BackColor = baseBg;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = customBg.HasValue ? ControlPaint.Light(customBg.Value, 0.12f) : PrimaryAccentHover;
        btn.FlatAppearance.MouseDownBackColor = customBg.HasValue ? ControlPaint.Dark(customBg.Value, 0.12f) : PrimaryAccent;
        btn.Cursor = Cursors.Hand;
        btn.TextAlign = ContentAlignment.MiddleCenter;
        btn.UseVisualStyleBackColor = false;
        if (btn.Height < 30) btn.Height = 34;
    }

    public static void StyleSecondaryButton(Button btn, string text)
    {
        btn.Text = text;
        btn.Font = BodyBoldFont;
        btn.ForeColor = Color.White;
        btn.BackColor = SecondarySlate;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = SecondarySlateHover;
        btn.Cursor = Cursors.Hand;
        btn.TextAlign = ContentAlignment.MiddleCenter;
        btn.UseVisualStyleBackColor = false;
        if (btn.Height < 30) btn.Height = 34;
    }

    public static void StyleOutlineButton(Button btn, string text, Color accent)
    {
        btn.Text = text;
        btn.Font = BodyBoldFont;
        btn.ForeColor = accent;
        btn.BackColor = CardBackground;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = Color.FromArgb(148, 163, 184);
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
        btn.Cursor = Cursors.Hand;
        btn.TextAlign = ContentAlignment.MiddleCenter;
        btn.UseVisualStyleBackColor = false;
        if (btn.Height < 30) btn.Height = 34;
    }

    public static void StyleInputBox(TextBox txt)
    {
        txt.Font = BodyFont;
        txt.BorderStyle = BorderStyle.FixedSingle;
        txt.BackColor = CardBackground;
        txt.ForeColor = Color.FromArgb(15, 23, 42);
        if (txt.Dock == DockStyle.Fill)
        {
            txt.Dock = DockStyle.None;
            txt.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        }
        txt.Margin = new Padding(0, 4, 8, 4);
    }

    public static void StyleComboBox(ComboBox cbo)
    {
        cbo.Font = BodyFont;
        cbo.DropDownStyle = ComboBoxStyle.DropDownList;
        cbo.FlatStyle = FlatStyle.Standard;
        cbo.BackColor = CardBackground;
        cbo.ForeColor = Color.FromArgb(15, 23, 42);
        if (cbo.Dock == DockStyle.Fill)
        {
            cbo.Dock = DockStyle.None;
            cbo.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        }
        cbo.Margin = new Padding(0, 4, 8, 4);
    }

    public static void StyleDateTimePicker(DateTimePicker dtp, string customFormat = "dd/MM/yyyy")
    {
        dtp.Font = BodyFont;
        dtp.Format = DateTimePickerFormat.Custom;
        dtp.CustomFormat = customFormat;
        if (dtp.Dock == DockStyle.Fill)
        {
            dtp.Dock = DockStyle.None;
            dtp.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        }
        dtp.Margin = new Padding(0, 4, 8, 4);
    }

    public static void StyleNumericUpDown(NumericUpDown nud)
    {
        nud.Font = BodyFont;
        nud.BorderStyle = BorderStyle.FixedSingle;
        if (nud.Dock == DockStyle.Fill)
        {
            nud.Dock = DockStyle.None;
            nud.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        }
        nud.Margin = new Padding(0, 4, 8, 4);
    }

    public static void EnableDoubleBuffer(Control control)
    {
        PropertyInfo? prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance);
        prop?.SetValue(control, true, null);
    }
}
