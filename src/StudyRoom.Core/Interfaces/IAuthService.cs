using StudyRoom.Core.Common;
using StudyRoom.Core.Models;

namespace StudyRoom.Core.Interfaces;

public interface IAuthService
{
    Task<Result<AuthTokens>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<bool>> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken = default);
    Task<Result<bool>> ConfirmSignUpAsync(string email, string confirmationCode, CancellationToken cancellationToken = default);
    Task<Result<bool>> ResendConfirmationCodeAsync(string email, CancellationToken cancellationToken = default);
    Task<Result<bool>> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task<Result<bool>> ConfirmForgotPasswordAsync(string email, string confirmationCode, string newPassword, CancellationToken cancellationToken = default);
    Task<Result<AuthTokens>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
