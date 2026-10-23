using System.Globalization;

namespace Area11.Core.Utils;

/// <summary>
/// The shop's typo-tolerant matcher (F-50, Spec 8.4): a medicine typed as "panadl" or "amoxilin" is
/// still found, without ever guessing what the cashier meant.
///
/// Why this is a separate, pure class: the counter, the product list and the no-bill return all ask
/// the same question - "did the person in front of me mean this medicine?" - and a shop that answers
/// it two different ways would show two different lists for one typo. Everything here is a score and
/// nothing here is a decision: the caller decides what to do with the ranking (show it, never bill it).
///
/// Why the distance is small on purpose: in a pharmacy "Norvasc" and "Norvasc 5" are the same shelf
/// but "Panadol" and "Panadine" are not, and a matcher that is generous will one day hand a cashier
/// the wrong box. The rules below are narrow: whole words, a short edit distance, and a hard refusal
/// to match anything shorter than four letters by distance alone.
/// </summary>
public static class Fuzzy
{
    /// <summary>
    /// Shorter than this and a typo is indistinguishable from a different medicine: "zink" is one
    /// letter from "Zinc", and one letter from "Zina", "Zinca" and every other four-letter name.
    /// </summary>
    public const int ShortestFuzzyWord = 5;

    /// <summary>How many suggestions a page shows before it stops being a list and starts being noise.</summary>
    public const int DefaultLimit = 20;

    private static readonly char[] Separators =
        { ' ', '\t', '\n', '\r', '-', '_', '/', '\\', ',', '.', '(', ')', '[', ']', '+', '&', '\'', '"' };

    /// <summary>
    /// Lower-case words with the digits and the packaging words removed: "Panadol 500mg Tablets"
    /// becomes "panadol". A strength is a number, and a number is what the exact search already
    /// handles - letting "500" take part in a typo score would make every 500mg medicine a match
    /// for every other one.
    /// </summary>
    public static string[] Words(string? text)
    {
        var clean = (text ?? string.Empty).ToLowerInvariant();

        foreach (var separator in Separators)
        {
            clean = clean.Replace(separator.ToString(), " ", StringComparison.Ordinal);
        }

        var words = new List<string>();

        foreach (var part in clean.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var letters = new string(part.Where(char.IsLetter).ToArray());

            if (letters.Length == 0 || IsPackagingWord(letters))
            {
                continue;
            }

            words.Add(letters);
        }

        return words.ToArray();
    }

    /// <summary>
    /// The words every box carries ("tablets", "syrup", "injection"), which say what the medicine IS,
    /// not which medicine it is. Removing them keeps "syrup" from matching every bottle on the shelf.
    /// </summary>
    private static bool IsPackagingWord(string word) => word is
        "tablet" or "tablets" or "tab" or "tabs" or "capsule" or "capsules" or "cap" or "caps"
        or "syrup" or "suspension" or "drops" or "drop" or "injection" or "inj" or "injections"
        or "sachet" or "sachets" or "cream" or "ointment" or "lotion" or "gel" or "spray"
        or "bottle" or "box" or "strip" or "strips" or "pack" or "ml" or "mg" or "gm" or "g" or "kg";

    /// <summary>
    /// The number of letters a word of this length may get wrong: 1 for a short word, 2 for a long
    /// one, and none at all below <see cref="ShortestFuzzyWord"/>. Why this shape: one wrong letter
    /// in "risek" already covers every four-letter medicine in the shop, so the tolerance has to grow
    /// with the word or the list stops being useful.
    /// </summary>
    public static int Tolerance(int length) => length switch
    {
        < ShortestFuzzyWord => 0,
        <= 7 => 1,
        <= 11 => 2,
        _ => 3
    };

    /// <summary>
    /// Edit distance with the one slip every typist makes: two letters swapped ("panadlo" for
    /// "panadol"). Plain Levenshtein charges 2 for a transposition, which makes the most common
    /// typing fault on a keyboard look like two separate mistakes.
    /// </summary>
    public static int Distance(string? left, string? right)
    {
        var a = (left ?? string.Empty).ToLowerInvariant();
        var b = (right ?? string.Empty).ToLowerInvariant();

        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var rows = a.Length + 1;
        var columns = b.Length + 1;
        var grid = new int[rows, columns];

        for (var row = 0; row < rows; row++)
        {
            grid[row, 0] = row;
        }

        for (var column = 0; column < columns; column++)
        {
            grid[0, column] = column;
        }

        for (var row = 1; row < rows; row++)
        {
            for (var column = 1; column < columns; column++)
            {
                var cost = a[row - 1] == b[column - 1] ? 0 : 1;

                var best = Math.Min(
                    Math.Min(grid[row - 1, column] + 1, grid[row, column - 1] + 1),
                    grid[row - 1, column - 1] + cost);

                if (row > 1 && column > 1
                    && a[row - 1] == b[column - 2] && a[row - 2] == b[column - 1])
                {
                    best = Math.Min(best, grid[row - 2, column - 2] + 1);
                }

                grid[row, column] = best;
            }
        }

        return grid[rows - 1, columns - 1];
    }

