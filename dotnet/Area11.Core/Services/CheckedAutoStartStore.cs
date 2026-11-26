namespace Area11.Core.Services;

/// <summary>Windows Run value kinds, without a Windows-only dependency in Core.</summary>
public enum AutoStartValueKind { None = -1, String = 1, ExpandString = 2, Binary = 3, DWord = 4, MultiString = 7, QWord = 11 }

/// <summary>Detached immutable value contents/kind/presence, not an ACL/key snapshot or registry transaction.</summary>
public sealed class AutoStartEntry
{
    private readonly object? _value;
    public bool IsPresent { get; }
    public AutoStartValueKind Kind { get; }
    public object? Value => Copy(_value);
    private AutoStartEntry(bool present, AutoStartValueKind kind, object? value)
    { IsPresent = present; Kind = kind; _value = Copy(value); }
    public static AutoStartEntry Missing { get; } = new(false, default, null);

    public static AutoStartEntry Present(AutoStartValueKind kind, object value)
    {
        var valid = kind switch
        {
            AutoStartValueKind.String or AutoStartValueKind.ExpandString => value is string,
            AutoStartValueKind.Binary or AutoStartValueKind.None => value is byte[],
            AutoStartValueKind.DWord => value is int,
            AutoStartValueKind.QWord => value is long,
            AutoStartValueKind.MultiString => value is string[],
            _ => false
        };
        if (!valid) throw new ArgumentException("Unsupported or mismatched Windows Run value.");
        return new AutoStartEntry(true, kind, value);
    }

    public static AutoStartEntry Command(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || exePath.Contains('"'))
            throw new ArgumentException("The program's own unquoted path is needed for the start-up entry.", nameof(exePath));
        return Present(AutoStartValueKind.String, "\"" + exePath.Trim() + "\"");
    }

    public bool Matches(AutoStartEntry other) => IsPresent == other.IsPresent && (!IsPresent ||
        (Kind == other.Kind && (_value switch
        {
            byte[] bytes => other._value is byte[] next && bytes.SequenceEqual(next),
            string[] words => other._value is string[] next && words.SequenceEqual(next, StringComparer.Ordinal),
            _ => Equals(_value, other._value)
        })));

    private static object? Copy(object? value) => value switch
    { byte[] bytes => bytes.ToArray(), string[] words => words.ToArray(), _ => value };
}

/// <summary>Capture must throw on unreadable data, never invent absence. Replace checks expected contents
/// before writing and reads back afterward. Windows offers NO cross-process atomic compare/exchange here.</summary>
public interface ICheckedAutoStartStore : IAutoStartStore
{
    AutoStartEntry Capture();
    void Replace(AutoStartEntry expected, AutoStartEntry replacement);
}
