using Genetec.Mcp.SecurityCenter.Authentication;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Authentication;

public class ClientRegistrationEndpointTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClientCredentialsRegistration_IsRejectedEvenWithAuthorizationCode(bool mixed)
    {
        var (client, error) = ClientRegistrationEndpoint.ValidateAndRegister(new ClientRegistrationRequest
        {
            RedirectUris = ["https://example.com/cb"],
            GrantTypes = mixed ? ["authorization_code", "client_credentials"] : ["client_credentials"]
        }, new ClientRegistrationStore());
        Assert.Null(client);
        Assert.Equal("invalid_client_metadata", error!.Error);
    }

    [Fact]
    public void ValidateAndRegister_ValidRequest_ReturnsClient()
    {
        var store = new ClientRegistrationStore();
        var body = new ClientRegistrationRequest
        {
            RedirectUris = ["https://example.com/callback"],
            GrantTypes = ["authorization_code"],
            TokenEndpointAuthMethod = "client_secret_post",
            ClientName = "Test"
        };

        var (client, error) = ClientRegistrationEndpoint.ValidateAndRegister(body, store);

        Assert.NotNull(client);
        Assert.Null(error);
        Assert.StartsWith("dyn-", client.ClientId);
    }

    [Fact]
    public void ValidateAndRegister_MissingRedirectUris_ReturnsError()
    {
        var store = new ClientRegistrationStore();
        var body = new ClientRegistrationRequest
        {
            RedirectUris = null,
            GrantTypes = ["authorization_code"]
        };

        var (client, error) = ClientRegistrationEndpoint.ValidateAndRegister(body, store);

        Assert.Null(client);
        Assert.NotNull(error);
        Assert.Equal("invalid_client_metadata", error.Error);
        Assert.Contains("redirect_uris", error.ErrorDescription);
    }

    [Fact]
    public void ValidateAndRegister_EmptyRedirectUris_ReturnsError()
    {
        var store = new ClientRegistrationStore();
        var body = new ClientRegistrationRequest
        {
            RedirectUris = [],
            GrantTypes = ["authorization_code"]
        };

        var (client, error) = ClientRegistrationEndpoint.ValidateAndRegister(body, store);

        Assert.Null(client);
        Assert.NotNull(error);
        Assert.Equal("invalid_client_metadata", error.Error);
    }

    [Fact]
    public void ValidateAndRegister_InvalidGrantType_ReturnsError()
    {
        var store = new ClientRegistrationStore();
        var body = new ClientRegistrationRequest
        {
            RedirectUris = ["https://example.com/callback"],
            GrantTypes = ["implicit"]
        };

        var (client, error) = ClientRegistrationEndpoint.ValidateAndRegister(body, store);

        Assert.Null(client);
        Assert.NotNull(error);
        Assert.Contains("grant_types", error.ErrorDescription);
    }

    [Fact]
    public void ValidateAndRegister_InvalidAuthMethod_ReturnsError()
    {
        var store = new ClientRegistrationStore();
        var body = new ClientRegistrationRequest
        {
            RedirectUris = ["https://example.com/callback"],
            TokenEndpointAuthMethod = "client_secret_basic"
        };

        var (client, error) = ClientRegistrationEndpoint.ValidateAndRegister(body, store);

        Assert.Null(client);
        Assert.NotNull(error);
        Assert.Contains("token_endpoint_auth_method", error.ErrorDescription);
    }

    [Fact]
    public void ValidateAndRegister_DefaultsGrantTypes_WhenNull()
    {
        var store = new ClientRegistrationStore();
        var body = new ClientRegistrationRequest
        {
            RedirectUris = ["https://example.com/callback"],
            GrantTypes = null
        };

        var (client, error) = ClientRegistrationEndpoint.ValidateAndRegister(body, store);

        Assert.NotNull(client);
        Assert.Null(error);
        Assert.Equal(["authorization_code"], client.GrantTypes);
    }

    [Fact]
    public void ValidateAndRegister_DefaultsAuthMethod_WhenNull()
    {
        var store = new ClientRegistrationStore();
        var body = new ClientRegistrationRequest
        {
            RedirectUris = ["https://example.com/callback"],
            TokenEndpointAuthMethod = null
        };

        var (client, error) = ClientRegistrationEndpoint.ValidateAndRegister(body, store);

        Assert.NotNull(client);
        Assert.Null(error);
        Assert.Equal("client_secret_post", client.TokenEndpointAuthMethod);
    }

    [Fact]
    public void ValidateAndRegister_InvalidRedirectUri_ReturnsError()
    {
        var store = new ClientRegistrationStore();
        var body = new ClientRegistrationRequest
        {
            RedirectUris = ["not-a-uri"]
        };

        var (client, error) = ClientRegistrationEndpoint.ValidateAndRegister(body, store);

        Assert.Null(client);
        Assert.NotNull(error);
        Assert.Contains("redirect_uris", error.ErrorDescription);
    }
}
