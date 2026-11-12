namespace StudyRoom.Core.Models;

public sealed record LoginResult
{
    public bool RequiresMfa { get; init; }
    public string? MfaSession { get; init; }
    public AuthTokens? Tokens { get; init; }

    public static LoginResult Success(AuthTokens tokens) => new() { Tokens = tokens };
    public static LoginResult Challenge(string mfaSession) => new() { RequiresMfa = true, MfaSession = mfaSession };
}
