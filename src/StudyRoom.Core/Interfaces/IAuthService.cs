using StudyRoom.Core.Common;
using StudyRoom.Core.Models;

namespace StudyRoom.Core.Interfaces;

public interface IAuthService
{
    Task<Result<LoginResult>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<bool>> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken = default);
    Task<Result<bool>> ConfirmSignUpAsync(string email, string confirmationCode, CancellationToken cancellationToken = default);
    Task<Result<bool>> ResendConfirmationCodeAsync(string email, CancellationToken cancellationToken = default);
    Task<Result<bool>> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task<Result<bool>> ConfirmForgotPasswordAsync(string email, string confirmationCode, string newPassword, CancellationToken cancellationToken = default);
    Task<Result<AuthTokens>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<Result<AuthTokens>> RespondToMfaChallengeAsync(string email, string mfaCode, string mfaSession, CancellationToken cancellationToken = default);
    Task<Result<string>> SetupTotpMfaAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<Result<bool>> VerifyTotpMfaAsync(string accessToken, string totpCode, CancellationToken cancellationToken = default);
    Task<Result<bool>> SetMfaPreferenceAsync(string accessToken, bool enabled, CancellationToken cancellationToken = default);
    Task<Result<bool>> GetMfaStatusAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<Result<bool>> ChangePasswordAsync(string accessToken, string oldPassword, string newPassword, CancellationToken cancellationToken = default);
    Task LogoutAsync(string? accessToken = null, CancellationToken cancellationToken = default);
}
