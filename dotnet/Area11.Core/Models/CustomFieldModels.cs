using System.Globalization;
using System.Text.Json;
using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// The three forms a custom field can be added to (migration 005). The strings are the values the
/// `custom_fields.entity` column already holds, so an older database needs no conversion.
/// </summary>
public static class CustomFieldEntity
{
    public const string Product = "product";
    public const string Customer = "customer";
    public const string Supplier = "supplier";

    public static readonly string[] All = { Product, Customer, Supplier };

    public static bool IsKnown(string? entity) =>
        !string.IsNullOrWhiteSpace(entity) && All.Contains(Clean(entity));

    public static string Clean(string? entity) => (entity ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>"Medicine", "Customer", "Supplier" - the word the screen shows.</summary>
    public static string LabelFor(string? entity) => Clean(entity) switch
    {
        Product => "Medicine",
        Customer => "Customer",
        Supplier => "Supplier",
        _ => "Record"
    };

    /// <summary>The permission that governs the values of this kind of record.</summary>
    public static string ModuleFor(string? entity) => Clean(entity) switch
    {
        Product => "products",
        Supplier => "suppliers",
        _ => "customers"
    };
}

/// <summary>
/// The kinds of custom field (Spec 8.4.2).
///
/// The original web app offered text, number, date, list and yes/no. The master specification adds
/// **amount** (money) and **photo**. The shop asked for exactly those four words - text, number,
/// amount, photo - so all seven exist here and a field the owner never uses costs nothing.
///
/// **Money is stored in paisa**, like every other rupee in this program (D-00a). A field that stored
/// "1200.5" as text would be a second kind of money in the database, and the reports would not know
/// about it.
///
/// **A photo field stores no value of its own.** The picture lives in the record's photo gallery
/// (F-41), so there is one copy, one backup and one "put away" rule instead of two. "Required" then
/// means "at least one picture must be in the gallery".
/// </summary>
public static class CustomFieldType
{
    public const string Text = "text";
    public const string Number = "number";
    public const string Amount = "amount";
    public const string Date = "date";
    public const string Select = "select";
    public const string Check = "check";
    public const string Photo = "photo";

    public static readonly string[] All = { Text, Number, Amount, Date, Select, Check, Photo };

    /// <summary>The four kinds the owner named first, in his order; the rest follow.</summary>
    public static readonly string[] Common = { Text, Number, Amount, Photo };

    public static bool IsKnown(string? type) =>
        !string.IsNullOrWhiteSpace(type) && All.Contains(Clean(type));

    public static string Clean(string? type) => (type ?? string.Empty).Trim().ToLowerInvariant();

    public static string LabelFor(string? type) => Clean(type) switch
    {
        Text => "Text",
        Number => "Number",
        Amount => "Amount (Rs)",
        Date => "Date",
        Select => "List (pick one)",
        Check => "Yes / no",
        Photo => "Photo",
        _ => "Text"
    };

    /// <summary>Only a list needs the owner to type the choices.</summary>
    public static bool NeedsOptions(string? type) => Clean(type) == Select;

    /// <summary>The choices of a list field, stored as a small JSON array.</summary>
    public static IReadOnlyList<string> ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            return (JsonSerializer.Deserialize<List<string>>(optionsJson) ?? new List<string>())
                .Select(option => option.Trim())
                .Where(option => option.Length > 0)
                .ToList();
        }
        catch (JsonException)
        {
            // A hand-edited or half-written value must not break the form; the list is simply empty.
            return Array.Empty<string>();
        }
    }

    /// <summary>Turns what the owner typed ("A, B , C") into the JSON the column keeps.</summary>
    public static string? BuildOptions(string? text)
    {
        var options = (text ?? string.Empty)
            .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(option => option.Trim())
            .Where(option => option.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return options.Count == 0 ? null : JsonSerializer.Serialize(options);
    }

    /// <summary>"1" and "0" are how a yes/no answer is stored; the screen shows a tick.</summary>
    public static bool AsBool(string? stored) => stored == "1" || stored == "true";

    /// <summary>Rupees text for an amount value, so the box can be filled again after a reload.</summary>
    public static string PrettyAmount(string? stored)
    {
        if (!long.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var paisa))
        {
            return string.Empty;
        }

        var rupees = Money.ToRupees(paisa);

        return rupees == decimal.Truncate(rupees)
            ? decimal.Truncate(rupees).ToString("0", CultureInfo.InvariantCulture)
            : rupees.ToString("0.##", CultureInfo.InvariantCulture);
    }
}

/// <summary>One field the owner made, as the `custom_fields` row holds it.</summary>
public sealed class CustomField
{
    public int Id { get; set; }

    public string Entity { get; set; } = CustomFieldEntity.Product;

    public string Label { get; set; } = string.Empty;

    public string Type { get; set; } = CustomFieldType.Text;

    public string? OptionsJson { get; set; }

    public bool Required { get; set; }

    public bool Active { get; set; } = true;

    public int Sort { get; set; }

    public string? CreatedAt { get; set; }

    public string EntityLabel => CustomFieldEntity.LabelFor(Entity);

    public string TypeLabel => CustomFieldType.LabelFor(Type);

    public bool IsPhoto => CustomFieldType.Clean(Type) == CustomFieldType.Photo;

    public bool NeedsOptions => CustomFieldType.NeedsOptions(Type);

    public IReadOnlyList<string> Options => CustomFieldType.ParseOptions(OptionsJson);

    public string OptionsText => string.Join(", ", Options);

    public string RequiredLabel => Required ? "required" : "optional";

    /// <summary>"shown" / "put away" - a field is never deleted, only switched off (D-00b).</summary>
    public string StateLabel => Active ? "shown" : "put away";

    /// <summary>"CNIC · Text · required" - the one line the manager list shows.</summary>
    public string Summary => Label + " · " + TypeLabel + (Required ? " · required" : string.Empty);
}

/// <summary>One stored answer. A field that is switched off keeps its value for ever.</summary>
public sealed class CustomValue
{
    public int Id { get; set; }

    public int FieldId { get; set; }

    public string Entity { get; set; } = CustomFieldEntity.Product;

    public int EntityId { get; set; }

    public string? Value { get; set; }

    public string? UpdatedAt { get; set; }
}

/// <summary>
/// A field the owner can add with one click instead of typing everything (Spec 8.4.1: CNIC, shelf,
/// salt, discount group). It is a suggestion, not a rule: the label and the choices stay editable.
/// </summary>
public sealed class CustomFieldSuggestion
{
    public CustomFieldSuggestion(string label, string type, string options = "")
    {
        Label = label;
        Type = type;
        Options = options;
    }

    public string Label { get; }

    public string Type { get; }

    public string Options { get; }

    public string TypeLabel => CustomFieldType.LabelFor(Type);

    public string Hint => Label + " (" + TypeLabel + ")";

    /// <summary>The eight the shop is most likely to want first; the owner can type any other.</summary>
    public static IReadOnlyList<CustomFieldSuggestion> All { get; } = new List<CustomFieldSuggestion>
    {
        new("CNIC", CustomFieldType.Text),
        new("Shelf / almari", CustomFieldType.Text),
        new("Salt / formula", CustomFieldType.Text),
        new("Discount group", CustomFieldType.Select, "None, A, B, C"),
        new("Date of birth", CustomFieldType.Date),
        new("Prescription photo", CustomFieldType.Photo),
        new("Delivery charge", CustomFieldType.Amount),
        new("Commission %", CustomFieldType.Number)
    };
}
