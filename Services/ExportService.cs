using System.Diagnostics;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RollShopManager.Models;

namespace RollShopManager.Services;

/// <summary>
/// Dịch vụ Xuất bản Hồ sơ Kỹ thuật &amp; Báo cáo Công nghiệp Chuẩn Tập đoàn Tôn Hoa Sen (ISO 9001:2015 Standard):
/// 1. Xuất Excel .xlsx chuẩn 5 Sheets (QUAN_LY_TRUC, MAI_WR, MAI_BUR, MAI_IMR, BAO_CAO) với công thức động FormulaA1.
/// 2. Xuất Báo cáo Kỳ sản xuất &amp; Kế hoạch vật tư ra PDF (A4 Ngang - Mã biểu mẫu BM-RS-01/HSG).
/// 3. Xuất Phiếu Lý lịch &amp; Theo dõi Tuổi thọ Trục Cán (Roll Passport) ra PDF (A4 Dọc - Mã biểu mẫu BM-RS-02/HSG).
/// </summary>
public static class ExportService
{
    // Bảng màu nhận diện Công nghiệp Tập đoàn Hoa Sen (Hoa Sen Corporate Industrial Palette)
    private const string HoaSenMaroonHex = "#7F1D1D";      // Đỏ đô Hoa Sen chủ đạo
    private const string HoaSenDarkSlateHex = "#1E293B";   // Xám thép đậm cho Header bảng
    private const string HoaSenSubHeaderHex = "#F1F5F9";   // Xám nhạt cho dòng tiêu đề phụ / tổng cộng
    private const string HoaSenBorderHex = "#94A3B8";      // Viền kẻ bảng sắc nét khi in A4
    private const string SafeBgHex = "#DCFCE7";
    private const string SafeFgHex = "#166534";
    private const string WarnBgHex = "#FEF3C7";
    private const string WarnFgHex = "#92400E";
    private const string CritBgHex = "#FEE2E2";
    private const string CritFgHex = "#991B1B";

    // Màu cho ClosedXML Excel
    private static readonly XLColor ExcelBrandMaroon = XLColor.FromArgb(127, 29, 29);
    private static readonly XLColor ExcelHeaderBg = XLColor.FromArgb(30, 41, 59);
    private static readonly XLColor ExcelHeaderFg = XLColor.White;
    private static readonly XLColor ExcelZebraBg = XLColor.FromArgb(248, 250, 252);
    private static readonly XLColor ExcelDangerBg = XLColor.FromArgb(254, 226, 226);
    private static readonly XLColor ExcelDangerFg = XLColor.FromArgb(153, 27, 27);
    private static readonly XLColor ExcelWarnBg = XLColor.FromArgb(254, 243, 199);
    private static readonly XLColor ExcelWarnFg = XLColor.FromArgb(146, 64, 14);
    private static readonly XLColor ExcelSafeBg = XLColor.FromArgb(220, 252, 231);
    private static readonly XLColor ExcelSafeFg = XLColor.FromArgb(22, 101, 52);

    static ExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // =========================================================================
    // 1. XUẤT EXCEL .XLSX CHUẨN 5 SHEETS (CLOSEDXML)
    // =========================================================================

    public static void ExportFullWorkbookToExcel(
        string filePath,
        DateTime fromDate,
        DateTime toDate,
        decimal productionTons,
        IReadOnlyList<RollLifetimeViewDto> allRolls,
        IReadOnlyList<GrindingLogViewDto> wrLogs,
        IReadOnlyList<GrindingLogViewDto> burLogs,
        IReadOnlyList<GrindingLogViewDto> imrLogs,
        IReadOnlyList<TypeConsumptionAndForecastDto> typeSummaries,
        IReadOnlyList<RollPeriodWearDto> rollPeriodDetails,
        IReadOnlyList<MonthlyConsumptionMatrixDto>? yearlyMatrix = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(allRolls);
        ArgumentNullException.ThrowIfNull(wrLogs);
        ArgumentNullException.ThrowIfNull(burLogs);
        ArgumentNullException.ThrowIfNull(imrLogs);
        ArgumentNullException.ThrowIfNull(typeSummaries);
        ArgumentNullException.ThrowIfNull(rollPeriodDetails);

        using var workbook = new XLWorkbook();
        workbook.Style.Font.FontName = "Arial";
        workbook.Style.Font.FontSize = 10;

        BuildMasterRollSheet(workbook, allRolls);
        BuildGrindingSheet(workbook, "MAI_WR", "NHẬT KÝ MÀI TRỤC LÀM VIỆC (WORK ROLL - WR)", wrLogs);
        BuildGrindingSheet(workbook, "MAI_BUR", "NHẬT KÝ MÀI TRỤC ĐỠ (BACKUP ROLL - BUR)", burLogs);
        BuildGrindingSheet(workbook, "MAI_IMR", "NHẬT KÝ MÀI TRỤC TRUNG GIAN (INTERMEDIATE ROLL - IMR)", imrLogs);
        BuildReportSheet(workbook, fromDate, toDate, productionTons, typeSummaries, rollPeriodDetails, yearlyMatrix);

        workbook.SaveAs(filePath);
    }

