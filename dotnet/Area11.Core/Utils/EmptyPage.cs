namespace Area11.Core.Utils;

/// <summary>
/// Why a page has nothing on it, in the shop's words (v0.33.0, D-89).
///
/// Why this exists: an empty list is the most common thing a shopkeeper sees, and it says nothing.
/// "Nothing here yet" and "nothing matches what you typed" are two completely different situations -
/// the first needs the shop to start entering, the second needs the operator to widen the search -
/// and a blank grid answers neither. Every page therefore names itself and says what would fill it.
/// </summary>
public sealed class EmptyHint
{
    /// <summary>The menu row this page is, as it is written on the screen.</summary>
    public string PageTitle { get; init; } = string.Empty;

    /// <summary>True when a search or a date box is narrowing the list - the rows may exist.</summary>
    public bool Searched { get; init; }

    /// <summary>The one-line answer to "why is this page empty?", half the answer.</summary>
    public string Headline => Searched ? "Nothing matches what you typed." : "Nothing here yet.";

    /// <summary>The other half: what fills this page, or what to do with the search boxes.</summary>
    public string Advice { get; init; } = string.Empty;

    /// <summary>Headline and advice as one sentence, which is what the window shows.</summary>
    public string Sentence => Headline + " " + Advice;
}

/// <summary>
/// A page that can be shown with no rows implements this, so the window can say why it is empty
/// (v0.33.0, D-89). The answer is computed, never stored: a page that fills itself does not have to
/// remember to clear anything.
/// </summary>
public interface IEmptyPage
{
    /// <summary>The words for an empty page, or null when the page has rows to show.</summary>
    EmptyHint? EmptyHint { get; }
}

/// <summary>
/// The shop's own answer, per page, to "why is this page empty?" (v0.33.0, D-89).
///
/// Every page the menu can open has an entry here - the static checker fails the build if one is
/// missing - because a generic sentence ("no data") is the sentence that made the owner ask the
/// question in the first place. Each line says **what would fill the page**, not what is absent.
/// </summary>
public static class EmptyPage
{
    /// <summary>
    /// One line per page: what would put something on it. The keys are the menu titles, spelled
    /// exactly as the shell writes them, so the check below can prove the list is complete.
    /// </summary>
    private static readonly Dictionary<string, string> Advices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dashboard"] =
            "The first bill of the day fills this page - until then the shop has nothing to summarise.",
        ["Counter (POS)"] =
            "Type a medicine name or scan a box and the list fills as you type. A new shop has no "
            + "medicines until one is added on Products, or imported from Excel / CSV.",
        ["Bills"] =
            "A bill appears here the moment the counter makes one. Nothing was deleted - a shop "
            + "that has just been installed has not billed yet.",
        ["Returns"] =
            "A return appears here when something comes back to the shelf or to quarantine. New "
            + "bills are on Bills.",
        ["No bill (PR)"] =
            "A return without a bill appears here. Make one from Returns when the customer has "
            + "lost the paper.",
        ["Products"] =
            "Add the first medicine with 'New medicine', or bring the shelf list in from Excel / CSV.",
        ["Reorder pad"] =
            "Nothing is running out. A medicine appears here when its shelf reaches the reorder "
            + "level set in Settings.",
        ["Write-off"] =
            "Nothing has been written off. Expired, broken or lost stock appears here after you "
            + "save a write-off.",
        ["Stock minus"] =
            "No shelf is below zero. This page fills when a sale takes stock the shop never received.",
        ["Stock-take"] =
            "A count appears here after you save a stock-take on this page.",
        ["Customers"] =
            "No customer yet. Add the first one with 'New customer' - the khata needs a name to "
            + "hang on.",
        ["Udhaar recovery"] =
            "Nobody owes the shop money. A customer appears here after a bill left on the khata.",
        ["Suppliers"] =
            "No supplier yet. Add the company that delivers the stock, then its page shows what the "
            + "shop owes it.",
        ["Doctors & reps"] =
            "No doctor or rep yet. Mark a customer as Doctor, or write a company's rep on its card.",
        ["Shift (galla)"] =
            "No drawer has been counted yet. Open the drawer at the start of the day and it appears here.",
        ["Expenses"] =
            "Nothing has been paid out yet. Rent, salary and owner drawings appear here when you "
            + "save one.",
        ["Reports"] =
            "Nothing was sold in this period. Choose a wider period above - the bills are there, "
            + "the range is not.",
        ["Tax report"] =
            "No bills in this month yet. Tax appears here after the first bill of the month.",
        ["Black box"] =
            "The black box is empty. Every change in the shop is written here - a new shop has none yet.",
        ["Excel / CSV"] =
            "No file has been read yet. Choose a CSV file and press Preview; nothing is written "
            + "until you press Import.",
        ["Backup & restore"] =
            "No backup yet. Press 'Back up now' - one ZIP of the database and the photos.",
        ["Second counter (LAN)"] =
            "This PC works alone. The page fills when a second counter joins this shop's folder.",
        ["Cloud sync"] =
            "Nothing has been sent to the cloud yet. Fill the settings above, then press Push.",
        ["Own fields"] =
            "No field of your own yet. Add one and it appears on the medicine, customer or supplier "
            + "form at once.",
        ["Printer & scanner"] =
            "No printer was found on this PC. Install the shop's printer, then press 'Look again'.",
        ["Check the books"] =
            "No findings. Every document adds up; press 'Check again' after a busy day.",
        ["Alerts"] =
            "Nothing needs attention. Ended alerts stay on record below."
    };

    /// <summary>
    /// The hint for a page that has no rows, or **null when it has rows** - the shape a view-model
    /// binds to, so a page never has to keep a second flag in step with its own list.
    /// </summary>
    public static EmptyHint? When(bool nothing, string pageTitle, bool searched = false) =>
        nothing ? For(pageTitle, searched) : null;

    /// <summary>
    /// The words for one page. An unknown title still gets a usable answer - a page whose name was
    /// spelled differently must not show an empty space where the explanation belongs.
    /// </summary>
    public static EmptyHint For(string pageTitle, bool searched = false) => new()
    {
        PageTitle = pageTitle,
        Searched = searched,
        Advice = searched
            // A search that finds nothing is not an empty shop, and saying "nothing here yet" would
            // send the operator looking for lost data.
            ? "Clear the search box or widen the dates - the rows are still there."
            : Advices.TryGetValue(pageTitle, out var advice)
                ? advice
                : "Nothing has been entered on this page yet."
    };

    /// <summary>The page titles this table answers for - a test and the checker read it.</summary>
    public static IReadOnlyCollection<string> KnownPages => Advices.Keys;
}
