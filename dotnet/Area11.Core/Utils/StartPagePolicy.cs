namespace Area11.Core.Utils;

/// <summary>
/// Pick only from pages already offered to the signed-in user. Counter is the normal start;
/// otherwise use Dashboard when available, then the first permitted ordinary page as a safe fallback.
/// Stable keys, not editable labels, identify pages. This does not grant any permission.
/// </summary>
public static class StartPagePolicy
{
    public static string? Choose(IEnumerable<string> permittedPageKeys)
    {
        var keys = permittedPageKeys.ToList();
        if (keys.Contains("counter", StringComparer.Ordinal))
        {
            return "counter";
        }

        return keys.FirstOrDefault(key => key == "dashboard")
               ?? keys.FirstOrDefault(key => !string.IsNullOrEmpty(key));
    }
}
