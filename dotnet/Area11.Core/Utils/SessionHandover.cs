using Area11.Core.Services;

namespace Area11.Core.Utils;

/// <summary>
/// The note a forgotten session leaves behind (v0.35.0, D-92).
///
/// The case: a cashier walks away with the drawer open; the idle limit signs him out; the next
/// person signs in. The drawer is still open in the books - and it must stay open, because the only
/// way to close it is to **count the cash**, and Area11 must never invent that number. So the
/// program does not close it. It writes two words here instead ("left open by X at T") and refuses
/// bills until a person has counted the drawer and opened their own on the Shift (galla) page.
///
/// Why settings rather than a new table: two short strings, no history, no money. A key that is
/// missing reads as "nothing pending", so an old database opens with nothing to complain about.
/// </summary>
public static class SessionHandover
{
    public const string AtKey = "session.handoverAt";
    public const string ByKey = "session.handoverBy";

    /// <summary>Writes the note. Never throws: a note that cannot be written must not stop a sign-out.</summary>
    public static void Mark(SettingsService settings, string person)
    {
        try
        {
            settings.SetUiOnly(AtKey, Clock.Now());
            settings.SetUiOnly(ByKey, person);
        }
        catch (Exception)
        {
            // The sign-out matters more than the note; the Shift page still shows the open drawer.
        }
    }

    /// <summary>
    /// The sentence for the counter, or empty when nothing is pending. Anything wrong (no settings
    /// row, a locked file) reads as "nothing pending" - a gate that fails closed would stop the shop.
    /// </summary>
    public static string Read(SettingsService settings)
    {
        try
        {
            var at = settings.GetString(AtKey);

            if (string.IsNullOrWhiteSpace(at))
            {
                return string.Empty;
            }

            var who = settings.GetString(ByKey);
            var person = string.IsNullOrWhiteSpace(who) ? "the last person" : who;

            return "The drawer was left open by " + person + " at " + at +
                   " and has not been counted. Count the cash on the Shift (galla) page and open your own drawer before making a bill.";
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>Forgets the note - called when a drawer is opened, closed or handed over.</summary>
    public static void Clear(SettingsService settings)
    {
        try
        {
            settings.SetUiOnly(AtKey, string.Empty);
            settings.SetUiOnly(ByKey, string.Empty);
        }
        catch (Exception)
        {
            // Same reason as Mark: the drawer's own state is the truth, this is only the sign-post.
        }
    }
}
