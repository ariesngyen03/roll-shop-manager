using System.Drawing.Drawing2D;
using RollShopManager.Models;
using RollShopManager.Services;

namespace RollShopManager.UI;

/// <summary>
/// Hộp thoại trực quan hóa Đường cong suy giảm đường kính &amp; Dự phóng tuổi thọ trục (GDI+ SCADA Chart).
/// Đã tối ưu chống đè chữ tuyệt đối khi phóng to / thu nhỏ cửa sổ (Anti-Collision Layout).
/// </summary>
public sealed class RollWearChartDialog : Form
{
    private readonly RollLifetimeViewDto _roll;
    private readonly IReadOnlyList<GrindingLogViewDto> _logs;
    private readonly WearChartCanvas _chartCanvas;

    public RollWearChartDialog(RollLifetimeViewDto roll, IReadOnlyList<GrindingLogViewDto> logs)
    {
        _roll = roll ?? throw new ArgumentNullException(nameof(roll));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));

        Text = $"Biểu Đồ Đường Cong Hao Mòn & Lý Lịch Trục - {_roll.RollCode} ({_roll.RollType})";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 640);
        ClientSize = new Size(1080, 680);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        BackColor = IndustrialTheme.AppBackground;
        Font = IndustrialTheme.BodyFont;

        TableLayoutPanel rootLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = Color.Transparent
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // Hàng 1: 4 Thẻ KPI của trục
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Hàng 2: Biểu đồ GDI+
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));  // Hàng 3: Nút xuất Passport & Đóng
        Controls.Add(rootLayout);

        // =====================================================================
        // HÀNG 1: 4 THẺ KPI THÔNG SỐ ĐỜI TRỤC
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

        kpiGrid.Controls.Add(new KpiCardPanel
        {
            CardTitle = $"MÃ TRỤC ({_roll.RollType})",
            CardValue = _roll.RollCode,
            CardSubText = $"Gốc: {_roll.MaxDiameter:0.00} mm | Scrap: {_roll.MinDiameter:0.00} mm",
            AccentColor = IndustrialTheme.PrimaryAccent,
            Margin = new Padding(0, 0, 6, 0)
        }, 0, 0);

        kpiGrid.Controls.Add(new KpiCardPanel
        {
            CardTitle = "ĐƯỜNG KÍNH HIỆN TẠI",
            CardValue = $"{_roll.CurrentDiameter:0.00} mm",
            CardSubText = $"Lượng dư còn: {_roll.RemainingUsableMm:0.00} / {_roll.UsableLayerMm:0.00} mm",
            AccentColor = IndustrialTheme.SecondarySlate,
            Margin = new Padding(6, 0, 6, 0)
        }, 1, 0);

        Color lifeAccent = _roll.RemainingLifetimePercent > 30m
            ? IndustrialTheme.SafeBar
            : (_roll.RemainingLifetimePercent >= 15m ? IndustrialTheme.WarnBar : IndustrialTheme.CritBar);

        kpiGrid.Controls.Add(new KpiCardPanel
        {
            CardTitle = "% LIFETIME CÒN LẠI",
            CardValue = $"{_roll.RemainingLifetimePercent:0.0} %",
            CardSubText = $"Trạng thái: {_roll.Status}",
            AccentColor = lifeAccent,
            Margin = new Padding(6, 0, 6, 0)
        }, 2, 0);

        int abnormalCount = _logs.Count(l => l.IsAbnormal);
        string wearSubText = _roll.LoggedGrindCount > 0
            ? $"Đã mài: {_roll.LoggedGrindCount} ca ({abnormalCount} sự cố) | TB: {_roll.AverageWearPerGrind:0.00} mm/ca"
            : $"ĐK hiện trạng đầu: {_roll.InitialDiameter:0.00} mm (Chưa phát sinh ca mài)";

        kpiGrid.Controls.Add(new KpiCardPanel
        {
            CardTitle = "TỐC ĐỘ HAO MÒN & DỰ BÁO",
            CardValue = _roll.EstimatedRemainingGrindsText,
            CardSubText = wearSubText,
            AccentColor = abnormalCount > 0 ? IndustrialTheme.WarnBar : IndustrialTheme.SafeBar,
            Margin = new Padding(6, 0, 0, 0)
        }, 3, 0);

        // =====================================================================
        // HÀNG 2: KHUNG VẼ BIỂU ĐỒ ĐƯỜNG CONG HAO MÒN
        // =====================================================================
        CardPanel chartCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10)
        };
        rootLayout.Controls.Add(chartCard, 0, 1);

        _chartCanvas = new WearChartCanvas(_roll, _logs)
        {
            Dock = DockStyle.Fill
        };
        chartCard.Controls.Add(_chartCanvas);

        // =====================================================================
        // HÀNG 3: THANH ĐIỀU KHIỂN & XUẤT PASSPORT PDF
        // =====================================================================
        FlowLayoutPanel bottomBar = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        rootLayout.Controls.Add(bottomBar, 0, 2);

        Button btnClose = new() { Width = 105, Height = 36, DialogResult = DialogResult.OK, Margin = new Padding(8, 2, 0, 0) };
        IndustrialTheme.StyleSecondaryButton(btnClose, "Đóng");
        bottomBar.Controls.Add(btnClose);

        Button btnExportPdf = new() { Width = 225, Height = 36, Margin = new Padding(0, 2, 0, 0) };
        IndustrialTheme.StylePrimaryButton(btnExportPdf, "📄 Xuất Lý Lịch Trục (PDF)", IndustrialTheme.PrimaryAccent);
        btnExportPdf.Click += OnExportPassportClick;
        bottomBar.Controls.Add(btnExportPdf);

        CancelButton = btnClose;
    }

    private void OnExportPassportClick(object? sender, EventArgs e)
    {
        try
        {
            using SaveFileDialog sfd = new()
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"LyLichTruc_{_roll.RollCode}_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                ExportService.ExportRollPassportToPdf(sfd.FileName, _roll, _logs);
                DialogResult openNow = MessageBox.Show(
                    $"Xuất Phiếu Lý Lịch Trục ({_roll.RollCode}) thành công!\n\nĐường dẫn: {sfd.FileName}\n\nBạn có muốn mở file PDF ngay không?",
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

    /// <summary>
    /// Canvas vẽ đồ thị đường cong suy giảm đường kính theo chuẩn SCADA:
    /// - Tách riêng hàng Tiêu đề và Chú giải để không bao giờ đè lên nhau.
    /// - Dành khoảng đệm đỉnh (Top Headroom 8%) để điểm 450.00mm nằm gọn trong khung đồ thị.
    /// - Chia tỷ lệ 72% chiều ngang cho lịch sử thực tế và 28% cho đường dự phóng tương lai.
    /// </summary>
    private sealed class WearChartCanvas : Panel
    {
        private readonly RollLifetimeViewDto _roll;
        private readonly IReadOnlyList<GrindingLogViewDto> _logs;

        public WearChartCanvas(RollLifetimeViewDto roll, IReadOnlyList<GrindingLogViewDto> logs)
        {
            _roll = roll;
            _logs = logs.OrderBy(l => l.GrindDate).ThenBy(l => l.Id).ToList();

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int padLeft = 82;
            int padRight = 32;
            int padTop = 48;
            int padBottom = 56;

            Rectangle plotArea = new(
                padLeft,
                padTop,
                Math.Max(120, Width - padLeft - padRight),
                Math.Max(100, Height - padTop - padBottom));

            // 1. Tiêu đề bên trái & Chú giải bên phải (Tách biệt hoàn toàn, không bao giờ đè nhau)
            using (SolidBrush titleBrush = new(Color.FromArgb(15, 23, 42)))
            {
                g.DrawString(
                    $"ĐƯỜNG CONG HAO MÒN TRỤC {_roll.RollCode} ({_roll.RollType}) & DỰ PHÓNG CHẠM NGƯỠNG SCRAP",
                    IndustrialTheme.BodyBoldFont,
                    titleBrush,
                    new PointF(padLeft, 10));
            }

            DrawLegendRightAligned(g, plotArea.Right, 12);

            decimal maxDia = _roll.MaxDiameter;
            decimal minDia = _roll.MinDiameter;
            decimal usableSpan = Math.Max(1m, maxDia - minDia);
            // Tạo khoảng đệm 8% phía trên MaxDiameter để nhãn số 450.00 nằm lọt bên trong khung biểu đồ
            decimal chartTopDia = maxDia + usableSpan * 0.08m;

            float yTop = plotArea.Top;
            float yMaxLine = ValueToY(maxDia, minDia, chartTopDia, plotArea);
            float y30 = ValueToY(minDia + usableSpan * 0.30m, minDia, chartTopDia, plotArea);
            float y15 = ValueToY(minDia + usableSpan * 0.15m, minDia, chartTopDia, plotArea);
            float yMin = plotArea.Bottom;

            // 2. Vẽ 3 dải màu nền theo ngưỡng % Lifetime (>30%, 15%-30%, <15%)
            using (SolidBrush safeZone = new(Color.FromArgb(16, 34, 197, 94)))
            using (SolidBrush warnZone = new(Color.FromArgb(24, 245, 158, 11)))
            using (SolidBrush critZone = new(Color.FromArgb(28, 239, 68, 68)))
            {
                g.FillRectangle(safeZone, plotArea.Left, yMaxLine, plotArea.Width, Math.Max(1, y30 - yMaxLine));
                g.FillRectangle(warnZone, plotArea.Left, y30, plotArea.Width, Math.Max(1, y15 - y30));
                g.FillRectangle(critZone, plotArea.Left, y15, plotArea.Width, Math.Max(1, yMin - y15));
            }

            // 3. Vẽ lưới ngang (Y-Axis: Đường kính mm từ Min đến Max)
            int gridSteps = 5;
            using (Pen gridPen = new(Color.FromArgb(226, 232, 240), 1f) { DashStyle = DashStyle.Dot })
            using (SolidBrush axisBrush = new(Color.FromArgb(71, 85, 105)))
            using (StringFormat rightAlign = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                for (int i = 0; i <= gridSteps; i++)
                {
                    decimal val = minDia + (usableSpan * (gridSteps - i) / gridSteps);
                    float y = ValueToY(val, minDia, chartTopDia, plotArea);
                    g.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
                    g.DrawString($"{val:0.0} mm", IndustrialTheme.SmallCaptionFont, axisBrush, new RectangleF(4, y - 10, padLeft - 10, 20), rightAlign);
                }
            }

            // 4. Vẽ đường giới hạn ĐK Phế phẩm (MinDiameter) ở đáy khung
            using (Pen scrapPen = new(IndustrialTheme.DangerAccent, 2f) { DashStyle = DashStyle.Dash })
            using (SolidBrush scrapBrush = new(IndustrialTheme.DangerAccent))
            {
                g.DrawLine(scrapPen, plotArea.Left, plotArea.Bottom, plotArea.Right, plotArea.Bottom);
                g.DrawString($"Ngưỡng Phế phẩm (Scrap): {minDia:0.00} mm (0%)", IndustrialTheme.SmallCaptionFont, scrapBrush, new PointF(plotArea.Left + 8, plotArea.Bottom - 18));
            }

            // 5. Chuẩn bị danh sách điểm thực tế: Điểm 0 (Khởi tạo) + Các lần mài
            var points = new List<(string Label, decimal Dia, bool IsAbnormal, decimal Wear, DateTime? Date)>
            {
                ("Khởi tạo", _roll.InitialDiameter, false, 0m, _roll.CommissionDate)
            };

            foreach (GrindingLogViewDto log in _logs)
            {
                points.Add(($"Lần {log.GrindSequence}", log.PostGrindDiameter, log.IsAbnormal, log.GrindConsumption, log.GrindDate));
            }

            bool hasProjection = !_roll.IsScrap && _roll.EstimatedRemainingGrinds > 0 && points.Count > 1;
            float historyWidthRatio = hasProjection ? 0.68f : 0.94f;
            float historySpanWidth = plotArea.Width * historyWidthRatio;
            float leftOffset = plotArea.Left + 24;

            var screenPts = new List<PointF>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                float x = points.Count == 1
                    ? leftOffset
                    : leftOffset + ((historySpanWidth - 24) * i / (float)(points.Count - 1));
                float y = ValueToY(points[i].Dia, minDia, chartTopDia, plotArea);
                screenPts.Add(new PointF(x, y));
            }

            // 6. Vẽ đường thực tế nối các lần mài
            if (screenPts.Count >= 2)
            {
                using Pen linePen = new(IndustrialTheme.PrimaryAccent, 2.6f);
                g.DrawLines(linePen, screenPts.ToArray());
            }

            // 7. Vẽ đường dự phóng tương lai (nét đứt màu cam) từ điểm cuối tới ngưỡng Scrap
            if (hasProjection && screenPts.Count > 0)
            {
                PointF lastPt = screenPts[^1];
                float endX = plotArea.Right - 16;
                float endY = plotArea.Bottom;

                using Pen projPen = new(IndustrialTheme.WarnBar, 2f) { DashStyle = DashStyle.Dash };
                g.DrawLine(projPen, lastPt, new PointF(endX, endY));

                using SolidBrush projBrush = new(IndustrialTheme.WarnText);
                using StringFormat rightSf = new() { Alignment = StringAlignment.Far };
                g.DrawString(
                    $"Dự phóng chạm Scrap sau ~{_roll.EstimatedRemainingGrinds} lần mài",
                    IndustrialTheme.SmallCaptionFont,
                    projBrush,
                    new PointF(endX - 6, endY - 32),
                    rightSf);

                g.DrawString(
                    $"Dự kiến (+{_roll.EstimatedRemainingGrinds} ca)",
                    IndustrialTheme.SmallCaptionFont,
                    projBrush,
                    new PointF(endX, plotArea.Bottom + 8),
                    rightSf);
            }

            // 8. Vẽ các nút điểm (Nodes) & Nhãn chống đè chữ (Anti-Collision Labels)
            using (SolidBrush normalBrush = new(IndustrialTheme.PrimaryAccent))
            using (SolidBrush abnormalBrush = new(IndustrialTheme.DangerAccent))
            using (SolidBrush whiteBrush = new(Color.White))
            using (SolidBrush textBrush = new(Color.FromArgb(30, 41, 59)))
            using (StringFormat centerFormat = new() { Alignment = StringAlignment.Center })
            {
                float lastDrawnValX = -999f;
                float lastDrawnAxisX = -999f;

                for (int i = 0; i < screenPts.Count; i++)
                {
                    PointF pt = screenPts[i];
                    var info = points[i];

                    float radius = info.IsAbnormal ? 6.5f : 4.5f;
                    g.FillEllipse(whiteBrush, pt.X - radius - 1.5f, pt.Y - radius - 1.5f, (radius + 1.5f) * 2, (radius + 1.5f) * 2);
                    g.FillEllipse(info.IsAbnormal ? abnormalBrush : normalBrush, pt.X - radius, pt.Y - radius, radius * 2, radius * 2);

                    bool isPriorityNode = (i == 0) || (i == screenPts.Count - 1) || info.IsAbnormal;

                    // Chỉ vẽ giá trị ĐK nếu cách nhãn trước ít nhất 52px (hoặc là điểm ưu tiên)
                    if (isPriorityNode || (pt.X - lastDrawnValX >= 52f && (screenPts[^1].X - pt.X >= 45f)))
                    {
                        string valText = info.IsAbnormal ? $"{info.Dia:0.00} (!)" : $"{info.Dia:0.00}";
                        SolidBrush lblBrush = info.IsAbnormal ? abnormalBrush : textBrush;
                        g.DrawString(valText, IndustrialTheme.SmallCaptionFont, lblBrush, pt.X, pt.Y - 19, centerFormat);
                        lastDrawnValX = pt.X;
                    }

                    // Chỉ vẽ nhãn trục X nếu cách nhãn trước ít nhất 46px
                    if (i == 0 || i == screenPts.Count - 1 || (pt.X - lastDrawnAxisX >= 46f && (screenPts[^1].X - pt.X >= 42f)))
                    {
                        g.DrawString(info.Label, IndustrialTheme.SmallCaptionFont, textBrush, pt.X, plotArea.Bottom + 7, centerFormat);
                        if (info.Date.HasValue && i > 0)
                        {
                            g.DrawString(info.Date.Value.ToString("dd/MM"), IndustrialTheme.SmallCaptionFont, Brushes.Gray, pt.X, plotArea.Bottom + 23, centerFormat);
                        }
                        lastDrawnAxisX = pt.X;
                    }
                }
            }

            // Viền khung PlotArea
            using Pen borderPen = new(Color.FromArgb(148, 163, 184), 1.2f);
            g.DrawRectangle(borderPen, plotArea);
        }

        private static void DrawLegendRightAligned(Graphics g, int rightEdgeX, int y)
        {
            int x = rightEdgeX - 365;

            using SolidBrush normalBrush = new(IndustrialTheme.PrimaryAccent);
            using SolidBrush abnormalBrush = new(IndustrialTheme.DangerAccent);
            using Pen projPen = new(IndustrialTheme.WarnBar, 2f) { DashStyle = DashStyle.Dash };
            using SolidBrush textBrush = new(Color.FromArgb(51, 65, 85));

            g.FillEllipse(normalBrush, x, y + 3, 9, 9);
            g.DrawString("Ca mài chuẩn", IndustrialTheme.SmallCaptionFont, textBrush, x + 13, y);

            x += 102;
            g.FillEllipse(abnormalBrush, x, y + 3, 9, 9);
            g.DrawString("Ca sự cố (!)", IndustrialTheme.SmallCaptionFont, textBrush, x + 13, y);

            x += 95;
            g.DrawLine(projPen, x, y + 8, x + 20, y + 8);
            g.DrawString("Dự phóng chạm Scrap", IndustrialTheme.SmallCaptionFont, textBrush, x + 24, y);
        }

        private static float ValueToY(decimal value, decimal min, decimal max, Rectangle plot)
        {
            if (max <= min) return plot.Bottom;
            decimal clamped = Math.Clamp(value, min, max);
            float ratio = (float)((clamped - min) / (max - min));
            return plot.Bottom - ratio * plot.Height;
        }
    }
}