    /// <summary>
    /// How well one typed word matches one word of a medicine: 100 for the same word, 90 for a
    /// beginning, 70 for a beginning of the other way round, 60 for "inside", and 40 down to 11 for
    /// a short typo. 0 means "not this word".
    ///
    /// Why "inside" scores below a beginning: someone who types "adol" is halfway through "panadol",
    /// which is a normal way to search; someone who types "risek" and lands inside "risekplus" is a
    /// match too, but the beginning of a name is what a person usually remembers.
    /// </summary>
    public static int WordScore(string? typedWord, string? targetWord)
    {
        var typed = (typedWord ?? string.Empty).Trim().ToLowerInvariant();
        var target = (targetWord ?? string.Empty).Trim().ToLowerInvariant();

        if (typed.Length == 0 || target.Length == 0)
        {
            return 0;
        }

        if (string.Equals(typed, target, StringComparison.Ordinal))
        {
            return 100;
        }

        if (target.StartsWith(typed, StringComparison.Ordinal))
        {
            // A shorter beginning is a little weaker: "pan" is a guess, "panad" is nearly the name.
            return 90 - Math.Min(10, target.Length - typed.Length);
        }

        if (typed.StartsWith(target, StringComparison.Ordinal))
        {
            return 70;
        }

        if (target.Contains(typed, StringComparison.Ordinal))
        {
            return 60;
        }

        var tolerance = Tolerance(typed.Length);

        if (tolerance == 0 || Math.Abs(typed.Length - target.Length) > tolerance + 1)
        {
            return 0;
        }

        var distance = Distance(typed, target);

        return distance <= tolerance ? Math.Max(11, 41 - (distance * 10)) : 0;
    }

    /// <summary>
    /// A whole search text against a whole name (and, when the caller has one, the salt): every typed
    /// word must land somewhere, or the answer is 0. Why "every": "pan 500" must not become "any
    /// medicine with 500 in it" just because "pan" matched.
    /// </summary>
    public static int Score(string? typed, string? name, string? salt = null)
    {
        var typedWords = Words(typed);

        if (typedWords.Length == 0)
        {
            return 0;
        }

        var targetWords = Words(name).Concat(Words(salt)).ToArray();

        if (targetWords.Length == 0)
        {
            return 0;
        }

        var total = 0;

        foreach (var typedWord in typedWords)
        {
            var best = 0;

            foreach (var targetWord in targetWords)
            {
                best = Math.Max(best, WordScore(typedWord, targetWord));

                if (best == 100)
                {
                    break;
                }
            }

            if (best == 0)
            {
                return 0;
            }

            total += best;
        }

        return total;
    }

    /// <summary>
    /// The best rows for a misspelt search, strongest first. Why a list and not one answer: the paper
    /// in front of the cashier is the only thing that can settle "Panadol" against "Panadine", so the
    /// program offers the neighbourhood and lets the human point.
    /// </summary>
    public static List<FuzzyHit<T>> Rank<T>(
        string? typed,
        IEnumerable<T> items,
        Func<T, string?> name,
        Func<T, string?>? salt = null,
        int limit = DefaultLimit)
    {
        var hits = new List<FuzzyHit<T>>();

        if (Words(typed).Length == 0)
        {
            return hits;
        }

        foreach (var item in items)
        {
            var score = Score(typed, name(item), salt is null ? null : salt(item));

            if (score > 0)
            {
                hits.Add(new FuzzyHit<T> { Item = item, Score = score });
            }
        }

        return hits
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => name(hit.Item) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    /// <summary>The shortest, clearest way to say what the program guessed - the page says this, not more.</summary>
    public static string SuggestionLabel(string? typed, string? closestName)
    {
        var clean = (typed ?? string.Empty).Trim();

        if (clean.Length == 0 || string.IsNullOrWhiteSpace(closestName))
        {
            return string.Empty;
        }

        return "Nothing is spelt \"" + clean + "\". Closest: " + closestName!.Trim()
               + ". Check the box before you bill it.";
    }
}

/// <summary>One row of a typo-tolerant search, with the score that put it there.</summary>
public sealed class FuzzyHit<T>
{
    public T Item { get; init; } = default!;

    public int Score { get; init; }

    /// <summary>"100" is the same word typed; anything under 60 is a guess the page must label.</summary>
    public bool IsExact => Score >= 100;

    public string ScoreLabel => Score.ToString(CultureInfo.InvariantCulture);
}
