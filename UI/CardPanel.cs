using System.Drawing.Drawing2D;

namespace RollShopManager.UI;

/// <summary>
/// Khối chứa giao diện dạng Card chuẩn Modern Industrial: Nền trắng, viền mảnh Slate-200 bo góc nhẹ.
/// Dùng để bọc các form nhập liệu, bảng dữ liệu hoặc cụm công cụ.
/// </summary>
public class CardPanel : Panel
{
    private int _cornerRadius = 6;

    public CardPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = IndustrialTheme.CardBackground;
        Padding = new Padding(16);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle rect = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath path = IndustrialTheme.CreateRoundedRectanglePath(rect, _cornerRadius);
        using SolidBrush bgBrush = new(IndustrialTheme.CardBackground);
        using Pen borderPen = new(IndustrialTheme.BorderLight, 1f);

        g.FillPath(bgBrush, path);
        g.DrawPath(borderPen, path);
    }
}
