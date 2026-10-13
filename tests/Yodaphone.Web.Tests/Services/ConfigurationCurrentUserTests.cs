using Microsoft.Extensions.Configuration;
using Xunit;
using Yodaphone.Web.Services;

namespace Yodaphone.Web.Tests.Services;

public sealed class ConfigurationCurrentUserTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData("42", 42)]
    public void Constructor_UsesConfiguredUserOrDefault(string? configured, int expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Chat:DevelopmentUserId"] = configured }).Build();
        Assert.Equal(expected, new ConfigurationCurrentUser(configuration).UserId);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-an-integer")]
    public void Constructor_RejectsInvalidUserId(string configured)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Chat:DevelopmentUserId"] = configured }).Build();
        Assert.Throws<InvalidOperationException>(() => new ConfigurationCurrentUser(configuration));
    }

    [Fact]
    public void Constructor_RejectsNullConfiguration() =>
        Assert.Throws<ArgumentNullException>(() => new ConfigurationCurrentUser(null!));
}
