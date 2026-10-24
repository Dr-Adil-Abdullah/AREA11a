namespace Area11.WPF.Services;

/// <summary>
/// Services never throw at the UI. They return this result so the ViewModel can show
/// a friendly message and the log keeps the technical detail.
/// </summary>
public class OperationResult
{
    public bool Ok { get; init; }

    public string? Error { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static OperationResult Success(IReadOnlyList<string>? warnings = null) =>
        new() { Ok = true, Warnings = warnings ?? Array.Empty<string>() };

    public static OperationResult Fail(string error, IReadOnlyList<string>? warnings = null) =>
        new() { Ok = false, Error = error, Warnings = warnings ?? Array.Empty<string>() };
}

public sealed class OperationResult<T> : OperationResult
{
    public T? Value { get; init; }

    public static OperationResult<T> Success(T value, IReadOnlyList<string>? warnings = null) =>
        new() { Ok = true, Value = value, Warnings = warnings ?? Array.Empty<string>() };

    public static new OperationResult<T> Fail(string error, IReadOnlyList<string>? warnings = null) =>
        new() { Ok = false, Error = error, Warnings = warnings ?? Array.Empty<string>() };
}

/// <summary>Expected input problems (missing name, bad quantity) — not technical failures.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }
}
