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
        Assert.Equal("access_token_xyz", result.Value.AccessToken);
        Assert.Equal(idToken, result.Value.IdToken);
        Assert.Equal("refresh_token_abc", result.Value.RefreshToken);
        Assert.Equal("user_123", result.Value.UserId);
        Assert.Equal("student@studyroom.local", result.Value.Email);
        Assert.Equal("Student A", result.Value.DisplayName);
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
        Assert.Equal("custom_username_42", result.Value!.UserId);
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
}
