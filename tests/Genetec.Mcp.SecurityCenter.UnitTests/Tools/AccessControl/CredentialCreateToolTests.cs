using Genetec.Mcp.SecurityCenter.Tools;
using Genetec.Mcp.SecurityCenter.Tools.AccessControl;

namespace Genetec.Mcp.SecurityCenter.UnitTests.Tools.AccessControl;

public class CredentialCreateToolTests
{
    [Fact]
    public async Task Create_WrongAssignmentType_PreservesCreatedCredentialAndRejectsAssignment()
    {
        var wrong = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler(new Tools.ToolTestHandler());
        handler.AddEntity(wrong, "UserGroup");
        var result = Assert.IsType<CredentialCreateAssignmentResult>(await CredentialCreateTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialCreateRequest([new CredentialItem("MCP_RejectAssignment", CredentialFormatType.Pin, Pin: "1234", AssignToCardholderId: wrong)])));
        Assert.NotEqual(Guid.Empty, result.Credential.Guid);
        Assert.Null(result.AssignmentResponse);
        Assert.Contains("UserGroup", result.AssignmentError);
        Assert.Single(handler.Requests, request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Create_MixedAssignmentTypes_PreservesCreatedCredentialsAndSuccessfulAssignment()
    {
        var valid = Guid.NewGuid();
        var wrong = Guid.NewGuid();
        var handler = new EntityMetadataTestHandler(new Tools.ToolTestHandler());
        handler.AddEntity(valid, "Cardholder");
        handler.AddEntity(wrong, "UserGroup");
        var batch = Assert.IsType<Genetec.Mcp.SecurityCenter.Tools.BatchCreateResult>(await CredentialCreateTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialCreateRequest([
                new CredentialItem("MCP_ValidAssignment", CredentialFormatType.Pin, Pin: "1234", AssignToCardholderId: valid),
                new CredentialItem("MCP_RejectAssignment", CredentialFormatType.Pin, Pin: "5678", AssignToCardholderId: wrong)])));
        Assert.Equal(2, batch.SuccessCount);
        Assert.NotNull(batch.Results[0].Response);
        Assert.Contains("UserGroup", batch.Results[1].Warning);
        Assert.Equal(3, handler.Requests.Count(request => request.Method == HttpMethod.Post));
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Post &&
            Uri.UnescapeDataString(request.Uri.Query).Contains(wrong.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateBatch_InvalidLaterFormat_RejectsBeforeAnyMutation()
    {
        var handler = new Tools.ToolTestHandler();
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialCreateTool.Execute(TestHelpers.FakeManager(handler),
                new CredentialCreateRequest([
                    new CredentialItem("Valid", CredentialFormatType.Pin, Pin: "1234"),
                    new CredentialItem("Invalid", CredentialFormatType.Pin)])));
        Assert.Contains("pin", error.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateBatch_AssignmentsExceedTotalOperationBudget_RejectsBeforeAnyMutation()
    {
        var handler = new Tools.ToolTestHandler();
        var items = Enumerable.Range(0, 26).Select(index =>
            new CredentialItem($"Credential {index}", CredentialFormatType.Pin,
                Pin: "1234", AssignToCardholderId: Guid.NewGuid())).ToList();
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialCreateTool.Execute(TestHelpers.FakeManager(handler), new CredentialCreateRequest(items)));
        Assert.Equal("items", error.ParamName);
        Assert.Contains("52 operations", error.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    public static IEnumerable<object[]> MissingFormatParameters()
    {
        yield return [CredentialFormatType.HidH10306, "facility_code"];
        yield return [CredentialFormatType.HidH1030237Bit, "card_number"];
        yield return [CredentialFormatType.HidH1030437Bit, "facility_code"];
        yield return [CredentialFormatType.HidCorp100035Bit, "company_code"];
        yield return [CredentialFormatType.HidCorp100048Bit, "company_code"];
        yield return [CredentialFormatType.Csn32Bit, "csn"];
        yield return [CredentialFormatType.LicensePlate, "plate_number"];
        yield return [CredentialFormatType.Custom, "custom_format_id"];
        yield return [CredentialFormatType.RawCard, "raw_data"];
    }

    [Theory]
    [MemberData(nameof(MissingFormatParameters))]
    public async Task EveryAdditionalFormat_ValidatesItsRequiredFields(
        CredentialFormatType format, string parameterName)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialCreateTool.Execute(TestHelpers.FakeManager(),
                new CredentialCreateRequest([new CredentialItem("MCP_Cred_Test", format)])));

        Assert.Contains(parameterName, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
    // ── Validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_PinFormat_NullPin_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialCreateTool.Execute(TestHelpers.FakeManager(),
                new CredentialCreateRequest([new CredentialItem("MCP_Cred_Test", CredentialFormatType.Pin)])));

        Assert.Contains("pin", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Wiegand26Bit_NullFacilityCode_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialCreateTool.Execute(TestHelpers.FakeManager(),
                new CredentialCreateRequest([new CredentialItem("MCP_Cred_Test", CredentialFormatType.Wiegand26Bit, CardNumber: 12345)])));

        Assert.Contains("facility_code", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Wiegand26Bit_NullCardNumber_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CredentialCreateTool.Execute(TestHelpers.FakeManager(),
                new CredentialCreateRequest([new CredentialItem("MCP_Cred_Test", CredentialFormatType.Wiegand26Bit, FacilityCode: 1)])));

        Assert.Contains("card_number", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WithCardholderAssignment_ReturnsAssignmentSdkResponse()
    {
        var handler = new EntityMetadataTestHandler(new Tools.ToolTestHandler());
        var cardholderId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        handler.AddEntity(cardholderId, "Cardholder");

        var result = await CredentialCreateTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialCreateRequest([new CredentialItem(
                "MCP_Cred_Assigned",
                CredentialFormatType.Pin,
                Pin: "1234",
                AssignToCardholderId: cardholderId)]));

        var assignment = Assert.IsType<CredentialCreateAssignmentResult>(result);
        Assert.Equal("{\"Guid\":\"00000000-0000-0000-0000-000000000001\",\"Status\":\"Ok\",\"Name\":\"Test\"}",
            assignment.AssignmentResponse);
        Assert.Null(assignment.AssignmentError);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task CreateBatch_WithCardholderAssignment_ReturnsAssignmentSdkResponsePerItem()
    {
        var handler = new EntityMetadataTestHandler(new Tools.ToolTestHandler());
        var cardholderId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        handler.AddEntity(cardholderId, "Cardholder");

        var result = await CredentialCreateTool.Execute(TestHelpers.FakeManager(handler),
            new CredentialCreateRequest([
                new CredentialItem("MCP_Cred_Assigned_1", CredentialFormatType.Pin,
                    Pin: "1234", AssignToCardholderId: cardholderId),
                new CredentialItem("MCP_Cred_Assigned_2", CredentialFormatType.Pin,
                    Pin: "5678", AssignToCardholderId: cardholderId)]));

        var batch = Assert.IsType<BatchCreateResult>(result);
        Assert.Equal(2, batch.SuccessCount);
        Assert.All(batch.Results, item => Assert.NotNull(item.Response));
        Assert.Equal(6, handler.Requests.Count);
    }
}
