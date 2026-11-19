using System.Security.Cryptography;
using System.Text;

namespace RollShopManager.Security;

/// <summary>Kết quả băm PIN: Hash, Salt (Base64) và số vòng lặp SHA256.</summary>
public sealed record PinHashResult(string HashBase64, string SaltBase64, int Iterations);

/// <summary>
/// Băm mã PIN bằng SHA256 kèm Salt ngẫu nhiên 16 byte.
/// Vòng 1: SHA256(salt || UTF8(pin)); các vòng tiếp theo: SHA256(hashTrước || salt).
/// Lặp nhiều vòng (key stretching) để làm chậm tấn công dò PIN nếu file DB bị sao chép ra ngoài.
/// </summary>
public static class PinHasher
{
    public const int SaltSizeBytes = 16;
    public const int DefaultIterations = 10_000;
    public const int MaxIterations = 1_000_000;

    public static byte[] GenerateSalt()
    {
        return RandomNumberGenerator.GetBytes(SaltSizeBytes);
    }

    public static PinHashResult CreateHash(string pin)
    {
        if (!PinPolicy.IsValidFormat(pin, out string error))
        {
            throw new ArgumentException(error, nameof(pin));
        }

        byte[] salt = GenerateSalt();
        byte[] hash = ComputeHashBytes(pin, salt, DefaultIterations);
        return new PinHashResult(Convert.ToBase64String(hash), Convert.ToBase64String(salt), DefaultIterations);
    }

    public static byte[] ComputeHashBytes(string pin, byte[] salt, int iterations)
    {
        ArgumentNullException.ThrowIfNull(pin);
        ArgumentNullException.ThrowIfNull(salt);
        if (salt.Length == 0)
        {
            throw new ArgumentException("Salt không được rỗng.", nameof(salt));
        }

        if (iterations < 1 || iterations > MaxIterations)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), iterations,
                $"Số vòng băm phải nằm trong khoảng 1 - {MaxIterations}.");
        }

        byte[] pinBytes = Encoding.UTF8.GetBytes(pin);
        byte[] firstBlock = new byte[salt.Length + pinBytes.Length];
        Buffer.BlockCopy(salt, 0, firstBlock, 0, salt.Length);
        Buffer.BlockCopy(pinBytes, 0, firstBlock, salt.Length, pinBytes.Length);

        byte[] hash = SHA256.HashData(firstBlock);
        byte[] roundBlock = new byte[hash.Length + salt.Length];

        try
        {
            for (int round = 1; round < iterations; round++)
            {
                Buffer.BlockCopy(hash, 0, roundBlock, 0, hash.Length);
                Buffer.BlockCopy(salt, 0, roundBlock, hash.Length, salt.Length);
                byte[] next = SHA256.HashData(roundBlock);
                CryptographicOperations.ZeroMemory(hash);
                hash = next;
            }

            return hash;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pinBytes);
            CryptographicOperations.ZeroMemory(firstBlock);
            CryptographicOperations.ZeroMemory(roundBlock);
        }
    }

    /// <summary>So sánh PIN nhập vào với hash đã lưu (so sánh thời gian hằng để chống tấn công timing).</summary>
    public static bool Verify(string? pin, string? saltBase64, string? expectedHashBase64, int iterations)
    {
        if (string.IsNullOrEmpty(pin) || string.IsNullOrWhiteSpace(saltBase64) || string.IsNullOrWhiteSpace(expectedHashBase64))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(saltBase64);
            expectedHash = Convert.FromBase64String(expectedHashBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expectedHash.Length != SHA256.HashSizeInBytes || iterations < 1 || iterations > MaxIterations)
        {
            return false;
        }

        byte[] actualHash = ComputeHashBytes(pin, salt, iterations);
        try
        {
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actualHash);
        }
    }
}
