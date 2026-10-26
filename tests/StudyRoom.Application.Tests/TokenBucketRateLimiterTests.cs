using StudyRoom.Application.Engines;

namespace StudyRoom.Application.Tests;

public class TokenBucketRateLimiterTests
{
    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public TestTimeProvider(DateTimeOffset initialTime)
        {
            _utcNow = initialTime;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }

    [Fact]
    public void TryAcquire_AllowsBurstUpToCapacity_ThenBlocks()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new TokenBucketRateLimiter(capacity: 5, refillRatePerSecond: 1, timeProvider);

        for (int i = 0; i < 5; i++)
        {
            Assert.True(limiter.TryAcquire(), $"Acquire {i + 1} should succeed");
        }

        Assert.False(limiter.TryAcquire(), "Acquire 6 should fail because bucket is empty");
    }

    [Fact]
    public void TryAcquire_AfterRefillTime_AllowsNewAcquisitions()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new TokenBucketRateLimiter(capacity: 3, refillRatePerSecond: 1, timeProvider);

        // Exhaust bucket
        Assert.True(limiter.TryAcquire());
        Assert.True(limiter.TryAcquire());
        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());

        // Advance 1 second -> 1 token refilled
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        Assert.True(limiter.TryAcquire(), "Should allow 1 acquire after 1 second refill");
        Assert.False(limiter.TryAcquire(), "Should block after consuming the 1 refilled token");
    }

    [Fact]
    public void GetRetryAfter_WhenExhausted_ReturnsExpectedDuration()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new TokenBucketRateLimiter(capacity: 2, refillRatePerSecond: 1, timeProvider);

        Assert.True(limiter.TryAcquire());
        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());

        var retryAfter = limiter.GetRetryAfter();
        Assert.InRange(retryAfter.TotalSeconds, 0.9, 1.1);
    }

    [Fact]
    public void AvailableTokens_DoNotExceedCapacity_WhenIdle()
    {
        var timeProvider = new TestTimeProvider(DateTimeOffset.UtcNow);
        var limiter = new TokenBucketRateLimiter(capacity: 2, refillRatePerSecond: 1, timeProvider);

        // Idle for 100 seconds
        timeProvider.Advance(TimeSpan.FromSeconds(100));

        Assert.True(limiter.TryAcquire());
        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire(), "Tokens should be capped at capacity (2)");
    }
}
