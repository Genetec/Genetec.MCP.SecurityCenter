namespace Genetec.Mcp.SecurityCenter.Helpers;

public interface IManagementBatchItem
{
    string? Name { get; }
    Guid? Id { get; }
}

public static class ManagementBatchItemHelper
{
    public static List<string> GetCreateNames(IReadOnlyList<IManagementBatchItem> items)
    {
        var names = new List<string>(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item is null || item.Id is not null || string.IsNullOrWhiteSpace(item.Name))
                throw new ArgumentException($"Each item must include a nonempty name and no id for create (invalid item at index {index}).", "items");
            names.Add(item.Name);
        }
        return names;
    }

    public static List<Guid> GetDeleteIds(IReadOnlyList<IManagementBatchItem> items)
    {
        var ids = new List<Guid>(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item is null || item.Name is not null || item.Id is null)
                throw new ArgumentException($"Each item must include an id and no name for delete (invalid item at index {index}).", "items");
            ids.Add(item.Id.Value);
        }
        return ids;
    }
}
