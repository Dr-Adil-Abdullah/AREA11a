using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The single policy for owner-defined form fields (Phase 2).
///
/// A field has two different identities: its database id and its stable, human-readable key. The
/// label is only display text and may change; the key is assigned once and never changes with it.
/// Keeping definition and answer rules here means the admin screen, the record panel and tests all
/// use exactly the same contract instead of growing separate validation rules.
///
/// This policy deliberately does not know about stock, prices, payments or documents. Those remain
/// in their existing services. It only governs the metadata and values of custom fields.
/// </summary>
public static class FieldGovernance
{
    public const int MinLabelLength = 2;

    public const int MaxLabelLength = 40;

    public const int MaxAnswerLength = 200;

    public const int MaxHelpTextLength = 160;

    /// <summary>The stable key used for a row that was created before this phase.</summary>
    public static string KeyFor(string? entity, int id) =>
        CustomFieldEntity.Clean(entity) + ".custom." + id.ToString(CultureInfo.InvariantCulture);

    /// <summary>Help is optional display guidance, never a second value or a validation expression.</summary>
    public static string CleanHelpText(string? text) => (text ?? string.Empty).Trim();

    /// <summary>Validates the complete definition before any database write starts.</summary>
    public static string? DefinitionError(
        string? entity,
        string? label,
        string? type,
        string? optionsText,
        string? helpText = null)
    {
        // Update already loaded its row from the database; a null entity means that existing row's
        // entity was checked by the caller. Add always passes an entity and therefore cannot skip this.
        if (entity is not null && !CustomFieldEntity.IsKnown(entity))
        {
            return "Choose the form this field belongs to.";
        }

        var cleanLabel = (label ?? string.Empty).Trim();
        if (cleanLabel.Length < MinLabelLength)
        {
            return "The field needs a name of at least " + MinLabelLength + " letters.";
        }

        if (cleanLabel.Length > MaxLabelLength)
        {
            return "The name is too long - keep it under " + MaxLabelLength + " letters.";
        }

        if (cleanLabel.Any(char.IsControl))
        {
            return "The field name must be one line without control characters.";
        }

        var cleanHelp = CleanHelpText(helpText);
        if (cleanHelp.Length > MaxHelpTextLength)
        {
            return "The help text is too long - keep it under " + MaxHelpTextLength + " letters.";
        }

        if (cleanHelp.Any(char.IsControl))
        {
            return "The help text must be one line without control characters.";
        }

        var cleanType = CustomFieldType.Clean(type);
        if (!CustomFieldType.IsKnown(cleanType))
        {
            return "Choose what kind of answer this field takes.";
        }

        if (CustomFieldType.NeedsOptions(cleanType))
        {
            var options = CustomFieldType.ParseOptions(CustomFieldType.BuildOptions(optionsText));
            if (options.Count < 2)
            {
                return "A list needs at least two choices, separated by commas.";
            }

            if (options.Any(option => option.Any(char.IsControl)))
            {
                return "List choices must be one line without control characters.";
            }
        }

        return null;
    }

    /// <summary>
    /// Turns what the person typed into the canonical stored text, or names the exact problem.
    /// Amounts remain integer paisa and quantities remain REAL-compatible strings; no financial
    /// document is created or changed by this method.
    /// </summary>
    public static bool Normalize(
        CustomField field,
        string? raw,
        int photoCount,
        out string? stored,
        out string? error)
    {
        stored = null;
        error = null;

        var text = (raw ?? string.Empty).Trim();
        var type = CustomFieldType.Clean(field.Type);

        if (type == CustomFieldType.Photo)
        {
            // A photo field has no value of its own: the gallery is the answer.
            if (field.Required && photoCount <= 0)
            {
                error = "\"" + field.Label + "\" needs at least one photo - use the Photos… button first.";
                return false;
            }

            return true;
        }

        if (text.Length == 0)
        {
            if (field.Required)
            {
                error = type == CustomFieldType.Check
                    ? "\"" + field.Label + "\" must be ticked."
                    : "\"" + field.Label + "\" is required.";
                return false;
            }

            return true;
        }

        if (text.Any(char.IsControl))
        {
            error = "\"" + field.Label + "\" must be one line without control characters.";
            return false;
        }

        switch (type)
        {
            case CustomFieldType.Text:
                if (text.Length > MaxAnswerLength)
                {
                    error = "\"" + field.Label + "\" must be shorter than " + MaxAnswerLength + " letters.";
                    return false;
                }

                stored = text;
                return true;

            case CustomFieldType.Number:
                if (!Qty.TryParse(text, out var number) || !double.IsFinite(number))
                {
                    error = "\"" + field.Label + "\" must be a number (finite only), for example 5 or 2.5.";
                    return false;
                }

                stored = Qty.Edit(number);
                return true;

            case CustomFieldType.Amount:
                if (!Money.TryParse(text, out var rupees))
                {
                    error = "\"" + field.Label + "\" must be an amount in rupees, for example 1200.";
                    return false;
                }

                try
                {
                    stored = Money.ToPaisa(rupees).ToString(CultureInfo.InvariantCulture);
                    return true;
                }
                catch (OverflowException)
                {
                    error = "\"" + field.Label + "\" is too large to store as an amount.";
                    return false;
                }

            case CustomFieldType.Date:
                if (!TryReadDate(text, out var date))
                {
                    error = "\"" + field.Label + "\" must be a date, for example 2026-10-22.";
                    return false;
                }

                stored = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return true;

            case CustomFieldType.Select:
                var match = field.Options.FirstOrDefault(option =>
                    string.Equals(option, text, StringComparison.OrdinalIgnoreCase));

                if (match is null)
                {
                    error = "\"" + field.Label + "\" must be one of: " + string.Join(", ", field.Options) + ".";
                    return false;
                }

                stored = match;
                return true;

            case CustomFieldType.Check:
                var ticked = text is "1" or "true" or "True" or "yes" or "Yes" or "on";

                if (!ticked && text is not ("0" or "false" or "False" or "no" or "No" or "off"))
                {
                    error = "\"" + field.Label + "\" must be yes or no.";
                    return false;
                }

                if (field.Required && !ticked)
                {
                    error = "\"" + field.Label + "\" must be ticked.";
                    return false;
                }

                stored = ticked ? "1" : "0";
                return true;

            default:
                error = "That kind of field is not understood.";
                return false;
        }
    }

    /// <summary>The two date spellings accepted by the existing shop forms.</summary>
    private static bool TryReadDate(string text, out DateTime date)
    {
        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy" };

        return DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }
}
