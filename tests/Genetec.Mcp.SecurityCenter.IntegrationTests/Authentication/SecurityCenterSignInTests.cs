using Genetec.Mcp.SecurityCenter.Client;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Authentication;

public class SecurityCenterSignInTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task ValidateConnection_ConfiguredLiveCredentialsSucceed()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var (success, error) = await sc.ValidateConfiguredConnection();

        Assert.True(success, error);
        Assert.Null(error);
        var session = sc.Manager.GetClient().Session;
        var credentials = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(session.AuthHeader["Basic ".Length..]));
        Assert.Contains($";{SecurityCenterSession.ApplicationId}:", credentials);
    }
}
