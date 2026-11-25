namespace Area11.Core.Utils;

/// <summary>
/// A service answer that never throws at the UI: either a value, or a friendly message the
/// owner can read. Warnings are the "only an alert, never block" channel (U-30).
/// </summary>
public class ServiceResult
{
    [System.Text.Json.Serialization.JsonIgnore]
    public Area11.Core.Models.EditVersion? EditVersion { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Area11.Core.Models.SettingsEditForm? SettingsEdit { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Area11.Core.Models.PermissionEditForm? PermissionEdit { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Area11.Core.Models.User? StaffEdit { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Area11.Core.Models.RecoveryEditForm? RecoveryEdit { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Area11.Core.Models.FormFieldDefinition? NativeFieldEdit { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Area11.Core.Models.CustomDefinitionEditForm? CustomDefinitionEdit { get; init; }

    public bool Ok { get; init; }

    public string? Error { get; init; }

    /// <summary>Failure only: creator did not run, or a pre-COMMIT transaction rollback returned successfully.
    /// Default false is unknown, never infer safety from wording. Not a durable exactly-once token.</summary>
    public bool NoCommitConfirmed { get; init; }

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
