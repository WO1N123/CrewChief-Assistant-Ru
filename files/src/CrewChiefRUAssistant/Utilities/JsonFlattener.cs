using System.Text.Json;

namespace CrewChiefRUAssistant.Utilities;

public static class JsonFlattener
{
    public static IReadOnlyDictionary<string, string> Flatten(JsonElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Walk(root, string.Empty, result);
        return result;
    }

    private static void Walk(
        JsonElement element,
        string path,
        IDictionary<string, string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = string.IsNullOrEmpty(path)
                        ? property.Name
                        : $"{path}.{property.Name}";

                    Walk(property.Value, childPath, result);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, $"{path}[{index}]", result);
                    index++;
                }
                break;

            case JsonValueKind.String:
                result[path] = element.GetString() ?? string.Empty;
                break;

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                result[path] = element.GetRawText();
                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                break;
        }
    }
}
