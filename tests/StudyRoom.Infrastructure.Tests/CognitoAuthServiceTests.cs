using System.Text;
using System.Text.Json;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;
using Moq;
using StudyRoom.Infrastructure.Auth;
using Xunit;

namespace StudyRoom.Infrastructure.Tests;

public class CognitoAuthServiceTests
{
    private readonly Mock<IAmazonCognitoIdentityProvider> _cognitoMock = new();
    private readonly CognitoAuthService _service;

    public CognitoAuthServiceTests()
    {
        _service = new CognitoAuthService(_cognitoMock.Object, "test-client-id");
    }

    private static string CreateSampleJwt(string sub, string email, string name)
    {
        string header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            sub,
            email,
            name
        }))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{header}.{payload}.sample_signature";
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsSuccessAndTokens()
    {
        string idToken = CreateSampleJwt("user_123", "student@studyroom.local", "Student A");

        _cognitoMock.Setup(c => c.InitiateAuthAsync(It.Is<InitiateAuthRequest>(r =>
            r.AuthFlow == AuthFlowType.USER_PASSWORD_AUTH &&
            r.AuthParameters["USERNAME"] == "student@studyroom.local" &&
            r.AuthParameters["PASSWORD"] == "Password123!"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateAuthResponse
            {
                AuthenticationResult = new AuthenticationResultType
                {
                    AccessToken = "access_token_xyz",
                    IdToken = idToken,
                    RefreshToken = "refresh_token_abc",
                    ExpiresIn = 3600
                }
            });

        var result = await _service.LoginAsync("student@studyroom.local", "Password123!");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.Tokens);
        Assert.False(result.Value.RequiresMfa);
        Assert.Equal("access_token_xyz", result.Value.Tokens.AccessToken);
        Assert.Equal(idToken, result.Value.Tokens.IdToken);
        Assert.Equal("refresh_token_abc", result.Value.Tokens.RefreshToken);
        Assert.Equal("user_123", result.Value.Tokens.UserId);
        Assert.Equal("student@studyroom.local", result.Value.Tokens.Email);
        Assert.Equal("Student A", result.Value.Tokens.DisplayName);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("email@test.com", "")]
    [InlineData("   ", "   ")]
    public async Task LoginAsync_EmptyCredentials_ReturnsFailure(string email, string password)
    {
        var result = await _service.LoginAsync(email, password);

        Assert.True(result.IsFailure);
        Assert.Contains("không được để trống", result.Error);
    }

    [Fact]
    public async Task LoginAsync_CognitoThrowsNotAuthorized_ReturnsFailure()
    {
        _cognitoMock.Setup(c => c.InitiateAuthAsync(It.IsAny<InitiateAuthRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotAuthorizedException("Incorrect username or password."));

        var result = await _service.LoginAsync("wrong@test.com", "badpass");

        Assert.True(result.IsFailure);
        Assert.Contains("Incorrect username or password.", result.Error);
    }

    [Fact]
    public async Task RegisterAsync_ValidInput_ReturnsSuccess()
    {
        _cognitoMock.Setup(c => c.SignUpAsync(It.Is<SignUpRequest>(r =>
            r.Username == "newuser@test.com" &&
            r.Password == "SecurePass123!" &&
            r.UserAttributes.Exists(a => a.Name == "name" && a.Value == "New Student")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignUpResponse
            {
                UserConfirmed = true,
                UserSub = "sub_999"
            });

        var result = await _service.RegisterAsync("newuser@test.com", "SecurePass123!", "New Student");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Theory]
    [InlineData("", "pass", "Name")]
    [InlineData("email@test.com", "", "Name")]
    [InlineData("email@test.com", "pass", "")]
    [InlineData("email@test.com", "pass", null)]
    public async Task RegisterAsync_EmptyInput_ReturnsFailure(string? email, string? password, string? displayName)
    {
        var result = await _service.RegisterAsync(email!, password!, displayName!);

        Assert.True(result.IsFailure);
        Assert.Contains("không được để trống", result.Error);
    }

    [Fact]
    public async Task LoginAsync_TokenWithCognitoUsername_ExtractsSubOrUsername()
    {
        string header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            cognito_username = "custom_username_42",
            email = "user@studyroom.local",
            name = "Custom User"
        }).Replace("cognito_username", "cognito:username"))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string idToken = $"{header}.{payload}.sig";

        _cognitoMock.Setup(c => c.InitiateAuthAsync(It.IsAny<InitiateAuthRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateAuthResponse
            {
                AuthenticationResult = new AuthenticationResultType
                {
                    AccessToken = "acc",
                    IdToken = idToken,
                    RefreshToken = "ref",
                    ExpiresIn = 3600
                }
            });

        var result = await _service.LoginAsync("user@studyroom.local", "pass");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value?.Tokens);
        Assert.Equal("custom_username_42", result.Value!.Tokens!.UserId);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ReturnsTokens()
    {
        _cognitoMock.Setup(c => c.InitiateAuthAsync(It.Is<InitiateAuthRequest>(r =>
            r.AuthFlow == AuthFlowType.REFRESH_TOKEN_AUTH &&
            r.AuthParameters["REFRESH_TOKEN"] == "valid_refresh_token"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateAuthResponse
            {
                AuthenticationResult = new AuthenticationResultType
                {
                    AccessToken = "new_access_token",
                    IdToken = "new_id_token",
                    RefreshToken = "valid_refresh_token",
                    ExpiresIn = 3600
                }
            });

        var result = await _service.RefreshTokenAsync("valid_refresh_token");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("new_access_token", result.Value.AccessToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_EmptyToken_ReturnsFailure()
    {
        var result = await _service.RefreshTokenAsync(string.Empty);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task LogoutAsync_CompletesSuccessfully()
    {
        await _service.LogoutAsync();
    }

    [Fact]
    public async Task LoginAsync_WhenMfaChallenge_ReturnsRequiresMfaAndSession()
    {
        _cognitoMock.Setup(c => c.InitiateAuthAsync(It.IsAny<InitiateAuthRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateAuthResponse
            {
                ChallengeName = ChallengeNameType.SOFTWARE_TOKEN_MFA,
                Session = "mfa_challenge_session_456"
            });

        var result = await _service.LoginAsync("user@studyroom.local", "Pass123!");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.RequiresMfa);
        Assert.Equal("mfa_challenge_session_456", result.Value.MfaSession);
        Assert.Null(result.Value.Tokens);
    }

    [Fact]
    public async Task RespondToMfaChallengeAsync_ValidCode_ReturnsTokens()
    {
        string idToken = CreateSampleJwt("user_123", "mfa@studyroom.local", "Mfa User");

        _cognitoMock.Setup(c => c.RespondToAuthChallengeAsync(It.Is<RespondToAuthChallengeRequest>(r =>
            r.ChallengeName == ChallengeNameType.SOFTWARE_TOKEN_MFA &&
            r.ChallengeResponses["SOFTWARE_TOKEN_MFA_CODE"] == "123456" &&
            r.Session == "session_123"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RespondToAuthChallengeResponse
            {
                AuthenticationResult = new AuthenticationResultType
                {
                    AccessToken = "mfa_access_token",
                    IdToken = idToken,
                    RefreshToken = "mfa_refresh_token",
                    ExpiresIn = 3600
                }
            });

        var result = await _service.RespondToMfaChallengeAsync("mfa@studyroom.local", "123456", "session_123");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("mfa_access_token", result.Value.AccessToken);
    }

    [Fact]
    public async Task SetupTotpMfaAsync_ValidAccessToken_ReturnsSecretCode()
    {
        _cognitoMock.Setup(c => c.AssociateSoftwareTokenAsync(It.IsAny<AssociateSoftwareTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssociateSoftwareTokenResponse
            {
                SecretCode = "TOTPSECRETKEYXYZ123"
            });

        var result = await _service.SetupTotpMfaAsync("valid_access_token");

        Assert.True(result.IsSuccess);
        Assert.Equal("TOTPSECRETKEYXYZ123", result.Value);
    }

    [Fact]
    public async Task VerifyTotpMfaAsync_SuccessCode_SetsMfaPreferenceAndReturnsTrue()
    {
        _cognitoMock.Setup(c => c.VerifySoftwareTokenAsync(It.Is<VerifySoftwareTokenRequest>(r =>
            r.UserCode == "123456"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifySoftwareTokenResponse
            {
                Status = VerifySoftwareTokenResponseType.SUCCESS
            });

        _cognitoMock.Setup(c => c.SetUserMFAPreferenceAsync(It.IsAny<SetUserMFAPreferenceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SetUserMFAPreferenceResponse());

        var result = await _service.VerifyTotpMfaAsync("valid_access_token", "123456");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task ChangePasswordAsync_ValidInput_ReturnsSuccess()
    {
        _cognitoMock.Setup(c => c.ChangePasswordAsync(It.Is<ChangePasswordRequest>(r =>
            r.AccessToken == "valid_token" &&
            r.PreviousPassword == "OldPass123!" &&
            r.ProposedPassword == "NewPass123!"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChangePasswordResponse());

        var result = await _service.ChangePasswordAsync("valid_token", "OldPass123!", "NewPass123!");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Theory]
    [InlineData("", "OldPass123!", "NewPass123!")]
    [InlineData("valid_token", "", "NewPass123!")]
    [InlineData("valid_token", "OldPass123!", "")]
    public async Task ChangePasswordAsync_MissingInput_ReturnsFailure(string token, string oldPass, string newPass)
    {
        var result = await _service.ChangePasswordAsync(token, oldPass, newPass);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ChangePasswordAsync_ShortPassword_ReturnsFailure()
    {
        var result = await _service.ChangePasswordAsync("valid_token", "OldPass123!", "short");

        Assert.True(result.IsFailure);
        Assert.Contains("ít nhất 8 ký tự", result.Error);
    }

    [Fact]
    public async Task ChangePasswordAsync_SameOldAndNewPassword_ReturnsFailure()
    {
        var result = await _service.ChangePasswordAsync("valid_token", "SamePass123!", "SamePass123!");

        Assert.True(result.IsFailure);
        Assert.Contains("không được trùng", result.Error);
    }

    [Fact]
    public async Task ChangePasswordAsync_CognitoException_ReturnsFailure()
    {
        _cognitoMock.Setup(c => c.ChangePasswordAsync(It.IsAny<ChangePasswordRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotAuthorizedException("Current password is incorrect."));

        var result = await _service.ChangePasswordAsync("valid_token", "WrongOld123!", "NewPass123!");

        Assert.True(result.IsFailure);
        Assert.Contains("Current password is incorrect.", result.Error);
    }

    [Fact]
    public async Task GetMfaStatusAsync_EnabledInMfaSettingsList_ReturnsTrue()
    {
        _cognitoMock.Setup(c => c.GetUserAsync(It.Is<GetUserRequest>(r => r.AccessToken == "valid_token"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserResponse
            {
                UserMFASettingList = new List<string> { "SOFTWARE_TOKEN_MFA" }
            });

        var result = await _service.GetMfaStatusAsync("valid_token");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task GetMfaStatusAsync_EnabledInPreferredMfaSetting_ReturnsTrue()
    {
        _cognitoMock.Setup(c => c.GetUserAsync(It.Is<GetUserRequest>(r => r.AccessToken == "valid_token"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserResponse
            {
                PreferredMfaSetting = "SOFTWARE_TOKEN_MFA"
            });

        var result = await _service.GetMfaStatusAsync("valid_token");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task GetMfaStatusAsync_Disabled_ReturnsFalse()
    {
        _cognitoMock.Setup(c => c.GetUserAsync(It.Is<GetUserRequest>(r => r.AccessToken == "valid_token"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserResponse
            {
                UserMFASettingList = new List<string>()
            });

        var result = await _service.GetMfaStatusAsync("valid_token");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task GetMfaStatusAsync_EmptyToken_ReturnsFailure()
    {
        var result = await _service.GetMfaStatusAsync(string.Empty);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetMfaStatusAsync_CognitoException_ReturnsFailure()
    {
        _cognitoMock.Setup(c => c.GetUserAsync(It.IsAny<GetUserRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotAuthorizedException("Access Token has expired."));

        var result = await _service.GetMfaStatusAsync("expired_token");

        Assert.True(result.IsFailure);
        Assert.Contains("Access Token has expired.", result.Error);
    }
}
