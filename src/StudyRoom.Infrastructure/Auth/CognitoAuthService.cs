using System.Text.Json;
using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;
using StudyRoom.Core.Common;
using StudyRoom.Core.Interfaces;
using StudyRoom.Core.Models;

namespace StudyRoom.Infrastructure.Auth;

public class CognitoAuthService : IAuthService, IDisposable
{
    private readonly IAmazonCognitoIdentityProvider _cognitoClient;
    private readonly string _clientId;
    private readonly bool _disposeClient;

    public CognitoAuthService(
        IAmazonCognitoIdentityProvider? cognitoClient = null,
        string clientId = "studyroom-desktop-client",
        RegionEndpoint? region = null)
    {
        _clientId = clientId;
        if (cognitoClient != null)
        {
            _cognitoClient = cognitoClient;
            _disposeClient = false;
        }
        else
        {
            _cognitoClient = new AmazonCognitoIdentityProviderClient(
                new AnonymousAWSCredentials(),
                region ?? RegionEndpoint.USEast1);
            _disposeClient = true;
        }
    }

    public async Task<Result<AuthTokens>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return Result<AuthTokens>.Failure("Email và mật khẩu không được để trống.");
        }

        try
        {
            var request = new InitiateAuthRequest
            {
                AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                ClientId = _clientId,
                AuthParameters = new Dictionary<string, string>
                {
                    { "USERNAME", email.Trim() },
                    { "PASSWORD", password }
                }
            };

            var response = await _cognitoClient.InitiateAuthAsync(request, cancellationToken);
            if (response.AuthenticationResult == null)
            {
                return Result<AuthTokens>.Failure($"Yêu cầu xác thực bổ sung: {response.ChallengeName?.Value ?? "Không xác định"}");
            }

            var (userId, tokenEmail, displayName) = ExtractClaimsFromIdToken(response.AuthenticationResult.IdToken);

            int expiresIn = response.AuthenticationResult.ExpiresIn.GetValueOrDefault(3600);
            if (expiresIn <= 0) expiresIn = 3600;

            var tokens = new AuthTokens
            {
                AccessToken = response.AuthenticationResult.AccessToken ?? string.Empty,
                IdToken = response.AuthenticationResult.IdToken ?? string.Empty,
                RefreshToken = response.AuthenticationResult.RefreshToken ?? string.Empty,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
                UserId = userId ?? email.Trim(),
                Email = tokenEmail ?? email.Trim(),
                DisplayName = displayName ?? email.Trim().Split('@')[0]
            };

            return Result<AuthTokens>.Success(tokens);
        }
        catch (AmazonServiceException ex)
        {
            return Result<AuthTokens>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<AuthTokens>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(displayName))
        {
            return Result<bool>.Failure("Email, mật khẩu và họ tên không được để trống.");
        }

        try
        {
            var request = new SignUpRequest
            {
                ClientId = _clientId,
                Username = email.Trim(),
                Password = password,
                UserAttributes = new List<AttributeType>
                {
                    new() { Name = "email", Value = email.Trim() },
                    new() { Name = "name", Value = displayName.Trim() }
                }
            };

            var response = await _cognitoClient.SignUpAsync(request, cancellationToken);
            return Result<bool>.Success(response.UserConfirmed == true || !string.IsNullOrEmpty(response.UserSub));
        }
        catch (AmazonServiceException ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }

    public async Task<Result<AuthTokens>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result<AuthTokens>.Failure("Refresh token không hợp lệ.");
        }

        try
        {
            var request = new InitiateAuthRequest
            {
                AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
                ClientId = _clientId,
                AuthParameters = new Dictionary<string, string>
                {
                    { "REFRESH_TOKEN", refreshToken }
                }
            };

            var response = await _cognitoClient.InitiateAuthAsync(request, cancellationToken);
            if (response.AuthenticationResult == null)
            {
                return Result<AuthTokens>.Failure("Không thể làm mới phiên đăng nhập.");
            }

            var (userId, tokenEmail, displayName) = ExtractClaimsFromIdToken(response.AuthenticationResult.IdToken);

            int expiresIn = response.AuthenticationResult.ExpiresIn.GetValueOrDefault(3600);
            if (expiresIn <= 0) expiresIn = 3600;

            var tokens = new AuthTokens
            {
                AccessToken = response.AuthenticationResult.AccessToken ?? string.Empty,
                IdToken = response.AuthenticationResult.IdToken ?? string.Empty,
                RefreshToken = response.AuthenticationResult.RefreshToken ?? refreshToken,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
                UserId = userId,
                Email = tokenEmail,
                DisplayName = displayName
            };

            return Result<AuthTokens>.Success(tokens);
        }
        catch (AmazonServiceException ex)
        {
            return Result<AuthTokens>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<AuthTokens>.Failure(ex.Message);
        }
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private static (string? userId, string? email, string? displayName) ExtractClaimsFromIdToken(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return (null, null, null);
        }

        try
        {
            string[] parts = idToken.Split('.');
            if (parts.Length < 2)
            {
                return (null, null, null);
            }

            string payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            byte[] bytes = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(bytes);
            var root = doc.RootElement;
            string? sub = root.TryGetProperty("sub", out var subProp) ? subProp.GetString() : null;
            if (string.IsNullOrEmpty(sub) && root.TryGetProperty("cognito:username", out var userProp))
            {
                sub = userProp.GetString();
            }
            string? email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
            string? name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            return (sub, email, name);
        }
        catch
        {
            return (null, null, null);
        }
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _cognitoClient.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
