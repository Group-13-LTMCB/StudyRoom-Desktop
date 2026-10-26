using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;

namespace StudyRoom.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiSecureStorageService : ISecureStorageService
{
    private readonly string _filePath;

    public DpapiSecureStorageService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StudyRoom",
            "session.dat");
    }

    public async Task SaveSessionAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        byte[] plainBytes = JsonSerializer.SerializeToUtf8Bytes(session);
        byte[] cipherBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

        await File.WriteAllBytesAsync(_filePath, cipherBytes, cancellationToken);
    }

    public async Task<AuthSession?> LoadSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            byte[] cipherBytes = await File.ReadAllBytesAsync(_filePath, cancellationToken);
            byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<AuthSession>(plainBytes);
        }
        catch
        {
            return null;
        }
    }

    public Task ClearSessionAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }

        return Task.CompletedTask;
    }
}