    private static void BuildMasterRollSheet(XLWorkbook workbook, IReadOnlyList<RollLifetimeViewDto> rolls)
    {
        var ws = workbook.Worksheets.Add("QUAN_LY_TRUC");

        // Header pháp nhân chuẩn công nghiệp
        ws.Cell(1, 1).Value = "CÔNG TY CỔ PHẦN TẬP ĐOÀN HOA SEN - PHÂN XƯỞNG CƠ KHÍ TRỤC CÁN (ROLL SHOP)";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 10;
        ws.Cell(1, 1).Style.Font.FontColor = ExcelBrandMaroon;

        ws.Cell(2, 1).Value = "BẢNG QUẢN LÝ DANH MỤC & TUỔI THỌ TRỤC CÁN (MASTER ROLL LIFETIME)";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromArgb(15, 23, 42);

        // Bảng thống kê quân số trục theo 3 loại dùng COUNTIFS (Dòng 3-6)
        ws.Cell(3, 1).Value = "Loại Trục";
        ws.Cell(3, 2).Value = "Tổng Số Trục";
        ws.Cell(3, 3).Value = "Đang Hoạt Động";
        ws.Cell(3, 4).Value = "Sắp Hết Đời (<15%)";
        ws.Cell(3, 5).Value = "Hết Đời (Scrap)";
        ws.Range(3, 1, 3, 5).Style.Font.Bold = true;
        ws.Range(3, 1, 3, 5).Style.Fill.BackgroundColor = XLColor.FromArgb(71, 85, 105);
        ws.Range(3, 1, 3, 5).Style.Font.FontColor = XLColor.White;
        ws.Range(3, 1, 3, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        string[] types = { RollTypes.WR, RollTypes.IMR, RollTypes.BUR };
        int statStartRow = 4;
        int headerRow = 8;
        int dataStartRow = 9;
        int dataEndRow = Math.Max(dataStartRow, dataStartRow + rolls.Count - 1);

        for (int i = 0; i < types.Length; i++)
        {
            int r = statStartRow + i;
            string t = types[i];
            ws.Cell(r, 1).Value = t;
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r, 2).FormulaA1 = $"COUNTIF(C{dataStartRow}:C{dataEndRow}, \"{t}\")";
            ws.Cell(r, 3).FormulaA1 = $"COUNTIFS(C{dataStartRow}:C{dataEndRow}, \"{t}\", M{dataStartRow}:M{dataEndRow}, \"*{RollStatusTexts.InUse}*\")";
            ws.Cell(r, 4).FormulaA1 = $"COUNTIFS(C{dataStartRow}:C{dataEndRow}, \"{t}\", K{dataStartRow}:K{dataEndRow}, \"<0.15\", M{dataStartRow}:M{dataEndRow}, \"<>{RollStatusTexts.Scrap}\")";
            ws.Cell(r, 5).FormulaA1 = $"COUNTIFS(C{dataStartRow}:C{dataEndRow}, \"{t}\", M{dataStartRow}:M{dataEndRow}, \"*{RollStatusTexts.Scrap}*\")";

            ws.Range(r, 2, r, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Range(r, 1, r, 5).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            ws.Range(r, 1, r, 5).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
        }

        string[] headers =
        {
            "STT", "Mã Trục", "Loại Trục", "Ngày Đưa Vào SD", "ĐK Max (mm)", "ĐK Min (mm)",
            "Số Lần Đã Mài", "ĐK Hiện Tại (mm)", "Tổng mm Đã Mài", "Lượng Dư Còn Lại (mm)",
            "% Lifetime Còn Lại", "Ước Tính Lần Mài Còn Lại", "Trạng Thái", "Ghi Chú"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = ExcelHeaderBg;
            cell.Style.Font.FontColor = ExcelHeaderFg;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        int currRow = headerRow + 1;
        for (int idx = 0; idx < rolls.Count; idx++)
        {
            var roll = rolls[idx];
            ws.Cell(currRow, 1).Value = idx + 1;
            ws.Cell(currRow, 2).Value = roll.RollCode;
            ws.Cell(currRow, 3).Value = roll.RollType;
            ws.Cell(currRow, 4).Value = roll.CommissionDate.ToString("dd/MM/yyyy");
            ws.Cell(currRow, 5).Value = roll.MaxDiameter;
            ws.Cell(currRow, 6).Value = roll.MinDiameter;
            ws.Cell(currRow, 7).Value = roll.TotalGrindCount;
            ws.Cell(currRow, 8).Value = roll.CurrentDiameter;

            // Công thức Excel trực tiếp FormulaA1
            ws.Cell(currRow, 9).FormulaA1 = $"=E{currRow}-H{currRow}";
            ws.Cell(currRow, 10).FormulaA1 = $"=MAX(0, H{currRow}-F{currRow})";
            ws.Cell(currRow, 11).FormulaA1 = $"=MAX(0, (H{currRow}-F{currRow})/(E{currRow}-F{currRow}))";
            ws.Cell(currRow, 12).Value = roll.LoggedGrindCount > 0 ? roll.EstimatedRemainingGrinds : "Chưa mài";
            ws.Cell(currRow, 13).Value = roll.Status;
            ws.Cell(currRow, 14).Value = roll.Notes ?? string.Empty;

            ws.Range(currRow, 1, currRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currRow, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currRow, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(currRow, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 10).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 11).Style.NumberFormat.Format = "0.0%";

            if (roll.IsScrap)
            {
                ws.Range(currRow, 1, currRow, 14).Style.Fill.BackgroundColor = ExcelDangerBg;
                ws.Cell(currRow, 13).Style.Font.FontColor = ExcelDangerFg;
                ws.Cell(currRow, 13).Style.Font.Bold = true;
            }
            else if (roll.RemainingLifetimePercent < 15m)
            {
                ws.Range(currRow, 1, currRow, 14).Style.Fill.BackgroundColor = ExcelWarnBg;
                ws.Cell(currRow, 13).Style.Font.FontColor = ExcelWarnFg;
                ws.Cell(currRow, 13).Style.Font.Bold = true;
            }
            else if (idx % 2 == 1)
            {
                ws.Range(currRow, 1, currRow, 14).Style.Fill.BackgroundColor = ExcelZebraBg;
            }

            currRow++;
        }

        int totalRows = Math.Max(headerRow + 1, currRow - 1);
        ws.Range(headerRow, 1, totalRows, headers.Length).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
        ws.Range(headerRow, 1, totalRows, headers.Length).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        ws.SheetView.FreezeRows(headerRow);
        ws.Columns().AdjustToContents(headerRow, totalRows, 8, 45);
    }

    private static void BuildGrindingSheet(XLWorkbook workbook, string sheetName, string title, IReadOnlyList<GrindingLogViewDto> logs)
    {
        var ws = workbook.Worksheets.Add(sheetName);

        ws.Cell(1, 1).Value = "CÔNG TY CỔ PHẦN TẬP ĐOÀN HOA SEN - PHÂN XƯỞNG CƠ KHÍ TRỤC CÁN (ROLL SHOP)";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 10;
        ws.Cell(1, 1).Style.Font.FontColor = ExcelBrandMaroon;

        ws.Cell(2, 1).Value = title;
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 13;

        int headerRow = 4;
        string[] headers =
        {
            "STT", "Thời Gian Mài", "Mã Trục", "Lần Mài Thứ", "ĐK Trước Mài (mm)",
            "ĐK Sau Mài (mm)", "Tiêu Hao Mài (mm)", "Tổng mm Mài Lũy Kế",
            "% Lifetime Sau Mài", "Ước Tính Lần Mài Còn", "Trạng Thái Sau Mài", "Ghi Chú Kỹ Thuật"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = ExcelHeaderBg;
            cell.Style.Font.FontColor = ExcelHeaderFg;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int currRow = headerRow + 1;
        for (int i = 0; i < logs.Count; i++)
        {
            var log = logs[i];
            ws.Cell(currRow, 1).Value = i + 1;
            ws.Cell(currRow, 2).Value = log.GrindDate.ToString("dd/MM/yyyy HH:mm");
            ws.Cell(currRow, 3).Value = log.RollCode;
            ws.Cell(currRow, 4).Value = log.GrindSequence;
            ws.Cell(currRow, 5).Value = log.PreGrindDiameter;
            ws.Cell(currRow, 6).Value = log.PostGrindDiameter;
            ws.Cell(currRow, 7).FormulaA1 = $"=E{currRow}-F{currRow}";
            ws.Cell(currRow, 8).Value = log.CumulativeWear;
            ws.Cell(currRow, 9).Value = log.RemainingLifetimePercent / 100m;
            ws.Cell(currRow, 10).Value = log.EstimatedRemainingGrinds;
            ws.Cell(currRow, 11).Value = log.ConditionText;
            ws.Cell(currRow, 12).Value = log.Remarks ?? string.Empty;

            ws.Range(currRow, 1, currRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(currRow, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 9).Style.NumberFormat.Format = "0.0%";

            if (log.IsAbnormal)
            {
                ws.Range(currRow, 1, currRow, 12).Style.Fill.BackgroundColor = ExcelDangerBg;
                ws.Cell(currRow, 11).Style.Font.FontColor = ExcelDangerFg;
                ws.Cell(currRow, 11).Style.Font.Bold = true;
            }
            else if (i % 2 == 1)
            {
                ws.Range(currRow, 1, currRow, 12).Style.Fill.BackgroundColor = ExcelZebraBg;
            }

            currRow++;
        }

        int totalRows = Math.Max(headerRow + 1, currRow - 1);
        ws.Range(headerRow, 1, totalRows, headers.Length).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
        ws.Range(headerRow, 1, totalRows, headers.Length).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        ws.SheetView.FreezeRows(headerRow);
        ws.Columns().AdjustToContents(headerRow, totalRows, 8, 45);
    }

    private static void BuildReportSheet(
        XLWorkbook workbook,
        DateTime fromDate,
        DateTime toDate,
        decimal productionTons,
        IReadOnlyList<TypeConsumptionAndForecastDto> typeSummaries,
        IReadOnlyList<RollPeriodWearDto> rollDetails,
        IReadOnlyList<MonthlyConsumptionMatrixDto>? yearlyMatrix = null)
    {
        var ws = workbook.Worksheets.Add("BAO_CAO");

        ws.Cell(1, 1).Value = "CÔNG TY CỔ PHẦN TẬP ĐOÀN HOA SEN - NHÀ MÁY TÔN HOA SEN (PHÂN XƯỞNG ROLL SHOP)";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 10;
        ws.Cell(1, 1).Style.Font.FontColor = ExcelBrandMaroon;

        ws.Cell(2, 1).Value = "BÁO CÁO TỔNG HỢP TIÊU HAO TRỤC CÁN & DỰ BÁO ĐẶT HÀNG VẬT TƯ (BM-RS-01/HSG)";
        ws.Cell(2, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromArgb(15, 23, 42);

        ws.Cell(3, 1).Value = $"Kỳ báo cáo: Từ ngày {fromDate:dd/MM/yyyy} đến ngày {toDate:dd/MM/yyyy}  |  Sản lượng cán: {productionTons:N0} Tấn thép  |  Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cell(3, 1).Style.Font.Italic = true;

        int currRow = 5;

        // =========================================================================
        // PHẦN I: MA TRẬN TIÊU HAO 12 THÁNG & SẢN LƯỢNG TRONG NĂM (NẾU CÓ)
        // =========================================================================
        if (yearlyMatrix is not null && yearlyMatrix.Count > 0)
        {
            ws.Cell(currRow, 1).Value = "I. MA TRẬN TIÊU HAO 12 THÁNG & ĐỊNH MỨC SẢN LƯỢNG TRONG NĂM";
            ws.Cell(currRow, 1).Style.Font.Bold = true;
            ws.Cell(currRow, 1).Style.Font.FontSize = 11;
            currRow++;

            int matrixHeaderRow = currRow;
            string[] matrixHeaders =
            {
                "Tháng", "Sản Lượng (Tấn)",
                "WR Lượt Mài", "WR Mài (mm)", "WR mm/Tấn", "WR Quy Đổi",
                "IMR Lượt Mài", "IMR Mài (mm)", "IMR mm/Tấn", "IMR Quy Đổi",
                "BUR Lượt Mài", "BUR Mài (mm)", "BUR mm/Tấn", "BUR Quy Đổi",
                "Tổng mm (3 Loại)", "Suất Tổng (mm/Tấn)", "Xu Hướng MoM"
            };

            for (int c = 0; c < matrixHeaders.Length; c++)
            {
                var cell = ws.Cell(matrixHeaderRow, c + 1);
                cell.Value = matrixHeaders[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = ExcelHeaderBg;
                cell.Style.Font.FontColor = ExcelHeaderFg;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            currRow++;
            foreach (var m in yearlyMatrix)
            {
                ws.Cell(currRow, 1).Value = m.MonthDisplay;
                ws.Cell(currRow, 2).Value = m.ProductionTons;

                ws.Cell(currRow, 3).Value = m.WrGrindCount;
                ws.Cell(currRow, 4).Value = m.WrTotalWearMm;
                ws.Cell(currRow, 5).Value = m.WrWearPerTon;
                ws.Cell(currRow, 6).Value = m.WrEquivalentRollsUsed;

                ws.Cell(currRow, 7).Value = m.ImrGrindCount;
                ws.Cell(currRow, 8).Value = m.ImrTotalWearMm;
                ws.Cell(currRow, 9).Value = m.ImrWearPerTon;
                ws.Cell(currRow, 10).Value = m.ImrEquivalentRollsUsed;

                ws.Cell(currRow, 11).Value = m.BurGrindCount;
                ws.Cell(currRow, 12).Value = m.BurTotalWearMm;
                ws.Cell(currRow, 13).Value = m.BurWearPerTon;
                ws.Cell(currRow, 14).Value = m.BurEquivalentRollsUsed;

                ws.Cell(currRow, 15).Value = m.TotalAllRollsWearMm;
                ws.Cell(currRow, 16).Value = m.TotalWearPerTon;

                if (m.MomTrendPercent.HasValue)
                {
                    decimal mom = m.MomTrendPercent.Value;
                    ws.Cell(currRow, 17).Value = mom > 0 ? $"▲ +{mom:0.0}%" : (mom < 0 ? $"▼ {mom:0.0}%" : "― 0.0%");
                }
                else
                {
                    ws.Cell(currRow, 17).Value = "―";
                }

                ws.Cell(currRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(currRow, 2).Style.NumberFormat.Format = "#,##0";
                ws.Cell(currRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(currRow, 4).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(currRow, 5).Style.NumberFormat.Format = "0.0000";
                ws.Cell(currRow, 6).Style.NumberFormat.Format = "0.00";

                ws.Cell(currRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(currRow, 8).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(currRow, 9).Style.NumberFormat.Format = "0.0000";
                ws.Cell(currRow, 10).Style.NumberFormat.Format = "0.00";

                ws.Cell(currRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(currRow, 12).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(currRow, 13).Style.NumberFormat.Format = "0.0000";
                ws.Cell(currRow, 14).Style.NumberFormat.Format = "0.00";

                ws.Cell(currRow, 15).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(currRow, 16).Style.NumberFormat.Format = "0.0000";
                ws.Cell(currRow, 17).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                if (m.IsSummaryRow)
                {
                    ws.Range(currRow, 1, currRow, 17).Style.Font.Bold = true;
                    ws.Range(currRow, 1, currRow, 17).Style.Fill.BackgroundColor = XLColor.FromArgb(226, 232, 240);
                }

                currRow++;
            }

            ws.Range(matrixHeaderRow, 1, currRow - 1, 17).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
            ws.Range(matrixHeaderRow, 1, currRow - 1, 17).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            currRow += 2;
        }

        string section1Title = yearlyMatrix is not null && yearlyMatrix.Count > 0
            ? "II. BẢNG TỔNG HỢP ĐỊNH MỨC TIÊU HAO THEO CHỦNG LOẠI & DỰ BÁO ĐẶT HÀNG"
            : "I. BẢNG TỔNG HỢP ĐỊNH MỨC TIÊU HAO THEO CHỦNG LOẠI & DỰ BÁO ĐẶT HÀNG";

        ws.Cell(currRow, 1).Value = section1Title;
        ws.Cell(currRow, 1).Style.Font.Bold = true;
        ws.Cell(currRow, 1).Style.Font.FontSize = 11;
        currRow++;

        int header1Row = currRow;
        string[] headers1 =
        {
            "Loại Trục", "Số Lượt Mài", "Mài Chuẩn (mm)", "Mài Sự Cố (mm)", "Tổng mm Mài",
            "mm / Tấn", "mm / 1.000 Tấn", "Số Cây Dùng (Quy đổi)", "Tồn Kho Còn (mm)",
            "Số Cây Nguyên Còn", "Hao (Cây/Tháng)", "Runway (Ngày)", "Lead Time (Ngày)",
            "Hạn Đặt (Ngày)", "NGÀY ĐẶT HÀNG", "TRẠNG THÁI CẢNH BÁO"
        };

        for (int c = 0; c < headers1.Length; c++)
        {
            var cell = ws.Cell(header1Row, c + 1);
            cell.Value = headers1[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = ExcelHeaderBg;
            cell.Style.Font.FontColor = ExcelHeaderFg;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        currRow++;
        foreach (var s in typeSummaries)
        {
            ws.Cell(currRow, 1).Value = s.RollType;
            ws.Cell(currRow, 2).Value = s.GrindCountInPeriod;
            ws.Cell(currRow, 3).Value = s.NormalWearMm;
            ws.Cell(currRow, 4).Value = s.AbnormalWearMm;
            ws.Cell(currRow, 5).Value = s.TotalWearMmInPeriod;
            ws.Cell(currRow, 6).Value = s.WearMmPerTon;
            ws.Cell(currRow, 7).Value = s.WearMmPer1000Tons;
            ws.Cell(currRow, 8).Value = s.EquivalentRollsConsumed;
            ws.Cell(currRow, 9).Value = s.TotalRemainingMmInShop;
            ws.Cell(currRow, 10).Value = s.EquivalentRollsRemaining;
            ws.Cell(currRow, 11).Value = s.MonthlyBurnRateRolls;
            ws.Cell(currRow, 12).Value = s.RunwayDays.HasValue ? s.RunwayDays.Value : "N/A";
            ws.Cell(currRow, 13).Value = s.LeadTimeDays;
            ws.Cell(currRow, 14).Value = s.DaysUntilOrderDeadline.HasValue ? s.DaysUntilOrderDeadline.Value : "N/A";
            ws.Cell(currRow, 15).Value = s.RecommendedOrderDate.HasValue ? s.RecommendedOrderDate.Value.ToString("dd/MM/yyyy") : "N/A";
            ws.Cell(currRow, 16).Value = s.AlertStatus;

            ws.Cell(currRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(currRow, 12, currRow, 16).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(currRow, 3).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 6).Style.NumberFormat.Format = "0.0000";
            ws.Cell(currRow, 7).Style.NumberFormat.Format = "0.00";
            ws.Cell(currRow, 8).Style.NumberFormat.Format = "0.00";
            ws.Cell(currRow, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 10).Style.NumberFormat.Format = "0.00";
            ws.Cell(currRow, 11).Style.NumberFormat.Format = "0.00";

            var alertCell = ws.Cell(currRow, 16);
            alertCell.Style.Font.Bold = true;
            if (s.AlertStatus.Contains("NGUY CẤP"))
            {
                alertCell.Style.Fill.BackgroundColor = ExcelDangerBg;
                alertCell.Style.Font.FontColor = ExcelDangerFg;
            }
            else if (s.AlertStatus.Contains("CẢNH BÁO"))
            {
                alertCell.Style.Fill.BackgroundColor = ExcelWarnBg;
                alertCell.Style.Font.FontColor = ExcelWarnFg;
            }
            else
            {
                alertCell.Style.Fill.BackgroundColor = ExcelSafeBg;
                alertCell.Style.Font.FontColor = ExcelSafeFg;
            }

            currRow++;
        }

        // Hàng tổng cộng
        ws.Cell(currRow, 1).Value = "TỔNG CỘNG";
        ws.Cell(currRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(currRow, 2).FormulaA1 = $"=SUM(B{header1Row + 1}:B{currRow - 1})";
        ws.Cell(currRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(currRow, 3).FormulaA1 = $"=SUM(C{header1Row + 1}:C{currRow - 1})";
        ws.Cell(currRow, 4).FormulaA1 = $"=SUM(D{header1Row + 1}:D{currRow - 1})";
        ws.Cell(currRow, 5).FormulaA1 = $"=SUM(E{header1Row + 1}:E{currRow - 1})";
        ws.Cell(currRow, 6).FormulaA1 = $"=SUM(F{header1Row + 1}:F{currRow - 1})";
        ws.Cell(currRow, 7).FormulaA1 = $"=SUM(G{header1Row + 1}:G{currRow - 1})";
        ws.Cell(currRow, 8).FormulaA1 = $"=SUM(H{header1Row + 1}:H{currRow - 1})";
        ws.Cell(currRow, 9).FormulaA1 = $"=SUM(I{header1Row + 1}:I{currRow - 1})";
        ws.Cell(currRow, 10).FormulaA1 = $"=SUM(J{header1Row + 1}:J{currRow - 1})";
        ws.Cell(currRow, 11).FormulaA1 = $"=SUM(K{header1Row + 1}:K{currRow - 1})";

        ws.Cell(currRow, 3).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(currRow, 4).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(currRow, 5).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(currRow, 6).Style.NumberFormat.Format = "0.0000";
        ws.Cell(currRow, 7).Style.NumberFormat.Format = "0.00";
        ws.Cell(currRow, 8).Style.NumberFormat.Format = "0.00";
        ws.Cell(currRow, 9).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(currRow, 10).Style.NumberFormat.Format = "0.00";
        ws.Cell(currRow, 11).Style.NumberFormat.Format = "0.00";

        ws.Range(currRow, 1, currRow, 16).Style.Font.Bold = true;
        ws.Range(currRow, 1, currRow, 16).Style.Fill.BackgroundColor = XLColor.FromArgb(226, 232, 240);
        ws.Range(header1Row, 1, currRow, 16).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
        ws.Range(header1Row, 1, currRow, 16).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);

        // Phần chi tiết từng trục
        string section2Title = yearlyMatrix is not null && yearlyMatrix.Count > 0
            ? "III. BẢNG THỐNG KÊ CHI TIẾT HAO MÒN CỦA TỪNG CÂY TRỤC TRONG KỲ"
            : "II. BẢNG THỐNG KÊ CHI TIẾT HAO MÒN CỦA TỪNG CÂY TRỤC TRONG KỲ";

        int section2Start = currRow + 3;
        ws.Cell(section2Start, 1).Value = section2Title;
        ws.Cell(section2Start, 1).Style.Font.Bold = true;
        ws.Cell(section2Start, 1).Style.Font.FontSize = 11;

        int header2Row = section2Start + 1;
        string[] headers2 =
        {
            "STT", "Mã Trục", "Loại Trục", "Số Lần Mài Kỳ", "Mài Chuẩn (mm)", "Mài Sự Cố (mm)",
            "Tổng mm Mài Trong Kỳ", "ĐK Hiện Tại (mm)", "Lượng Dư Còn Lại (mm)", "% Lifetime Còn Lại", "Trạng Thái"
        };

        for (int c = 0; c < headers2.Length; c++)
        {
            var cell = ws.Cell(header2Row, c + 1);
            cell.Value = headers2[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = ExcelHeaderBg;
            cell.Style.Font.FontColor = ExcelHeaderFg;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        currRow = header2Row + 1;
        for (int i = 0; i < rollDetails.Count; i++)
        {
            var r = rollDetails[i];
            ws.Cell(currRow, 1).Value = i + 1;
            ws.Cell(currRow, 2).Value = r.RollCode;
            ws.Cell(currRow, 3).Value = r.RollType;
            ws.Cell(currRow, 4).Value = r.GrindCountInPeriod;
            ws.Cell(currRow, 5).Value = r.NormalWearMm;
            ws.Cell(currRow, 6).Value = r.AbnormalWearMm;
            ws.Cell(currRow, 7).FormulaA1 = $"=E{currRow}+F{currRow}";
            ws.Cell(currRow, 8).Value = r.CurrentDiameter;
            ws.Cell(currRow, 9).Value = r.RemainingUsableMm;
            ws.Cell(currRow, 10).Value = r.RemainingLifetimePercent / 100m;
            ws.Cell(currRow, 11).Value = r.Status;

            ws.Range(currRow, 1, currRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currRow, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(currRow, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currRow, 10).Style.NumberFormat.Format = "0.0%";

            if (r.Status == RollStatusTexts.Scrap)
            {
                ws.Range(currRow, 1, currRow, 11).Style.Fill.BackgroundColor = ExcelDangerBg;
            }
            else if (r.RemainingLifetimePercent < 15m)
            {
                ws.Range(currRow, 1, currRow, 11).Style.Fill.BackgroundColor = ExcelWarnBg;
            }
            else if (i % 2 == 1)
            {
                ws.Range(currRow, 1, currRow, 11).Style.Fill.BackgroundColor = ExcelZebraBg;
            }

            currRow++;
        }

        int totalDetailRows = Math.Max(header2Row + 1, currRow - 1);
        ws.Range(header2Row, 1, totalDetailRows, headers2.Length).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
        ws.Range(header2Row, 1, totalDetailRows, headers2.Length).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        ws.SheetView.FreezeRows(header1Row);
        ws.Columns().AdjustToContents(header1Row, totalDetailRows, 8, 45);
    }

    // =========================================================================
    // 2. XUẤT PDF BIỂU MẪU CÔNG NGHIỆP CHUẨN TÔN HOA SEN (ISO 9001:2015)
    // =========================================================================

    /// <summary>
    /// Mẫu 1 (BM-RS-01/HSG): Báo cáo Tổng hợp Tiêu hao Trục Cán &amp; Kế hoạch Vật tư (A4 Ngang - Landscape).
    /// Bố cục chuẩn Tập đoàn Hoa Sen: Khung Header ISO 3 ô, Khối KPI vận hành, Bảng kẻ lưới 4 cạnh sắc nét,
    /// Chữ số căn phải chuẩn kỹ thuật, Khối phê duyệt 4 cấp.
    /// </summary>
    public static void ExportPeriodReportToPdf(
        string filePath,
        DateTime fromDate,
        DateTime toDate,
        decimal productionTons,
        IReadOnlyList<TypeConsumptionAndForecastDto> typeSummaries,
        IReadOnlyList<RollPeriodWearDto> rollDetails,
        IReadOnlyList<MonthlyConsumptionMatrixDto>? yearlyMatrix = null)
    {
        int totalGrinds = typeSummaries.Sum(s => s.GrindCountInPeriod);
        int totalAbnormal = typeSummaries.Sum(s => s.AbnormalGrindCountInPeriod);
        decimal totalNormalMm = typeSummaries.Sum(s => s.NormalWearMm);
        decimal totalAbnormalMm = typeSummaries.Sum(s => s.AbnormalWearMm);
        decimal totalWearMm = totalNormalMm + totalAbnormalMm;
        decimal overallWearPerTon = productionTons > 0m ? totalWearMm / productionTons : 0m;
        decimal totalEqConsumed = typeSummaries.Sum(s => s.EquivalentRollsConsumed);
        decimal totalRemainingMm = typeSummaries.Sum(s => s.TotalRemainingMmInShop);
        decimal totalEqRemaining = typeSummaries.Sum(s => s.EquivalentRollsRemaining);
        decimal totalMonthlyBurnRolls = typeSummaries.Sum(s => s.MonthlyBurnRateRolls);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8.5f).FontColor("#0F172A"));

                // -------------------------------------------------------------
                // KHUNG HEADER BIỂU MẪU ISO 3 CỘT CHUẨN TẬP ĐOÀN HOA SEN
                // -------------------------------------------------------------
                page.Header().PaddingBottom(8).Table(headerTable =>
                {
                    headerTable.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(205); // Cột 1: Pháp nhân Tập đoàn / Nhà máy
                        cols.RelativeColumn();    // Cột 2: Tên Biểu mẫu báo cáo
                        cols.ConstantColumn(165); // Cột 3: Mã hiệu kiểm soát tài liệu ISO
                    });

                    // Ô 1: Thương hiệu Tập đoàn Hoa Sen
                    headerTable.Cell().Border(0.8f).BorderColor(HoaSenMaroonHex).Padding(6).Column(c =>
                    {
                        c.Item().Background(HoaSenMaroonHex).PaddingVertical(3).PaddingHorizontal(6)
                            .Text("HOA SEN GROUP - TÔN HOA SEN").FontColor(Colors.White).Bold().FontSize(9.5f);
                        c.Item().PaddingTop(3).Text("CÔNG TY CP TẬP ĐOÀN HOA SEN").Bold().FontSize(8.5f).FontColor(HoaSenMaroonHex);
                        c.Item().Text("NHÀ MÁY CÁN NGUỘI - XƯỞNG TRỤC CÁN").Bold().FontSize(8f).FontColor("#334155");
                        c.Item().Text("Hệ thống Quản lý Sản xuất Công nghiệp (MES)").FontSize(7.5f).Italic().FontColor("#64748B");
                    });

                    // Ô 2: Tiêu đề Báo cáo & Kỳ thống kê
                    headerTable.Cell().Border(0.8f).BorderColor(HoaSenMaroonHex).Padding(6).AlignMiddle().AlignCenter().Column(c =>
                    {
                        c.Item().AlignCenter().Text("BÁO CÁO TỔNG HỢP TIÊU HAO TRỤC CÁN & KẾ HOẠCH VẬT TƯ")
                            .Bold().FontSize(12.5f).FontColor(HoaSenMaroonHex);
                        c.Item().PaddingTop(3).AlignCenter()
                            .Text($"KỲ BÁO CÁO: TỪ NGÀY {fromDate:dd/MM/yyyy} ĐẾN NGÀY {toDate:dd/MM/yyyy} ({(toDate.Date - fromDate.Date).Days + 1} NGÀY)")
                            .Bold().FontSize(9f).FontColor("#1E293B");
                        c.Item().PaddingTop(2).AlignCenter()
                            .Text($"Sản lượng thép cán trong kỳ: {productionTons:N0} Tấn  |  Chủng loại trục: WR - IMR - BUR")
                            .FontSize(8.5f).FontColor("#334155");
                    });

                    // Ô 3: Khung kiểm soát tài liệu ISO 9001:2015
                    headerTable.Cell().Border(0.8f).BorderColor(HoaSenMaroonHex).Padding(6).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.ConstantItem(72).Text("Mã biểu mẫu:").FontSize(8f).FontColor("#475569");
                            r.RelativeItem().AlignRight().Text("BM-RS-01/HSG").Bold().FontSize(8f).FontColor(HoaSenMaroonHex);
                        });
                        c.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(72).Text("Hệ thống:").FontSize(8f).FontColor("#475569");
                            r.RelativeItem().AlignRight().Text("ISO 9001:2015").Bold().FontSize(8f);
                        });
                        c.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(72).Text("Lần ban hành:").FontSize(8f).FontColor("#475569");
                            r.RelativeItem().AlignRight().Text("02 (Năm 2026)").FontSize(8f);
                        });
                        c.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(72).Text("Ngày in biểu:").FontSize(8f).FontColor("#475569");
                            r.RelativeItem().AlignRight().Text($"{DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8f);
                        });
                    });
                });

                // -------------------------------------------------------------
                // NỘI DUNG CHÍNH
                // -------------------------------------------------------------
                page.Content().Column(col =>
                {
                    // KHỐI TÓM TẮT 4 CHỈ TIÊU KỸ THUẬT TRONG KỲ
                    col.Item().PaddingBottom(8).Table(kpiTable =>
                    {
                        kpiTable.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                        });

                        AddSummaryKpiBox(kpiTable, "1. SẢN LƯỢNG CÁN TRONG KỲ", $"{productionTons:N0} TẤN",
                            $"Kỳ vận hành: {fromDate:dd/MM} - {toDate:dd/MM/yyyy}", HoaSenMaroonHex);

                        AddSummaryKpiBox(kpiTable, "2. TỔNG SỐ CA MÀI PHÁT SINH", $"{totalGrinds} LƯỢT MÀI",
                            $"Mài chuẩn: {totalGrinds - totalAbnormal} ca | Sự cố: {totalAbnormal} ca", "#1E293B");

                        AddSummaryKpiBox(kpiTable, "3. TỔNG LƯỢNG HAO MÒN TRỤC", $"{totalWearMm:N2} mm",
                            $"Suất tiêu hao: {overallWearPerTon:N4} mm / Tấn", "#0F766E");

                        AddSummaryKpiBox(kpiTable, "4. TỒN KHO LƯỢNG DƯ TOÀN XƯỞNG", $"{totalRemainingMm:N2} mm",
                            $"Tương đương: ~{totalEqRemaining:N2} cây trục nguyên", "#1D4ED8");
                    });

                    // NẾU CÓ MA TRẬN 12 THÁNG: IN BẢNG MA TRẬN 12 THÁNG
                    if (yearlyMatrix is not null && yearlyMatrix.Count > 0)
                    {
                        col.Item().PaddingBottom(8).Column(mCol =>
                        {
                            mCol.Item().Background(HoaSenSubHeaderHex).Border(0.6f).BorderColor(HoaSenBorderHex)
                                .PaddingVertical(3.5f).PaddingHorizontal(6)
                                .Text("I. MA TRẬN TIÊU HAO 12 THÁNG & ĐỊNH MỨC SẢN LƯỢNG TRONG NĂM")
                                .Bold().FontSize(9f).FontColor(HoaSenMaroonHex);

                            mCol.Item().PaddingTop(2).Table(mTable =>
                            {
                                mTable.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1.3f); // Tháng
                                    cols.RelativeColumn(1.1f); // Tấn
                                    cols.RelativeColumn(0.9f); // WR mm
                                    cols.RelativeColumn(1.0f); // WR mm/1000T
                                    cols.RelativeColumn(0.9f); // IMR mm
                                    cols.RelativeColumn(1.0f); // IMR mm/1000T
                                    cols.RelativeColumn(0.9f); // BUR mm
                                    cols.RelativeColumn(1.0f); // BUR mm/1000T
                                    cols.RelativeColumn(1.0f); // Tổng mm
                                    cols.RelativeColumn(1.1f); // Suất tổng
                                    cols.RelativeColumn(1.0f); // MoM
                                });

                                mTable.Header(h =>
                                {
                                    AddTableHeaderCell(h, "Kỳ Tháng");
                                    AddTableHeaderCell(h, "Sản Lượng\n(Tấn)");
                                    AddTableHeaderCell(h, "WR Mài\n(mm)");
                                    AddTableHeaderCell(h, "WR Định Mức\n(mm/Tấn)");
                                    AddTableHeaderCell(h, "IMR Mài\n(mm)");
                                    AddTableHeaderCell(h, "IMR Định Mức\n(mm/Tấn)");
                                    AddTableHeaderCell(h, "BUR Mài\n(mm)");
                                    AddTableHeaderCell(h, "BUR Định Mức\n(mm/Tấn)");
                                    AddTableHeaderCell(h, "Tổng Mài\n(3 Loại mm)");
                                    AddTableHeaderCell(h, "Suất Tổng\n(mm/Tấn)");
                                    AddTableHeaderCell(h, "Xu Hướng\nMoM (%)");
                                });

                                foreach (var m in yearlyMatrix)
                                {
                                    bool isSummary = m.IsSummaryRow;
                                    string? rowBg = isSummary ? "#E2E8F0" : (m.MonthIndex % 2 == 1 ? "#F8FAFC" : null);

                                    AddTableBodyCell(mTable, m.MonthDisplay, alignCenter: true, bold: isSummary, bg: rowBg);
                                    AddTableBodyCell(mTable, $"{m.ProductionTons:N0}", alignRight: true, bold: isSummary, bg: rowBg);
                                    AddTableBodyCell(mTable, $"{m.WrTotalWearMm:N2}", alignRight: true, bg: rowBg);
                                    AddTableBodyCell(mTable, $"{m.WrWearPerTon:N4}", alignRight: true, bg: rowBg);
                                    AddTableBodyCell(mTable, $"{m.ImrTotalWearMm:N2}", alignRight: true, bg: rowBg);
                                    AddTableBodyCell(mTable, $"{m.ImrWearPerTon:N4}", alignRight: true, bg: rowBg);
                                    AddTableBodyCell(mTable, $"{m.BurTotalWearMm:N2}", alignRight: true, bg: rowBg);
                                    AddTableBodyCell(mTable, $"{m.BurWearPerTon:N4}", alignRight: true, bg: rowBg);
                                    AddTableBodyCell(mTable, $"{m.TotalAllRollsWearMm:N2}", alignRight: true, bold: true, bg: rowBg);
                                    AddTableBodyCell(mTable, $"{m.TotalWearPerTon:N4}", alignRight: true, bold: true, bg: rowBg);

                                    string momText = m.MomTrendPercent.HasValue
                                        ? (m.MomTrendPercent.Value > 0 ? $"+{m.MomTrendPercent.Value:0.0}%" : $"{m.MomTrendPercent.Value:0.0}%")
                                        : "-";
                                    string? momColor = m.MomTrendPercent.HasValue && m.MomTrendPercent.Value > 0 ? CritFgHex : (m.MomTrendPercent.HasValue && m.MomTrendPercent.Value < 0 ? SafeFgHex : null);
                                    AddTableBodyCell(mTable, momText, alignCenter: true, textColor: momColor, bg: rowBg);
                                }
                            });
                        });
                    }

                    string typeSectionTitle = yearlyMatrix is not null && yearlyMatrix.Count > 0
                        ? "II. BẢNG PHÂN TÍCH ĐỊNH MỨC TIÊU HAO & DỰ BÁO NGÀY ĐẶT HÀNG THEO CHỦNG LOẠI (WR - IMR - BUR)"
                        : "I. BẢNG PHÂN TÍCH ĐỊNH MỨC TIÊU HAO & DỰ BÁO NGÀY ĐẶT HÀNG THEO CHỦNG LOẠI (WR - IMR - BUR)";

                    // BẢNG TỔNG HỢP THEO CHỦNG LOẠI & DỰ BÁO ĐẶT HÀNG
                    col.Item().Background(HoaSenSubHeaderHex).Border(0.6f).BorderColor(HoaSenBorderHex)
                        .PaddingVertical(3.5f).PaddingHorizontal(6)
                        .Text(typeSectionTitle)
                        .Bold().FontSize(9f).FontColor(HoaSenMaroonHex);

                    col.Item().PaddingTop(2).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(36);   // Loại
                            cols.ConstantColumn(35);   // Lượt mài
                            cols.RelativeColumn(0.95f); // Mài chuẩn
                            cols.RelativeColumn(0.95f); // Mài sự cố
                            cols.RelativeColumn(1.0f);  // Tổng mài
                            cols.RelativeColumn(0.95f); // mm/1000T
                            cols.RelativeColumn(0.9f);  // Cây dùng
                            cols.RelativeColumn(1.0f);  // Tồn kho mm
                            cols.RelativeColumn(0.9f);  // Cây còn
                            cols.RelativeColumn(0.95f); // Hao (Cây/Tháng)
                            cols.RelativeColumn(0.85f); // Runway
                            cols.ConstantColumn(38);    // LeadTime
                            cols.RelativeColumn(0.85f); // Hạn đặt
                            cols.ConstantColumn(58);    // Ngày đặt
                            cols.RelativeColumn(1.4f);  // Cảnh báo
                        });

