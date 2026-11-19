namespace RollShopManager.Security;

/// <summary>
/// Bộ lọc thông điệp Windows toàn ứng dụng: bắt các sự kiện chuột / bàn phím để cập nhật
/// <see cref="AppSession.LastActivityTime"/>, phục vụ tự động khóa về Operator sau 3 phút không thao tác.
/// Đăng ký 1 lần trong Program.Main: Application.AddMessageFilter(new IdleActivityMessageFilter());
/// </summary>
public sealed class IdleActivityMessageFilter : IMessageFilter
{
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_NCMOUSEMOVE = 0x00A0;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int WM_NCRBUTTONDOWN = 0x00A4;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_MOUSEHWHEEL = 0x020E;

    private Point _lastCursorPosition = new(int.MinValue, int.MinValue);

    /// <summary>Luôn trả về false để thông điệp tiếp tục được xử lý bình thường bởi control đích.</summary>
    public bool PreFilterMessage(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_MOUSEMOVE:
            case WM_NCMOUSEMOVE:
                // Windows có thể gửi WM_MOUSEMOVE "giả" khi cửa sổ đổi focus / vẽ lại dù chuột đứng yên.
                // Chỉ tính là hoạt động khi vị trí con trỏ thực sự thay đổi.
                Point currentPosition = Cursor.Position;
                if (currentPosition != _lastCursorPosition)
                {
                    _lastCursorPosition = currentPosition;
                    AppSession.RegisterActivity();
                }

                break;

            case WM_LBUTTONDOWN:
            case WM_RBUTTONDOWN:
            case WM_MBUTTONDOWN:
            case WM_NCLBUTTONDOWN:
            case WM_NCRBUTTONDOWN:
            case WM_MOUSEWHEEL:
            case WM_MOUSEHWHEEL:
            case WM_KEYDOWN:
            case WM_SYSKEYDOWN:
                AppSession.RegisterActivity();
                break;
        }

        return false;
    }
}
