using System.Text;

namespace Genetec.Mcp.SecurityCenter.Tools;

public static class EntityNameSanitizer
{
    public static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var replaced = name.Replace("&", "-").Replace("/", "-");
        var sb = new StringBuilder(replaced.Length);
        foreach (var ch in replaced)
        {
            if (ch <= 127)
                sb.Append(ch);
        }

        return sb.ToString().Trim();
    }
}