                        table.Header(h =>
                        {
                            AddTableHeaderCell(h, "Chủng\nLoại");
                            AddTableHeaderCell(h, "Lượt\nMài");
                            AddTableHeaderCell(h, "Mài Chuẩn\n(mm)");
                            AddTableHeaderCell(h, "Mài Sự Cố\n(mm)");
                            AddTableHeaderCell(h, "Tổng Mài\n(mm)");
                            AddTableHeaderCell(h, "Định Mức\n(mm/Tấn)");
                            AddTableHeaderCell(h, "Quy Đổi\nCây Dùng");
                            AddTableHeaderCell(h, "Tồn Kho\nCòn (mm)");
                            AddTableHeaderCell(h, "Quy Đổi\nCây Còn");
                            AddTableHeaderCell(h, "Hao\n(Cây/Tháng)");
                            AddTableHeaderCell(h, "Runway\n(Ngày)");
                            AddTableHeaderCell(h, "Lead\nTime");
                            AddTableHeaderCell(h, "Hạn Đặt\n(Ngày)");
                            AddTableHeaderCell(h, "Ngày Đặt\nKhuyến Nghị");
                            AddTableHeaderCell(h, "Đánh Giá Tình Trạng\nKế Hoạch Vật Tư");
                        });

                        foreach (var s in typeSummaries)
                        {
                            AddTableBodyCell(table, s.RollType, alignCenter: true, bold: true);
                            AddTableBodyCell(table, s.GrindCountInPeriod.ToString(), alignCenter: true);
                            AddTableBodyCell(table, $"{s.NormalWearMm:N2}", alignRight: true);
                            AddTableBodyCell(table, $"{s.AbnormalWearMm:N2}", alignRight: true, textColor: s.AbnormalWearMm > 0 ? CritFgHex : null);
                            AddTableBodyCell(table, $"{s.TotalWearMmInPeriod:N2}", alignRight: true, bold: true);
                            AddTableBodyCell(table, $"{s.WearMmPerTon:N4}", alignRight: true);
                            AddTableBodyCell(table, $"{s.EquivalentRollsConsumed:N2}", alignRight: true);
                            AddTableBodyCell(table, $"{s.TotalRemainingMmInShop:N2}", alignRight: true);
                            AddTableBodyCell(table, $"{s.EquivalentRollsRemaining:N2}", alignRight: true, bold: true);
                            AddTableBodyCell(table, $"{s.MonthlyBurnRateRolls:N2}", alignRight: true);
                            AddTableBodyCell(table, s.RunwayDays?.ToString() ?? "-", alignCenter: true);
                            AddTableBodyCell(table, $"{s.LeadTimeDays}d", alignCenter: true);
                            AddTableBodyCell(table, s.DaysUntilOrderDeadline?.ToString() ?? "-", alignCenter: true, bold: true);
                            AddTableBodyCell(table, s.RecommendedOrderDate?.ToString("dd/MM/yyyy") ?? "-", alignCenter: true, bold: true);

                            (string bg, string fg) = ResolvePdfAlertStyle(s.AlertStatus);
                            table.Cell().Border(0.5f).BorderColor(HoaSenBorderHex).Background(bg).PaddingVertical(3).PaddingHorizontal(4)
                                .AlignMiddle().AlignCenter().Text(s.AlertStatus).Bold().FontSize(7.5f).FontColor(fg);
                        }

