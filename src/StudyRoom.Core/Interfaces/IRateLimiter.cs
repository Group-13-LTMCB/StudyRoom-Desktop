namespace StudyRoom.Core.Interfaces;

public interface IRateLimiter
{
    bool TryAcquire();
    TimeSpan GetRetryAfter();
}
