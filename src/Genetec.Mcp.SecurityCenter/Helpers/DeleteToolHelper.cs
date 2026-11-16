using Genetec.Mcp.SecurityCenter.Client;

namespace Genetec.Mcp.SecurityCenter.Helpers;

public static class DeleteToolHelper
{
    public static async Task<object> Execute(SecurityCenterRestClient client, List<Guid> items, string expectedType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedType, "expected_type");
        if (items.Count == 0)
            throw new ArgumentException("Parameter 'items' must contain at least one GUID.");

        if (items.Count == 1)
        {
            await client.RequireEntityType(items[0], "items", expectedType);
            return await client.DeleteEntity<StatusResult>(items[0]);
        }

        return await client.DeleteEntitiesBatch(items, expectedType);
    }
}
