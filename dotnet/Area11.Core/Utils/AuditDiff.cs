using System.Text.Json;
using System.Text.Json.Nodes;

namespace Area11.Core.Utils;

/// <summary>
/// The black-box rule (Spec 22.4): store only the fields that really changed.
/// New record → full new value. Delete → full old value. Edit → changed fields only.
/// No change → nothing at all.
/// </summary>
public static class AuditDiff
{
    public static (string? Old, string? New) Compare(object? before, object? after)
    {
        var beforeNode = before is null ? null : JsonSerializer.SerializeToNode(before);
        var afterNode = after is null ? null : JsonSerializer.SerializeToNode(after);

        if (beforeNode is null && afterNode is null)
        {
            return (null, null);
        }

        if (beforeNode is null)
        {
            return (null, afterNode!.ToJsonString(Json.WriterOptions));
        }

        if (afterNode is null)
        {
            return (beforeNode.ToJsonString(Json.WriterOptions), null);
        }

        if (beforeNode is not JsonObject beforeObject || afterNode is not JsonObject afterObject)
        {
            var same = string.Equals(beforeNode.ToJsonString(), afterNode.ToJsonString(), StringComparison.Ordinal);
            return same
                ? (null, null)
                : (beforeNode.ToJsonString(Json.WriterOptions), afterNode.ToJsonString(Json.WriterOptions));
        }

        var oldValues = new JsonObject();
        var newValues = new JsonObject();

        foreach (var pair in afterObject)
        {
            var beforeValue = beforeObject[pair.Key];
            var beforeText = beforeValue?.ToJsonString() ?? "null";
            var afterText = pair.Value?.ToJsonString() ?? "null";

            if (!string.Equals(beforeText, afterText, StringComparison.Ordinal))
            {
                oldValues[pair.Key] = beforeValue?.DeepClone();
                newValues[pair.Key] = pair.Value?.DeepClone();
            }
        }

        foreach (var pair in beforeObject)
        {
            if (!afterObject.ContainsKey(pair.Key))
            {
                oldValues[pair.Key] = pair.Value?.DeepClone();
                newValues[pair.Key] = null;
            }
        }

        if (oldValues.Count == 0)
        {
            return (null, null);
        }

        return (oldValues.ToJsonString(Json.WriterOptions), newValues.ToJsonString(Json.WriterOptions));
    }
}

/// <summary>Shared JSON settings so every stored value looks the same.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions WriterOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string Text(string? value) => JsonSerializer.Serialize(value ?? string.Empty);

    public static string Number(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string Number(double value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string Flag(bool value) => value ? "true" : "false";
}
