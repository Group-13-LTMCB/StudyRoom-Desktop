using StudyRoom.Core.Models;

namespace StudyRoom.Core.Interfaces;

public interface ISecureStorageService
{
    Task SaveSessionAsync(AuthSession session, CancellationToken cancellationToken = default);
    Task<AuthSession?> LoadSessionAsync(CancellationToken cancellationToken = default);
    Task ClearSessionAsync(CancellationToken cancellationToken = default);
}
