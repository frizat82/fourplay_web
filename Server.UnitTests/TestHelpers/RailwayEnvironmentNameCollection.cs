using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Every test class that mutates the process-global RAILWAY_ENVIRONMENT_NAME environment variable
/// must share this collection — xUnit runs different test classes in parallel by default, and two
/// classes each setting/clearing the same env var race (the exact bug class already fixed once for
/// VAPID_* in WebPushSenderTests/NotificationsControllerTests — see frizat-tgk Phase 1). Shared by
/// GoogleEmailSenderTests (pre-existing) and NotificationDispatcherTests (frizat-tgk Phase 2),
/// since both independently read this same var via the exact same IsProductionEnvironment pattern.
/// </summary>
[CollectionDefinition(Name)]
public sealed class RailwayEnvironmentNameCollection
{
    public const string Name = "RailwayEnvironmentName";
}
