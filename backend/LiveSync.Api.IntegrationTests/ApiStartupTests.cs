using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LiveSync.Api.IntegrationTests;

public class ApiStartupTests
{
    [Fact]
    public async Task RootEndpoint_ReturnsNotFound_WithTestConfiguration()
    {
        await using var factory = new LiveSyncApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class LiveSyncApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=livesync_tests;Username=test;Password=test",
                    ["Jwt:Secret"] = "this-is-a-minimum-32-byte-test-secret!",
                    ["Jwt:Issuer"] = "LiveSyncAuthAPI",
                    ["Jwt:Audience"] = "LiveSyncClient",
                    ["Database:MigrateOnStartup"] = "false"
                };

                configBuilder.AddInMemoryCollection(values);
            });
        }
    }
}
