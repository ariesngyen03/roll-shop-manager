namespace RollShopManager.Security;

/// <summary>Quy tắc định dạng mã PIN quản lý.</summary>
public static class PinPolicy
{
    /// <summary>Mã PIN mặc định khi khởi tạo DB lần đầu.</summary>
    public const string DefaultPin = "123456";

    public const int MinLength = 4;
    public const int MaxLength = 12;

    /// <summary>PIN hợp lệ: chỉ gồm chữ số 0-9, độ dài từ 4 đến 12 ký tự.</summary>
    public static bool IsValidFormat(string? pin, out string errorMessage)
    {
        if (string.IsNullOrEmpty(pin))
        {
            errorMessage = "Mã PIN không được để trống.";
            return false;
        }

        if (pin.Length < MinLength || pin.Length > MaxLength)
        {
            errorMessage = $"Mã PIN phải có từ {MinLength} đến {MaxLength} chữ số.";
            return false;
        }

        foreach (char character in pin)
        {
            if (character < '0' || character > '9')
            {
                errorMessage = "Mã PIN chỉ được chứa chữ số (0-9).";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }
}
