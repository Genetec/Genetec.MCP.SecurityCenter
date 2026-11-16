using Genetec.Mcp.SecurityCenter.Client;
using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.IntegrationTests.Tools.AccessControl;

public class CredentialCreateToolTests(ScIntegrationFixture sc) : IClassFixture<ScIntegrationFixture>
{
    [Fact]
    public async Task Create_WrongAssignmentType_PreservesCreatedCredentialAndRejectsAssignment()
    {
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var name = $"MCP_Assignment_{Guid.NewGuid():N}";
        var result = Assert.IsType<CredentialCreateAssignmentResult>(await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem(name, CredentialFormatType.Pin,
                Pin: Random.Shared.Next(1000000, 9999999).ToString(), AssignToCardholderId: wrong.Id)])));
        sc.TrackForCleanup(result.Credential.Guid);
        Assert.Null(result.AssignmentResponse);
        Assert.Contains("UserGroup", result.AssignmentError);
        await IntegrationTestHelpers.AssertEntityAsync(sc, result.Credential.Guid, "Credential", name);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
    }

    [Fact]
    public async Task Create_MixedAssignmentTypes_PreservesCreatedCredentialsAndReadsBackSuccessfulAssignment()
    {
        var holder = Assert.IsType<EntityCreateResult>(await CardholderCreateTool.Execute(sc.Manager,
            new CardholderCreateRequest([new CardholderItem($"MCP_Assignment_{Guid.NewGuid():N}")])));
        sc.TrackForCleanup(holder.Guid);
        var wrong = await IntegrationTestHelpers.CreateTrackedUserGroupAsync(sc);
        var validName = $"MCP_CredValid_{Guid.NewGuid():N}";
        var wrongName = $"MCP_CredWarn_{Guid.NewGuid():N}";
        var batch = Assert.IsType<BatchCreateResult>(await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([
                new CredentialItem(validName, CredentialFormatType.Pin, Pin: Random.Shared.Next(1000000, 9999999).ToString(), AssignToCardholderId: holder.Guid),
                new CredentialItem(wrongName, CredentialFormatType.Pin, Pin: Random.Shared.Next(1000000, 9999999).ToString(), AssignToCardholderId: wrong.Id)])));
        foreach (var item in batch.Results) if (item.Guid.HasValue) sc.TrackForCleanup(item.Guid.Value);
        Assert.Equal(2, batch.SuccessCount);
        Assert.NotNull(batch.Results[0].Response);
        Assert.Contains("UserGroup", batch.Results[1].Warning);
        var validId = batch.Results[0].Guid!.Value;
        var unassignedId = batch.Results[1].Guid!.Value;
        await IntegrationTestHelpers.AssertEntityAsync(sc, validId, "Credential", validName);
        await IntegrationTestHelpers.AssertEntityAsync(sc, unassignedId, "Credential", wrongName);
        var membership = Assert.IsType<string>(await CardholderOperationsTool.Execute(sc.Manager,
            new CardholderOperationsRequest(CardholderOperationsAction.GetCredentials, Id: holder.Guid)));
        var ids = IntegrationTestHelpers.ExtractGuids(membership);
        Assert.Contains(validId, ids);
        Assert.DoesNotContain(unassignedId, ids);
        await IntegrationTestHelpers.AssertEntityAsync(sc, wrong.Id, "UserGroup", wrong.Name);
    }

    public static IEnumerable<object[]> BuiltInCredentialFormats()
    {
        yield return [new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.Pin, Pin: "1001")];
        yield return [new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.Wiegand26Bit, FacilityCode: 1, CardNumber: 1002)];
        yield return [new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.HidH10306, FacilityCode: 1, CardNumber: 1003)];
        yield return [new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.HidH1030237Bit, CardNumber: 1004)];
        yield return [new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.HidH1030437Bit, FacilityCode: 1, CardNumber: 1005)];
        yield return [new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.HidCorp100035Bit, CompanyCode: 1, CardNumber: 1006)];
        yield return [new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.HidCorp100048Bit, CompanyCode: 1, CardNumber: 1007)];
        yield return [new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.Csn32Bit, Csn: 1008)];
        yield return [new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.LicensePlate, PlateNumber: "MCP1009", PlateState: "ON")];
        yield return [new CredentialItem($"MCP_Cred_{Guid.NewGuid():N}", CredentialFormatType.RawCard, RawData: "0F", BitLength: 8)];
    }
    // ── Integration ─────────────────────────────────────────────────

    [Fact]
    public async Task Create_SinglePinCredential_ReturnsGuid()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var name = $"MCP_Cred_{Guid.NewGuid():N}";

        var result = await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([new CredentialItem(name, CredentialFormatType.Pin, Pin: "1234")]));

        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(default(Guid), created.Guid);
        sc.TrackForCleanup(created.Guid);
        await AssertCreatedCredentialCanBeReadBack(created.Guid, name);
    }

    [Fact]
    public async Task Create_BatchPinCredentials_ReturnsAllGuids()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var items = Enumerable.Range(1, 3)
            .Select(i => new CredentialItem($"MCP_Cred_{i}_{Guid.NewGuid():N}", CredentialFormatType.Pin, Pin: $"{1000 + i}"))
            .ToList();

        var result = await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest(items));

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(3, batch.RequestedCount);
        Assert.Equal(3, batch.SuccessCount);
        Assert.Equal(0, batch.FailureCount);
        Assert.All(batch.Results, r =>
        {
            Assert.True(r.Success);
            Assert.True(r.Guid.HasValue);
            sc.TrackForCleanup(r.Guid!);
        });

        for (var i = 0; i < batch.Results.Count; i++)
            await AssertCreatedCredentialCanBeReadBack(batch.Results[i].Guid!.Value, items[i].Name);
    }

    [Theory]
    [MemberData(nameof(BuiltInCredentialFormats))]
    public async Task EveryBuiltInFormat_CreatesAndCanBeReadBack(CredentialItem item)
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var result = await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([item]));
        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(Guid.Empty, created.Guid);
        sc.TrackForCleanup(created.Guid);
        await AssertCreatedCredentialCanBeReadBack(created.Guid, item.Name);
    }

    [Fact]
    public async Task CustomFormat_ExistingCredentialFormatAndValues_CreateAndCanBeReadBack()
    {
        IntegrationTestHelpers.AssertScAvailable(sc);

        var fixture = await CredentialSearchToolTests.FindExistingCustomCredentialAsync(sc);
        Assert.True(fixture is not null,
            "No custom-format credential with a readable format ID and field values was found in the first 300 credentials in Security Center.");

        var name = $"MCP_Cred_{Guid.NewGuid():N}";
        var result = await CredentialCreateTool.Execute(sc.Manager,
            new CredentialCreateRequest([
                new CredentialItem(name, CredentialFormatType.Custom,
                    CustomFormatId: fixture!.FormatId,
                    CustomValues: fixture.Values)]));
        var created = Assert.IsType<EntityCreateResult>(result);
        Assert.NotEqual(Guid.Empty, created.Guid);
        sc.TrackForCleanup(created.Guid);
        await AssertCreatedCredentialCanBeReadBack(created.Guid, name);
    }

    private async Task AssertCreatedCredentialCanBeReadBack(Guid credentialId, string expectedName)
    {
        var readBack = await sc.Manager.GetClient().GetEntityTrimmed(credentialId.ToString());
        Assert.Contains(expectedName, readBack, StringComparison.OrdinalIgnoreCase);
    }
}