                        // Dòng TỔNG CỘNG
                        AddTableFooterCell(table, "TỔNG", alignCenter: true);
                        AddTableFooterCell(table, totalGrinds.ToString(), alignCenter: true);
                        AddTableFooterCell(table, $"{totalNormalMm:N2}", alignRight: true);
                        AddTableFooterCell(table, $"{totalAbnormalMm:N2}", alignRight: true);
                        AddTableFooterCell(table, $"{totalWearMm:N2}", alignRight: true);
                        AddTableFooterCell(table, $"{overallWearPerTon:N4}", alignRight: true);
                        AddTableFooterCell(table, $"{totalEqConsumed:N2}", alignRight: true);
                        AddTableFooterCell(table, $"{totalRemainingMm:N2}", alignRight: true);
                        AddTableFooterCell(table, $"{totalEqRemaining:N2}", alignRight: true);
                        AddTableFooterCell(table, $"{totalMonthlyBurnRolls:N2}", alignRight: true);
                        AddTableFooterCell(table, "-", alignCenter: true);
                        AddTableFooterCell(table, "-", alignCenter: true);
                        AddTableFooterCell(table, "-", alignCenter: true);
                        AddTableFooterCell(table, "-", alignCenter: true);
                        AddTableFooterCell(table, "TỔNG HỢP TOÀN XƯỞNG", alignCenter: true);
                    });

                    string detailSectionTitle = yearlyMatrix is not null && yearlyMatrix.Count > 0
                        ? "III. BẢNG THỐNG KÊ CHI TIẾT HAO MÒN & TUỔI THỌ TỪNG CÂY TRỤC CÁN TRONG KỲ"
                        : "II. BẢNG THỐNG KÊ CHI TIẾT HAO MÒN & TUỔI THỌ TỪNG CÂY TRỤC CÁN TRONG KỲ";

                    // BẢNG CHI TIẾT TỪNG CÂY TRỤC TRONG KỲ
                    col.Item().PaddingTop(8).Background(HoaSenSubHeaderHex).Border(0.6f).BorderColor(HoaSenBorderHex)
                        .PaddingVertical(3.5f).PaddingHorizontal(6)
                        .Text(detailSectionTitle)
                        .Bold().FontSize(9f).FontColor(HoaSenMaroonHex);

                    col.Item().PaddingTop(2).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(28);   // STT
                            cols.ConstantColumn(68);   // Mã Trục
                            cols.ConstantColumn(45);   // Loại
                            cols.ConstantColumn(50);   // Số lần mài
                            cols.RelativeColumn(1.0f); // Mài chuẩn
                            cols.RelativeColumn(1.0f); // Mài sự cố
                            cols.RelativeColumn(1.1f); // Tổng mài kỳ
                            cols.RelativeColumn(1.1f); // ĐK hiện tại
                            cols.RelativeColumn(1.1f); // Lượng dư còn
                            cols.RelativeColumn(1.0f); // % Tuổi thọ
                            cols.RelativeColumn(1.4f); // Trạng thái
                        });

                        table.Header(h =>
                        {
                            AddTableHeaderCell(h, "STT");
                            AddTableHeaderCell(h, "Mã Số Trục");
                            AddTableHeaderCell(h, "Loại");
                            AddTableHeaderCell(h, "Số Lần\nMài Kỳ");
                            AddTableHeaderCell(h, "Mài Chuẩn\n(mm)");
                            AddTableHeaderCell(h, "Mài Sự Cố\n(mm)");
                            AddTableHeaderCell(h, "Tổng Mài Kỳ\n(mm)");
                            AddTableHeaderCell(h, "ĐK Hiện Tại\n(mm)");
                            AddTableHeaderCell(h, "Lượng Dư Còn\n(mm)");
                            AddTableHeaderCell(h, "% Tuổi Thọ\nCòn Lại");
                            AddTableHeaderCell(h, "Trạng Thái Kỹ Thuật");
                        });

                        for (int i = 0; i < rollDetails.Count; i++)
                        {
                            var r = rollDetails[i];
                            string? rowBg = i % 2 == 1 ? "#F8FAFC" : null;

                            AddTableBodyCell(table, (i + 1).ToString(), alignCenter: true, bg: rowBg);
                            AddTableBodyCell(table, r.RollCode, alignCenter: true, bold: true, bg: rowBg);
                            AddTableBodyCell(table, r.RollType, alignCenter: true, bg: rowBg);
                            AddTableBodyCell(table, r.GrindCountInPeriod.ToString(), alignCenter: true, bg: rowBg);
                            AddTableBodyCell(table, $"{r.NormalWearMm:N2}", alignRight: true, bg: rowBg);
                            AddTableBodyCell(table, $"{r.AbnormalWearMm:N2}", alignRight: true, bg: rowBg, textColor: r.AbnormalWearMm > 0 ? CritFgHex : null);
                            AddTableBodyCell(table, $"{r.TotalWearMmInPeriod:N2}", alignRight: true, bold: true, bg: rowBg);
                            AddTableBodyCell(table, $"{r.CurrentDiameter:N2}", alignRight: true, bg: rowBg);
                            AddTableBodyCell(table, $"{r.RemainingUsableMm:N2}", alignRight: true, bg: rowBg);
                            AddTableBodyCell(table, $"{r.RemainingLifetimePercent:0.0}%", alignRight: true, bold: true, bg: rowBg,
                                textColor: r.RemainingLifetimePercent < 15m ? CritFgHex : null);

                            (string stBg, string stFg) = ResolvePdfAlertStyle(r.Status);
                            table.Cell().Border(0.5f).BorderColor(HoaSenBorderHex).Background(stBg).PaddingVertical(2.5f).PaddingHorizontal(4)
                                .AlignMiddle().AlignCenter().Text(r.Status).Bold().FontSize(7.5f).FontColor(stFg);
                        }
                    });

                    // KHỐI CHỮ KÝ PHÊ DUYỆT 4 CẤP CHUẨN NHÀ MÁY TÔN HOA SEN
                    col.Item().PaddingTop(12).EnsureSpace(75).Table(sigTable =>
                    {
                        sigTable.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                        });

                        AddSignatureCell(sigTable, "NGƯỜI LẬP BIỂU", "Kỹ thuật viên Roll Shop");
                        AddSignatureCell(sigTable, "TỔ TRƯỞNG TỔ MÀI", "Kiểm tra số liệu thực tế");
                        AddSignatureCell(sigTable, "QUẢN ĐỐC XƯỞNG CÁN", "Thẩm định kỹ thuật & vật tư");
                        AddSignatureCell(sigTable, "BAN GIÁM ĐỐC NHÀ MÁY", "Phê duyệt kế hoạch đặt hàng");
                    });
                });

                // -------------------------------------------------------------
                // CHÂN TRANG CHUẨN ISO
                // -------------------------------------------------------------
                page.Footer().PaddingTop(4).Column(f =>
                {
                    f.Item().LineHorizontal(0.6f).LineColor(HoaSenBorderHex);
                    f.Item().PaddingTop(3).Row(r =>
                    {
                        r.RelativeItem().Text("Biểu mẫu BM-RS-01/HSG - Trích xuất tự động từ Hệ thống Quản lý Xưởng Cơ Khí Trục Cán (MES Roll Shop) - Tập đoàn Hoa Sen.")
                            .FontSize(7.5f).Italic().FontColor("#64748B");
                        r.ConstantItem(100).AlignRight().Text(x =>
                        {
                            x.Span("Trang ").FontSize(8f).FontColor("#334155");
                            x.CurrentPageNumber().FontSize(8f).Bold();
                            x.Span(" / ").FontSize(8f).FontColor("#334155");
                            x.TotalPages().FontSize(8f).Bold();
                        });
                    });
                });
            });
        }).GeneratePdf(filePath);
    }

    /// <summary>
    /// Mẫu 2 (BM-RS-02/HSG): Phiếu Lý Lịch &amp; Theo Dõi Tuổi Thọ Trục Cán - Roll Passport (A4 Dọc - Portrait).
    /// Bố cục chuẩn Tập đoàn Hoa Sen: Khung Header ISO 3 cột, Bảng Hồ sơ Kỹ thuật 4 cột đóng khung,
    /// Bảng Nhật ký mài toàn vòng đời kẻ ô sắc nét, Khối chữ ký xác nhận 3 bên.
    /// </summary>
    public static void ExportRollPassportToPdf(
        string filePath,
        RollLifetimeViewDto roll,
        IReadOnlyList<GrindingLogViewDto> logs)
    {
        int abnormalCount = logs.Count(l => l.IsAbnormal);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Portrait());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8.5f).FontColor("#0F172A"));

                // -------------------------------------------------------------
                // KHUNG HEADER ISO 3 CỘT CHUẨN TÔN HOA SEN
                // -------------------------------------------------------------
                page.Header().PaddingBottom(8).Table(headerTable =>
                {
                    headerTable.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(165);
                        cols.RelativeColumn();
                        cols.ConstantColumn(135);
                    });

                    headerTable.Cell().Border(0.8f).BorderColor(HoaSenMaroonHex).Padding(5).Column(c =>
                    {
                        c.Item().Background(HoaSenMaroonHex).PaddingVertical(2.5f).PaddingHorizontal(5)
                            .Text("HOA SEN GROUP").FontColor(Colors.White).Bold().FontSize(9f);
                        c.Item().PaddingTop(2).Text("CÔNG TY CP TẬP ĐOÀN HOA SEN").Bold().FontSize(8f).FontColor(HoaSenMaroonHex);
                        c.Item().Text("NHÀ MÁY TÔN HOA SEN").Bold().FontSize(7.5f).FontColor("#334155");
                        c.Item().Text("Xưởng Cơ Khí Trục Cán (Roll Shop)").FontSize(7.5f).FontColor("#475569");
                    });

                    headerTable.Cell().Border(0.8f).BorderColor(HoaSenMaroonHex).Padding(5).AlignMiddle().AlignCenter().Column(c =>
                    {
                        c.Item().AlignCenter().Text("PHIẾU LÝ LỊCH & THEO DÕI TUỔI THỌ TRỤC CÁN")
                            .Bold().FontSize(11.5f).FontColor(HoaSenMaroonHex);
                        c.Item().PaddingTop(2).AlignCenter().Text("(TECHNICAL ROLL PASSPORT & GRINDING LOG)")
                            .Bold().FontSize(8f).FontColor("#475569");
                        c.Item().PaddingTop(3).AlignCenter()
                            .Text($"MÃ SỐ TRỤC: {roll.RollCode}   |   CHỦNG LOẠI: {roll.RollType}")
                            .Bold().FontSize(9.5f).FontColor("#0F172A");
                    });

                    headerTable.Cell().Border(0.8f).BorderColor(HoaSenMaroonHex).Padding(5).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.ConstantItem(58).Text("Mã hiệu:").FontSize(7.5f).FontColor("#475569");
                            r.RelativeItem().AlignRight().Text("BM-RS-02/HSG").Bold().FontSize(7.5f).FontColor(HoaSenMaroonHex);
                        });
                        c.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(58).Text("Tiêu chuẩn:").FontSize(7.5f).FontColor("#475569");
                            r.RelativeItem().AlignRight().Text("ISO 9001:2015").Bold().FontSize(7.5f);
                        });
                        c.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(58).Text("Ban hành:").FontSize(7.5f).FontColor("#475569");
                            r.RelativeItem().AlignRight().Text("Lần 02").FontSize(7.5f);
                        });
                        c.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(58).Text("Ngày in:").FontSize(7.5f).FontColor("#475569");
                            r.RelativeItem().AlignRight().Text($"{DateTime.Now:dd/MM/yyyy}").FontSize(7.5f);
                        });
                    });
                });

                // -------------------------------------------------------------
                // NỘI DUNG CHÍNH
                // -------------------------------------------------------------
                page.Content().Column(col =>
                {
                    // MỤC I: BẢNG HỒ SƠ KỸ THUẬT & THÔNG SỐ TUỔI THỌ TRỤC
                    col.Item().Background(HoaSenSubHeaderHex).Border(0.6f).BorderColor(HoaSenBorderHex)
                        .PaddingVertical(3.5f).PaddingHorizontal(6)
                        .Text("I. THÔNG SỐ KỸ THUẬT CHUẨN & ĐÁNH GIÁ HIỆN TRẠNG TUỔI THỌ TRỤC CÁN")
                        .Bold().FontSize(9f).FontColor(HoaSenMaroonHex);

                    col.Item().PaddingTop(2).Table(specTable =>
                    {
                        specTable.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(125);
                            cols.RelativeColumn();
                            cols.ConstantColumn(135);
                            cols.RelativeColumn();
                        });

                        AddSpecRow(specTable,
                            "Mã định danh trục:", roll.RollCode,
                            "Chủng loại trục:", $"{roll.RollType} ({RollTypes.GetDisplayName(roll.RollType)})",
                            boldVal1: true, boldVal2: true);

                        AddSpecRow(specTable,
                            "Ngày đưa vào sử dụng:", $"{roll.CommissionDate:dd/MM/yyyy}",
                            "Trạng thái kỹ thuật:", roll.Status,
                            boldVal2: true);

                        AddSpecRow(specTable,
                            "ĐK nguyên thủy (Max):", $"{roll.MaxDiameter:N2} mm",
                            "ĐK hiện tại:", $"{roll.CurrentDiameter:N2} mm",
                            boldVal2: true);

                        AddSpecRow(specTable,
                            "ĐK giới hạn Scrap (Min):", $"{roll.MinDiameter:N2} mm",
                            "Lượng dư còn lại:", $"{roll.RemainingUsableMm:N2} / {roll.UsableLayerMm:N2} mm",
                            boldVal2: true);

                        AddSpecRow(specTable,
                            "ĐK hiện trạng bắt đầu:", $"{roll.InitialDiameter:N2} mm",
                            "Tỷ lệ % Lifetime còn lại:", $"{roll.RemainingLifetimePercent:0.0} %",
                            boldVal2: true);

                        AddSpecRow(specTable,
                            "Số ca mài đã ghi nhận:", $"{roll.LoggedGrindCount} ca (Sự cố: {abnormalCount} ca)",
                            "Tổng lượng mòn lũy kế:", $"{roll.TotalWearMm:N2} mm");

                        AddSpecRow(specTable,
                            "Tốc độ mòn TB / ca:", roll.LoggedGrindCount > 0 ? $"{roll.AverageWearPerGrind:N3} mm/lần" : "Chưa có dữ liệu mài",
                            "Ước tính số lần mài còn:", roll.EstimatedRemainingGrindsText,
                            boldVal2: true);

                        // Dòng Ghi chú kỹ thuật
                        specTable.Cell().Border(0.5f).BorderColor(HoaSenBorderHex).Background("#F8FAFC").Padding(4)
                            .Text("Ghi chú hồ sơ trục:").Bold().FontSize(8f).FontColor("#334155");
                        specTable.Cell().ColumnSpan(3).Border(0.5f).BorderColor(HoaSenBorderHex).Padding(4)
                            .Text(string.IsNullOrWhiteSpace(roll.Notes) ? "Không có ghi chú đặc biệt." : roll.Notes).FontSize(8f);
                    });

                    // MỤC II: BẢNG NHẬT KÝ TOÀN BỘ CÁC CA MÀI CỦA TRỤC
                    col.Item().PaddingTop(10).Background(HoaSenSubHeaderHex).Border(0.6f).BorderColor(HoaSenBorderHex)
                        .PaddingVertical(3.5f).PaddingHorizontal(6)
                        .Text($"II. LỊCH SỬ CHI TIẾT CÁC CA MÀI TRỤC {roll.RollCode} ({logs.Count} BẢN GHI TRONG PHẦN MỀM)")
                        .Bold().FontSize(9f).FontColor(HoaSenMaroonHex);

                    col.Item().PaddingTop(2).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(30);   // Lần mài
                            cols.ConstantColumn(68);   // Ngày giờ mài
                            cols.ConstantColumn(52);   // ĐK Trước
                            cols.ConstantColumn(52);   // ĐK Sau
                            cols.ConstantColumn(46);   // Tiêu hao
                            cols.ConstantColumn(48);   // Lũy kế
                            cols.ConstantColumn(45);   // % Còn
                            cols.ConstantColumn(38);   // Ước còn
                            cols.ConstantColumn(48);   // Phân loại
                            cols.RelativeColumn();     // Ghi chú kỹ thuật
                        });

                        table.Header(h =>
                        {
                            AddTableHeaderCell(h, "Lần\nMài");
                            AddTableHeaderCell(h, "Thời Gian\nThực Hiện");
                            AddTableHeaderCell(h, "ĐK Trước\n(mm)");
                            AddTableHeaderCell(h, "ĐK Sau\n(mm)");
                            AddTableHeaderCell(h, "Tiêu Hao\n(mm)");
                            AddTableHeaderCell(h, "Lũy Kế\n(mm)");
                            AddTableHeaderCell(h, "% Life\nCòn");
                            AddTableHeaderCell(h, "Ước\nCòn");
                            AddTableHeaderCell(h, "Tình\nTrạng");
                            AddTableHeaderCell(h, "Ghi Chú Kỹ Thuật / Nguyên Nhân Sự Cố");
                        });

                        if (logs.Count == 0)
                        {
                            table.Cell().ColumnSpan(10).Border(0.5f).BorderColor(HoaSenBorderHex).Padding(8)
                                .AlignCenter().Text("Chưa có phát sinh ca mài nào được ghi nhận cho cây trục này.").Italic().FontColor("#64748B");
                        }
                        else
                        {
                            for (int i = 0; i < logs.Count; i++)
                            {
                                var log = logs[i];
                                string? rowBg = log.IsAbnormal ? CritBgHex : (i % 2 == 1 ? "#F8FAFC" : null);

                                AddTableBodyCell(table, log.GrindSequence.ToString(), alignCenter: true, bold: true, bg: rowBg);
                                AddTableBodyCell(table, $"{log.GrindDate:dd/MM/yy HH:mm}", alignCenter: true, bg: rowBg);
                                AddTableBodyCell(table, $"{log.PreGrindDiameter:N2}", alignRight: true, bg: rowBg);
                                AddTableBodyCell(table, $"{log.PostGrindDiameter:N2}", alignRight: true, bold: true, bg: rowBg);
                                AddTableBodyCell(table, $"{log.GrindConsumption:N2}", alignRight: true, bold: true, bg: rowBg,
                                    textColor: log.IsAbnormal ? CritFgHex : null);
                                AddTableBodyCell(table, $"{log.CumulativeWear:N2}", alignRight: true, bg: rowBg);
                                AddTableBodyCell(table, $"{log.RemainingLifetimePercent:0.0}%", alignRight: true, bg: rowBg);
                                AddTableBodyCell(table, log.EstimatedRemainingGrinds.ToString(), alignCenter: true, bg: rowBg);

                                AddTableBodyCell(table, log.IsAbnormal ? "Sự cố" : "Chuẩn",
                                    alignCenter: true,
                                    bold: log.IsAbnormal,
                                    bg: rowBg,
                                    textColor: log.IsAbnormal ? CritFgHex : SafeFgHex);

                                AddTableBodyCell(table, log.Remarks ?? string.Empty, bg: rowBg);
                            }
                        }
                    });

                    // KHỐI CHỮ KÝ XÁC NHẬN LÝ LỊCH TRỤC 3 BÊN
                    col.Item().PaddingTop(14).EnsureSpace(75).Table(sigTable =>
                    {
                        sigTable.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                        });

                        AddSignatureCell(sigTable, "KỸ THUẬT VIÊN ROLL SHOP", "Ghi nhận & trích xuất hồ sơ");
                        AddSignatureCell(sigTable, "TỔ TRƯỞNG CƠ KHÍ TRỤC", "Kiểm tra thông số kỹ thuật");
                        AddSignatureCell(sigTable, "QUẢN ĐỐC PHÂN XƯỞNG", "Xác nhận tình trạng thiết bị");
                    });
                });

                // -------------------------------------------------------------
                // CHÂN TRANG CHUẨN ISO
                // -------------------------------------------------------------
                page.Footer().PaddingTop(4).Column(f =>
                {
                    f.Item().LineHorizontal(0.6f).LineColor(HoaSenBorderHex);
                    f.Item().PaddingTop(3).Row(r =>
                    {
                        r.RelativeItem().Text($"Biểu mẫu BM-RS-02/HSG - Hồ sơ lý lịch trục {roll.RollCode} - Hệ thống MES Roll Shop Tập đoàn Hoa Sen.")
                            .FontSize(7.5f).Italic().FontColor("#64748B");
                        r.ConstantItem(90).AlignRight().Text(x =>
                        {
                            x.Span("Trang ").FontSize(8f).FontColor("#334155");
                            x.CurrentPageNumber().FontSize(8f).Bold();
                            x.Span(" / ").FontSize(8f).FontColor("#334155");
                            x.TotalPages().FontSize(8f).Bold();
                        });
                    });
                });
            });
        }).GeneratePdf(filePath);
    }

    // =========================================================================
    // CÁC HÀM HỖ TRỢ ĐỊNH DẠNG BẢNG PDF CHUẨN CÔNG NGHIỆP
    // =========================================================================

    private static void AddSummaryKpiBox(TableDescriptor table, string title, string mainValue, string subText, string accentHex)
    {
        table.Cell().PaddingHorizontal(2).Border(0.6f).BorderColor(HoaSenBorderHex).Column(c =>
        {
            c.Item().Background(accentHex).PaddingVertical(2.5f).PaddingHorizontal(6)
                .Text(title).Bold().FontSize(7.5f).FontColor(Colors.White);
            c.Item().PaddingVertical(4).PaddingHorizontal(6).Column(inner =>
            {
                inner.Item().Text(mainValue).Bold().FontSize(11f).FontColor(accentHex);
                inner.Item().PaddingTop(1).Text(subText).FontSize(7.5f).FontColor("#475569");
            });
        });
    }

    private static void AddTableHeaderCell(TableCellDescriptor header, string text)
    {
        header.Cell()
            .Border(0.5f).BorderColor(HoaSenBorderHex)
            .Background(HoaSenDarkSlateHex)
            .PaddingVertical(4).PaddingHorizontal(3)
            .AlignMiddle().AlignCenter()
            .Text(text).FontColor(Colors.White).Bold().FontSize(7.5f);
    }

    private static void AddTableBodyCell(
        TableDescriptor table,
        string text,
        bool alignCenter = false,
        bool alignRight = false,
        bool bold = false,
        string? bg = null,
        string? textColor = null)
    {
        IContainer cell = table.Cell().Border(0.5f).BorderColor(HoaSenBorderHex);
        if (!string.IsNullOrEmpty(bg))
        {
            cell = cell.Background(bg);
        }

        cell = cell.PaddingVertical(3).PaddingHorizontal(4).AlignMiddle();

        if (alignCenter)
        {
            cell = cell.AlignCenter();
        }
        else if (alignRight)
        {
            cell = cell.AlignRight();
        }
        else
        {
            cell = cell.AlignLeft();
        }

        var span = cell.Text(text).FontSize(8f);
        if (bold)
        {
            span.Bold();
        }
        if (!string.IsNullOrEmpty(textColor))
        {
            span.FontColor(textColor);
        }
    }

    private static void AddTableFooterCell(
        TableDescriptor table,
        string text,
        bool alignCenter = false,
        bool alignRight = false)
    {
        IContainer cell = table.Cell()
            .Border(0.5f).BorderColor(HoaSenBorderHex)
            .Background("#E2E8F0")
            .PaddingVertical(3.5f).PaddingHorizontal(4)
            .AlignMiddle();

        if (alignCenter)
        {
            cell = cell.AlignCenter();
        }
        else if (alignRight)
        {
            cell = cell.AlignRight();
        }

        cell.Text(text).Bold().FontSize(8f).FontColor("#0F172A");
    }

    private static void AddSpecRow(
        TableDescriptor table,
        string label1, string val1,
        string label2, string val2,
        bool boldVal1 = false,
        bool boldVal2 = false)
    {
        table.Cell().Border(0.5f).BorderColor(HoaSenBorderHex).Background("#F8FAFC").PaddingVertical(3.5f).PaddingHorizontal(5)
            .AlignMiddle().Text(label1).Bold().FontSize(8f).FontColor("#334155");

        var c1 = table.Cell().Border(0.5f).BorderColor(HoaSenBorderHex).PaddingVertical(3.5f).PaddingHorizontal(6)
            .AlignMiddle().Text(val1).FontSize(8.5f);
        if (boldVal1) c1.Bold().FontColor(HoaSenMaroonHex);

        table.Cell().Border(0.5f).BorderColor(HoaSenBorderHex).Background("#F8FAFC").PaddingVertical(3.5f).PaddingHorizontal(5)
            .AlignMiddle().Text(label2).Bold().FontSize(8f).FontColor("#334155");

        var c2 = table.Cell().Border(0.5f).BorderColor(HoaSenBorderHex).PaddingVertical(3.5f).PaddingHorizontal(6)
            .AlignMiddle().Text(val2).FontSize(8.5f);
        if (boldVal2) c2.Bold().FontColor("#0F172A");
    }

    private static void AddSignatureCell(TableDescriptor table, string roleTitle, string roleSub)
    {
        table.Cell().Border(0.5f).BorderColor(HoaSenBorderHex).Padding(6).AlignCenter().Column(c =>
        {
            c.Item().AlignCenter().Text(roleTitle).Bold().FontSize(8.5f).FontColor("#0F172A");
            c.Item().AlignCenter().Text($"({roleSub})").Italic().FontSize(7.5f).FontColor("#64748B");
            c.Item().Height(42); // Khoảng trống đóng dấu & ký tên
            c.Item().AlignCenter().Text("Họ và tên: ........................................").FontSize(7.5f).FontColor("#475569");
        });
    }

    private static (string Bg, string Fg) ResolvePdfAlertStyle(string status)
    {
        string s = (status ?? string.Empty).ToUpperInvariant();
        if (s.Contains("NGUY CẤP") || s.Contains("SCRAP") || s.Contains("HẾT ĐỜI"))
        {
            return (CritBgHex, CritFgHex);
        }

        if (s.Contains("CẢNH BÁO") || s.Contains("BẤT THƯỜNG") || s.Contains("KIỂM TRA"))
        {
            return (WarnBgHex, WarnFgHex);
        }

        return (SafeBgHex, SafeFgHex);
    }

    /// <summary>
    /// Mở file an toàn bằng ứng dụng mặc định của hệ điều hành (MS Excel, Acrobat, Edge...).
    /// </summary>
    public static void OpenFileWithDefaultApp(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExportService] Không thể mở file tự động: {ex.Message}");
        }
    }
}
