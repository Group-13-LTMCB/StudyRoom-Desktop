namespace StudyRoom.Core.Models;

public sealed record AuthTokens
{
    public required string IdToken { get; init; }
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public string? UserId { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
}
