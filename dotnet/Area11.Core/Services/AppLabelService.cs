using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The shop's own names for application sections (E-12/S-05). A stable key drives navigation and
/// permissions; only the visible words change. Labels are ordinary database settings, so every
/// counter reads the same catalog and every save keeps the Settings old/new audit.
///
/// This is deliberately a catalog of section names, not a free-text rewrite of sentences, receipts,
/// historical documents or audit rows. That keeps a renamed word from changing a business meaning.
/// </summary>
public sealed class AppLabelService
{
    public const int MaximumLength = 60;

    private static readonly IReadOnlyList<AppLabelDefinition> Catalog = new[]
    {
        new AppLabelDefinition("sell", "SELL"),
        new AppLabelDefinition("stock", "STOCK"),
        new AppLabelDefinition("people", "CUSTOMERS & SUPPLIERS"),
        new AppLabelDefinition("money", "MONEY"),
        new AppLabelDefinition("system", "SYSTEM"),
        new AppLabelDefinition("dashboard", "Dashboard"),
        new AppLabelDefinition("counter", "Counter (POS)"),
        new AppLabelDefinition("bills", "Bills"),
        new AppLabelDefinition("returns", "Returns"),
        new AppLabelDefinition("provisional", "No bill (PR)"),
        new AppLabelDefinition("products", "Products"),
        new AppLabelDefinition("reorder", "Reorder pad"),
        new AppLabelDefinition("purchases", "Purchases"),
        new AppLabelDefinition("writeOff", "Write-off"),
        new AppLabelDefinition("stockMinus", "Stock minus"),
        new AppLabelDefinition("stockTake", "Stock-take"),
        new AppLabelDefinition("partners", "Customers & suppliers"),
        new AppLabelDefinition("doctors", "Doctors & reps"),
        new AppLabelDefinition("shift", "Shift (galla)"),
        new AppLabelDefinition("expenses", "Expenses"),
        new AppLabelDefinition("reports", "Reports"),
        new AppLabelDefinition("tax", "Tax report"),
        new AppLabelDefinition("audit", "Black box"),
        new AppLabelDefinition("importExport", "Excel / CSV"),
        new AppLabelDefinition("backup", "Backup & restore"),
        new AppLabelDefinition("lan", "Second counter (LAN)"),
        new AppLabelDefinition("cloud", "Cloud sync"),
        new AppLabelDefinition("customFields", "Own fields"),
        new AppLabelDefinition("hardware", "Printer & scanner"),
        new AppLabelDefinition("settings", "Settings"),
        new AppLabelDefinition("books", "Check the books"),
        new AppLabelDefinition("alerts", "Alerts")
    };

    private readonly SettingsService _settings;
    private readonly PermissionService _permissions;

    public AppLabelService(SettingsService settings, PermissionService permissions)
    {
        _settings = settings;
        _permissions = permissions;
    }

    public static IReadOnlyList<AppLabelDefinition> Definitions => Catalog;

    public static string SettingKey(string key) => Definition(key).SettingKey;

    /// <summary>Current text for one stable key; an invalid stored value can never blank the menu.</summary>
    public string Get(string key)
    {
        var definition = Definition(key);
        var stored = _settings.GetString(definition.SettingKey).Trim();
        return ValidateText(definition, stored) is null ? stored : definition.DefaultText;
    }

    public List<AppLabelValue> List() => Catalog.Select(definition => new AppLabelValue
    {
        Key = definition.Key,
        DefaultText = definition.DefaultText,
        Text = Get(definition.Key)
    }).ToList();

    /// <summary>Validate a complete editor save without writing a partial set of labels.</summary>
    public ServiceResult Validate(IReadOnlyDictionary<string, string?> values)
    {
        if (values.Count == 0)
        {
            return ServiceResult.Fail("There are no application labels to save.");
        }

        foreach (var pair in values)
        {
            var definition = Catalog.FirstOrDefault(item =>
                string.Equals(item.Key, pair.Key, StringComparison.Ordinal));

            if (definition is null)
            {
                return ServiceResult.Fail("Unknown application label '" + pair.Key + "'.");
            }

            var refusal = ValidateText(definition, pair.Value);
            if (refusal is not null)
            {
                return ServiceResult.Fail(refusal);
            }
        }

        return ServiceResult.Success();
    }

    /// <summary>Public-service write: Core permission, validation, one transaction and old/new audit.</summary>
    public ServiceResult Save(IReadOnlyDictionary<string, string?> values, User? actor)
    {
        if (!_permissions.Can(actor, Modules.Settings))
        {
            return ServiceResult.Fail("You are not allowed to change the application labels.");
        }

        var valid = Validate(values);
        if (!valid.Ok)
        {
            return valid;
        }

        var settings = values.ToDictionary(
            pair => Definition(pair.Key).SettingKey,
            pair => (object?)pair.Value!.Trim(),
            StringComparer.Ordinal);

        return _settings.SetMany(settings, actor);
    }

    /// <summary>Restore every shipped section name, and only those names, with the same audit trail.</summary>
    public ServiceResult Reset(User? actor)
    {
        if (!_permissions.Can(actor, Modules.Settings))
        {
            return ServiceResult.Fail("You are not allowed to restore the application labels.");
        }

        return _settings.SetMany(Catalog.ToDictionary(
            definition => definition.SettingKey,
            definition => (object?)definition.DefaultText,
            StringComparer.Ordinal), actor);
    }

    /// <summary>SettingsService calls this too, so a direct setting write cannot bypass the guard.</summary>
    public static string? GuardSetting(string key, object? value)
    {
        var definition = Catalog.FirstOrDefault(item => item.SettingKey == key);
        return definition is null ? "Unknown application label setting '" + key + "'." : ValidateText(definition, value);
    }

    private static AppLabelDefinition Definition(string key) => Catalog.FirstOrDefault(item =>
        string.Equals(item.Key, key, StringComparison.Ordinal))
        ?? throw new ArgumentException("Unknown application label '" + key + "'.", nameof(key));

    private static string? ValidateText(AppLabelDefinition definition, object? value)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            return definition.DefaultText + " cannot be empty.";
        }

        text = text.Trim();
        if (text.Length > MaximumLength)
        {
            return definition.DefaultText + " cannot be longer than " + MaximumLength + " characters.";
        }

        return text.Any(char.IsControl)
            ? definition.DefaultText + " must stay on one line without control characters."
            : null;
    }
}
