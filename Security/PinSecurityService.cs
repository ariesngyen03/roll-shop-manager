using System.Globalization;
using Microsoft.Data.Sqlite;
using RollShopManager.Database;
using RollShopManager.Models;

namespace RollShopManager.Security;

/// <summary>
/// Kiểm tra và thay đổi mã PIN quản lý lưu trong bảng AppSettings (dạng SHA256 + Salt).
/// </summary>
public sealed class PinSecurityService
{
    private readonly AppSettingsRepository _settingsRepository;
    private readonly AuditLogRepository _auditLogRepository;

    public PinSecurityService()
        : this(new AppSettingsRepository(), new AuditLogRepository())
    {
    }

    public PinSecurityService(AppSettingsRepository settingsRepository, AuditLogRepository auditLogRepository)
    {
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
    }

    public bool IsPinConfigured()
    {
        string? hash = _settingsRepository.Get(AppSettingKeys.AdminPinHash);
        string? salt = _settingsRepository.Get(AppSettingKeys.AdminPinSalt);
        return !string.IsNullOrWhiteSpace(hash) && !string.IsNullOrWhiteSpace(salt);
    }

    /// <summary>Kiểm tra PIN nhập vào có khớp PIN quản lý đang lưu hay không.</summary>
    public bool VerifyPin(string? pin)
    {
        if (!PinPolicy.IsValidFormat(pin, out _))
        {
            return false;
        }

        string? hash = _settingsRepository.Get(AppSettingKeys.AdminPinHash);
        string? salt = _settingsRepository.Get(AppSettingKeys.AdminPinSalt);
        string? iterationsText = _settingsRepository.Get(AppSettingKeys.AdminPinIterations);

        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt))
        {
            throw new InvalidOperationException(
                "Mã PIN quản lý chưa được khởi tạo trong CSDL. Hãy chạy DbInitializer.Initialize() trước.");
        }

        int iterations = int.TryParse(iterationsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : PinHasher.DefaultIterations;

        return PinHasher.Verify(pin, salt, hash, iterations);
    }

    /// <summary>true nếu PIN quản lý vẫn là PIN mặc định "123456" (nên nhắc Admin đổi PIN).</summary>
    public bool IsUsingDefaultPin()
    {
        return VerifyPin(PinPolicy.DefaultPin);
    }

    /// <summary>Đổi PIN quản lý. Chỉ thực hiện được khi đang ở quyền Admin.</summary>
    public void ChangePin(string currentPin, string newPin, string confirmNewPin)
    {
        AuthorizationGuard.EnsureAdmin("Đổi mã PIN quản lý");

        if (!string.Equals(newPin, confirmNewPin, StringComparison.Ordinal))
        {
            throw new ArgumentException("Mã PIN mới và mã PIN xác nhận không khớp nhau.", nameof(confirmNewPin));
        }

        if (!PinPolicy.IsValidFormat(newPin, out string formatError))
        {
            throw new ArgumentException(formatError, nameof(newPin));
        }

        if (string.Equals(currentPin, newPin, StringComparison.Ordinal))
        {
            throw new ArgumentException("Mã PIN mới phải khác mã PIN hiện tại.", nameof(newPin));
        }

        if (!VerifyPin(currentPin))
        {
            throw new PermissionDeniedException("Mã PIN hiện tại không đúng.");
        }

        PinHashResult newHash = PinHasher.CreateHash(newPin);

        using SqliteConnection connection = DbConnectionFactory.CreateOpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        StorePinHash(_settingsRepository, newHash, connection, transaction);
        _auditLogRepository.Insert(new AuditLogEntity
        {
            ActionTime = DateTime.Now,
            ActionType = AuditActionTypes.ChangePin,
            TargetEntity = "AppSettings",
            Details = $"[{Environment.MachineName}] Admin đã đổi mã PIN quản lý."
        }, connection, transaction);

        transaction.Commit();
        AppSession.RegisterActivity();
    }

    /// <summary>Ghi Hash / Salt / Iterations của PIN vào AppSettings trong transaction được truyền vào.</summary>
    public static void StorePinHash(
        AppSettingsRepository settingsRepository,
        PinHashResult hash,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(settingsRepository);
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        settingsRepository.Set(AppSettingKeys.AdminPinHash, hash.HashBase64, connection, transaction);
        settingsRepository.Set(AppSettingKeys.AdminPinSalt, hash.SaltBase64, connection, transaction);
        settingsRepository.Set(AppSettingKeys.AdminPinIterations,
            hash.Iterations.ToString(CultureInfo.InvariantCulture), connection, transaction);
        settingsRepository.Set(AppSettingKeys.AdminPinUpdatedAt,
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), connection, transaction);
    }
}
