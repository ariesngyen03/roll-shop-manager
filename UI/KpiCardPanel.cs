using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RollShopManager.UI;

/// <summary>
/// Thẻ hiển thị chỉ số KPI chuẩn SCADA/MES:
/// - Vạch màu nhấn 4px ở cạnh trái (Accent Bar)
/// - Tiêu đề phụ chữ nhỏ in hoa màu xám
/// - Con số chỉ số lớn 18pt Bold
/// - Dòng diễn giải phụ (Subtext)
/// - Nền trắng, viền mảnh Slate-200, bo nhẹ các góc.
/// </summary>
public class KpiCardPanel : Panel
{
    private string _cardTitle = "TIÊU ĐỀ CHỈ SỐ";
    private string _cardValue = "0";
    private string _cardSubText = string.Empty;
    private Color _accentColor = IndustrialTheme.PrimaryAccent;
    private int _accentBarWidth = 4;
    private int _cornerRadius = 6;

    public KpiCardPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = IndustrialTheme.CardBackground;
        Padding = new Padding(16, 12, 14, 12);
        MinimumSize = new Size(180, 85);
        Dock = DockStyle.Fill;
    }

    [Category("Industrial KPI")]
    [DefaultValue("TIÊU ĐỀ CHỈ SỐ")]
    public string CardTitle
    {
        get => _cardTitle;
        set
        {
            _cardTitle = value ?? string.Empty;
            Invalidate();
        }
    }

    [Category("Industrial KPI")]
    [DefaultValue("0")]
    public string CardValue
    {
        get => _cardValue;
        set
        {
            _cardValue = value ?? string.Empty;
            Invalidate();
        }
    }

    [Category("Industrial KPI")]
    [DefaultValue("")]
    public string CardSubText
    {
        get => _cardSubText;
        set
        {
            _cardSubText = value ?? string.Empty;
            Invalidate();
        }
    }

    [Category("Industrial KPI")]
    public Color AccentColor
    {
        get => _accentColor;
        set
        {
            _accentColor = value;
            Invalidate();
        }
    }

    [Category("Industrial KPI")]
    [DefaultValue(4)]
    public int AccentBarWidth
    {
        get => _accentBarWidth;
        set
        {
            _accentBarWidth = Math.Max(2, value);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Rectangle rect = new(0, 0, Width - 1, Height - 1);

        // 1. Vẽ nền trắng & viền Slate-200
        using (GraphicsPath path = IndustrialTheme.CreateRoundedRectanglePath(rect, _cornerRadius))
        using (SolidBrush bgBrush = new(IndustrialTheme.CardBackground))
        using (Pen borderPen = new(IndustrialTheme.BorderLight, 1f))
        {
            g.FillPath(bgBrush, path);
            g.DrawPath(borderPen, path);
        }

        // 2. Vẽ Vạch màu nhấn 4px ở cạnh trái (Left Accent Bar)
        Rectangle accentRect = new(0, 0, _accentBarWidth, Height - 1);
        using (GraphicsPath accentPath = CreateLeftRoundedPath(accentRect, _cornerRadius))
        using (SolidBrush accentBrush = new(_accentColor))
        {
            g.FillPath(accentBrush, accentPath);
        }

        // 3. Vùng vẽ nội dung văn bản (đẩy vào trong sau thanh accent)
        int textLeft = _accentBarWidth + 14;
        int textTop = 8;
        int textWidth = Math.Max(40, Width - textLeft - 12);

        // Dòng 1: Tiêu đề in hoa màu xám
        Rectangle titleRect = new(textLeft, textTop, textWidth, 18);
        using (SolidBrush titleBrush = new(IndustrialTheme.MutedText))
        using (StringFormat sf = new() { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
        {
            g.DrawString(_cardTitle.ToUpperInvariant(), IndustrialTheme.CardTitleFont, titleBrush, titleRect, sf);
        }

        // Dòng 2: Con số chỉ số lớn Bold
        int valueTop = textTop + 18;
        Rectangle valueRect = new(textLeft, valueTop, textWidth, 32);
        using (SolidBrush valueBrush = new(Color.FromArgb(15, 23, 42)))
        using (StringFormat sf = new() { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
        {
            g.DrawString(_cardValue, IndustrialTheme.CardValueFont, valueBrush, valueRect, sf);
        }

        // Dòng 3 (Nếu có): Diễn giải phụ
        if (!string.IsNullOrWhiteSpace(_cardSubText))
        {
            int subTop = valueTop + 31;
            Rectangle subRect = new(textLeft, subTop, textWidth, Math.Max(16, Height - subTop - 4));
            using (SolidBrush subBrush = new(Color.FromArgb(100, 116, 139)))
            using (StringFormat sf = new() { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(_cardSubText, IndustrialTheme.SmallCaptionFont, subBrush, subRect, sf);
            }
        }
    }

    private static GraphicsPath CreateLeftRoundedPath(Rectangle rect, int radius)
    {
        GraphicsPath path = new();
        int diameter = radius * 2;
        Rectangle arc = new(rect.X, rect.Y, diameter, diameter);

        // Top-Left
        path.AddArc(arc, 180, 90);
        // Top-Right góc vuông
        path.AddLine(rect.Right, rect.Top, rect.Right, rect.Bottom);
        // Bottom-Right góc vuông
        // Bottom-Left bo tròn
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }
}
