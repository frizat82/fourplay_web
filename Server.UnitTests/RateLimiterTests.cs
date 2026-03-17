using System.Threading.RateLimiting;

namespace FourPlayWebApp.Server.UnitTests;

public class RateLimiterTests
{
    [Fact]
    public async Task LoginLimiter_Allows_PermitLimit_Then_Denies()
    {
        var options = new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        };

        await using var limiter = new FixedWindowRateLimiter(options);

        for (int i = 0; i < options.PermitLimit; i++)
        {
            var lease = await limiter.AcquireAsync(1);
            Assert.True(lease.IsAcquired, $"Expected permit {i + 1} to be acquired");
        }

        var denied = await limiter.AcquireAsync(1);
        Assert.False(denied.IsAcquired, "Expected the limiter to deny the request after permits exhausted");
    }

    [Fact]
    public async Task RegisterLimiter_Allows_3_Then_Denies()
    {
        var options = new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(5),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        };

        await using var limiter = new FixedWindowRateLimiter(options);

        for (int i = 0; i < options.PermitLimit; i++)
        {
            var lease = await limiter.AcquireAsync(1);
            Assert.True(lease.IsAcquired);
        }

        var denied = await limiter.AcquireAsync(1);
        Assert.False(denied.IsAcquired);
    }

    [Fact]
    public async Task ForgotLimiter_Allows_3_Then_Denies()
    {
        var options = new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromHours(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        };

        await using var limiter = new FixedWindowRateLimiter(options);

        for (int i = 0; i < options.PermitLimit; i++)
        {
            var lease = await limiter.AcquireAsync(1);
            Assert.True(lease.IsAcquired);
        }

        var denied = await limiter.AcquireAsync(1);
        Assert.False(denied.IsAcquired);
    }
}

