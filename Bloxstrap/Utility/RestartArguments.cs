using System.Collections.Generic;

namespace Claudestrap.Utility;

public static class RestartArguments
{
    public static string[] Clean(IEnumerable<string> arguments)
    {
        var result = new List<string>();
        using var items = arguments.GetEnumerator();
        while (items.MoveNext())
        {
            if (items.Current is "-updateready" or "-updateparent") { items.MoveNext(); continue; }
            result.Add(items.Current);
        }
        return result.ToArray();
    }
}
