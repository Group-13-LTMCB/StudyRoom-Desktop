using StudyRoom.Core.Interfaces;

namespace StudyRoom.Application.Engines;

public class TokenBucketRateLimiter : IRateLimiter
{
    private readonly double _capacity;
    private readonly double _refillRatePerSecond;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();

    private double _availableTokens;
    private DateTimeOffset _lastRefill;

    public TokenBucketRateLimiter(
        double capacity = 5.0,
        double refillRatePerSecond = 1.0,
        TimeProvider? timeProvider = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        }

        if (refillRatePerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(refillRatePerSecond), "Refill rate must be positive.");
        }

        _capacity = capacity;
        _refillRatePerSecond = refillRatePerSecond;
        _availableTokens = capacity;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lastRefill = _timeProvider.GetUtcNow();
    }

    public bool TryAcquire()
    {
        lock (_lock)
        {
            Refill();

            if (_availableTokens >= 1.0)
            {
                _availableTokens -= 1.0;
                return true;
            }

            return false;
        }
    }

    public TimeSpan GetRetryAfter()
    {
        lock (_lock)
        {
            Refill();

            if (_availableTokens >= 1.0)
            {
                return TimeSpan.Zero;
            }

            double needed = 1.0 - _availableTokens;
            double seconds = needed / _refillRatePerSecond;
            return TimeSpan.FromSeconds(Math.Max(0, seconds));
        }
    }

    private void Refill()
    {
        var now = _timeProvider.GetUtcNow();
        double elapsedSeconds = (now - _lastRefill).TotalSeconds;

        if (elapsedSeconds > 0)
        {
            _availableTokens = Math.Min(_capacity, _availableTokens + (elapsedSeconds * _refillRatePerSecond));
            _lastRefill = now;
        }
    }
}
