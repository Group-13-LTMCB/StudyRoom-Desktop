using System.Runtime.Versioning;
using System.Text;
using StudyRoom.Core.Models;
using StudyRoom.Infrastructure.Security;

namespace StudyRoom.Infrastructure.Tests;

[SupportedOSPlatform("windows")]
public class DpapiSecureStorageServiceTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly DpapiSecureStorageService _service;

    public DpapiSecureStorageServiceTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"studyroom_test_{Guid.NewGuid():N}.dat");
        _service = new DpapiSecureStorageService(_testFilePath);
    }

    [Fact]
    public async Task SaveSessionAsync_And_LoadSessionAsync_ShouldPersistAndRestoreSession()
    {
        var session = new AuthSession
        {
            UserId = "user-123",
            Email = "student@studyroom.test",
            DisplayName = "Student One",
            IdToken = "id-token-xyz-secret",
            AccessToken = "access-token-abc-secret",
            RefreshToken = "refresh-token-def-secret",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
        };

        await _service.SaveSessionAsync(session);
        var loaded = await _service.LoadSessionAsync();

        Assert.NotNull(loaded);
        Assert.Equal(session.UserId, loaded.UserId);
        Assert.Equal(session.Email, loaded.Email);
        Assert.Equal(session.DisplayName, loaded.DisplayName);
        Assert.Equal(session.IdToken, loaded.IdToken);
        Assert.Equal(session.AccessToken, loaded.AccessToken);
        Assert.Equal(session.RefreshToken, loaded.RefreshToken);
        Assert.Equal(session.ExpiresAtUtc.ToUnixTimeSeconds(), loaded.ExpiresAtUtc.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task SaveSessionAsync_ShouldNotStorePlaintextTokensOnDisk()
    {
        var session = new AuthSession
        {
            UserId = "secret-user-id",
            Email = "supersecret@studyroom.test",
            DisplayName = "Secret User",
            IdToken = "VERY_CONFIDENTIAL_ID_TOKEN",
            AccessToken = "VERY_CONFIDENTIAL_ACCESS_TOKEN",
            RefreshToken = "VERY_CONFIDENTIAL_REFRESH_TOKEN",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
        };

        await _service.SaveSessionAsync(session);

        byte[] rawBytes = await File.ReadAllBytesAsync(_testFilePath);
        string rawString = Encoding.UTF8.GetString(rawBytes);

        Assert.DoesNotContain("VERY_CONFIDENTIAL_ID_TOKEN", rawString);
        Assert.DoesNotContain("VERY_CONFIDENTIAL_ACCESS_TOKEN", rawString);
        Assert.DoesNotContain("VERY_CONFIDENTIAL_REFRESH_TOKEN", rawString);
    }

    [Fact]
    public async Task ClearSessionAsync_ShouldDeleteFileAndMakeLoadReturnNull()
    {
        var session = new AuthSession
        {
            UserId = "user-456",
            Email = "test@studyroom.test",
            DisplayName = "Test",
            IdToken = "id",
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        await _service.SaveSessionAsync(session);
        Assert.True(File.Exists(_testFilePath));

        await _service.ClearSessionAsync();
        Assert.False(File.Exists(_testFilePath));

        var loaded = await _service.LoadSessionAsync();
        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadSessionAsync_WhenFileDoesNotExist_ShouldReturnNull()
    {
        var loaded = await _service.LoadSessionAsync();
        Assert.Null(loaded);
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            try
            {
                File.Delete(_testFilePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
