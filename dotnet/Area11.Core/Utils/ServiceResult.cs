namespace Area11.Core.Utils;

/// <summary>
/// A service answer that never throws at the UI: either a value, or a friendly message the
/// owner can read. Warnings are the "only an alert, never block" channel (U-30).
/// </summary>
public class ServiceResult
{
    public bool Ok { get; init; }

    public string? Error { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static ServiceResult Success(params string[] warnings) =>
        new() { Ok = true, Warnings = warnings };

    public static ServiceResult Fail(string error) => new() { Ok = false, Error = error };
}

public sealed class ServiceResult<T> : ServiceResult
{
    public T? Value { get; init; }

    public static ServiceResult<T> Success(T value, params string[] warnings) =>
        new() { Ok = true, Value = value, Warnings = warnings };

    public static new ServiceResult<T> Fail(string error) => new() { Ok = false, Error = error };
}
