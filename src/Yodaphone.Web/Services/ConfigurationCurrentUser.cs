using Microsoft.Extensions.Configuration;

namespace Yodaphone.Web.Services;

/// <summary>
/// Development-only current user source. Replace this with a claims-backed
/// implementation once application authentication is enabled.
/// </summary>
public sealed class ConfigurationCurrentUser : ICurrentUser
{
    public ConfigurationCurrentUser(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredId = configuration.GetValue<int?>("Chat:DevelopmentUserId") ?? 1;
        if (configuredId <= 0)
        {
            throw new InvalidOperationException("Chat:DevelopmentUserId must be a positive integer.");
        }

        UserId = configuredId;
    }

    public int UserId { get; }
}
