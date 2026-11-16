using System.Text.Json;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class AssuranceProfileListToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task List_ReturnsConfiguredProfilesWithIdsAndMatchingDetails()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);
        var json = Assert.IsType<string>(await AssuranceProfileListTool.Execute(sc.Manager));
        using var document = IntegrationTestHelpers.ParseJson(json);
        var profiles = IntegrationTestHelpers.GetResult(document.RootElement).GetProperty("GetAssuranceProfiles");
        Assert.Equal(JsonValueKind.Array, profiles.ValueKind);
        Assert.NotEmpty(profiles.EnumerateArray());
        var ids = new HashSet<Guid>();
        foreach (var profile in profiles.EnumerateArray())
        {
            var profileId = profile.GetProperty("ProfileId").GetGuid();
            Assert.NotEqual(Guid.Empty, profileId);
            Assert.True(ids.Add(profileId), "The lookup returned a duplicate ProfileId.");
            using var detailsDocument = IntegrationTestHelpers.ParseJson(await sc.Manager.GetClient().ExecuteEntityMethod(
                "00000000-0000-0000-0000-000000000007", "GetAssuranceProfileByProfileId", profileId.ToString()));
            var details = IntegrationTestHelpers.GetResult(detailsDocument.RootElement).GetProperty("GetAssuranceProfileByProfileId");
            Assert.Equal(profileId, details.GetProperty("ProfileId").GetGuid());
            Assert.Equal(details.GetProperty("Description").GetString(), profile.GetProperty("Description").GetString());
            Assert.Equal(details.GetProperty("Mode").GetInt32(), profile.GetProperty("Mode").GetInt32());
        }
    }
}
