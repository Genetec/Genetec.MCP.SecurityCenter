namespace Genetec.Mcp.SecurityCenter.Helpers;

public static class ResourceLimits
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 50;
    public const int MaxBatchItems = 50;

    public static void EnsureBatchOperationCount(long operationCount, string parameterName)
    {
        if (operationCount > MaxBatchItems)
            throw new ArgumentException(
                $"Parameter '{parameterName}' expands to {operationCount} operations; at most {MaxBatchItems} operations are allowed per request.",
                parameterName);
    }
}
