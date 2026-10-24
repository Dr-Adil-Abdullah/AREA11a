using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The misspelt-medicine search (F-50, Spec 8.4): a cashier types "panadl" and the shop still finds
/// Panadol - without ever guessing what was meant.
///
/// Why every rule here is a test: a typo-tolerant search is the one feature that can hand a
/// pharmacist the wrong box while looking helpful. So the tests pin both halves of the promise - that
/// the obvious misspellings are found, and that a different medicine is never presented as the same
/// one.
/// </summary>
public sealed class FuzzySearchTests
{
    // ============================== the obvious typos ==============================

    [Fact]
    public void AMisspeltMedicineIsStillFound()
    {
        using var test = new TestDatabase();
        Add(test, "Panadol 500mg", "Paracetamol");

        var found = Smart(test, "panadl");

        Assert.True(found.Ok, found.Error);
        Assert.True(found.Value!.IsFuzzy);
        Assert.Equal("Panadol 500mg", found.Value.Items[0].Name);
        Assert.Contains("panadl", found.Value.NoteLabel);
        Assert.Contains("Panadol 500mg", found.Value.NoteLabel);
    }

    [Fact]
    public void TwoLettersSwappedAreOneMistakeNotTwo()
    {
        using var test = new TestDatabase();
        Add(test, "Augmentin 625mg", "Amoxicillin");

        // "augmnetin": the n and e swapped - the commonest fault on a keyboard.
        var found = Smart(test, "augmnetin");

        Assert.True(found.Value!.IsFuzzy);
        Assert.Equal("Augmentin 625mg", found.Value.Items[0].Name);
    }

    [Fact]
    public void TheSaltIsSearchedWhenTheBrandIsNotRemembered()
    {
        using var test = new TestDatabase();
        Add(test, "Risek 20mg", "Omeprazole");

        // A real misspelling, not a prefix: "omeprazle" has the o and the l the wrong way round.
        // The old test typed "omeprazol", which is a PREFIX of the salt - and the exact search
        // matches a prefix through LIKE, so the answer came back exact and was (correctly) not
        // labelled a guess. The test, not the search, was wrong; the owner's run of v0.28.0 found it.
        var found = Smart(test, "omeprazle");

        Assert.True(found.Value!.IsFuzzy);
        Assert.Equal("Risek 20mg", found.Value.Items[0].Name);
    }

    [Fact]
    public void APrefixOfTheSaltIsFoundExactlyAndIsNotCalledAGuess()
    {
        using var test = new TestDatabase();
        Add(test, "Risek 20mg", "Omeprazole");

        // The other half of the same rule: half a word is a normal way to search, and when it finds
        // the medicine the page says nothing about typos - there was no typo to forgive.
        var found = Smart(test, "omeprazol");

        Assert.True(found.Ok, found.Error);
        Assert.False(found.Value!.IsFuzzy);
        Assert.Equal("Risek 20mg", found.Value.Items[0].Name);
    }

    [Fact]
    public void TheStrengthAndTheWordTabletAreIgnoredByTheTypos()
    {
        using var test = new TestDatabase();
        Add(test, "Panadol 500mg", "Paracetamol");
        Add(test, "Panadine Extra 500mg", "Paracetamol");

        var found = Smart(test, "panadl 500mg tablets");

        Assert.True(found.Value!.IsFuzzy);
        Assert.Equal("Panadol 500mg", found.Value.Items[0].Name);
    }

    // ============================== it never guesses too much ==============================

    [Fact]
    public void AShortWordIsNeverMatchedByDistanceAlone()
    {
        using var test = new TestDatabase();
        Add(test, "Zinc", "Zinc sulphate");

        // "zink" is only four letters and one edit away - a shop that guesses here would also match
        // "Zest", "Zina" and every other four-letter name in the catalog.
        var found = Smart(test, "zink");

        Assert.False(found.Value!.IsFuzzy);
        Assert.True(found.Value.IsEmpty);
    }

    [Fact]
    public void ADifferentMedicineIsNeverOfferedAsTheSameOne()
    {
        using var test = new TestDatabase();
        Add(test, "Panadol 500mg", "Paracetamol");
        Add(test, "Augmentin 625mg", "Amoxicillin");

        var found = Smart(test, "augmentin");

        Assert.False(found.Value!.IsFuzzy);
        Assert.Single(found.Value.Items);
        Assert.Equal("Augmentin 625mg", found.Value.Items[0].Name);
    }

    [Fact]
    public void EachTypedWordMustLandSomewhere()
    {
        using var test = new TestDatabase();
        Add(test, "Panadol 500mg", "Paracetamol");

        // "panadol xyz" must not become "panadol" just because one word matched.
        var found = Smart(test, "panadol xyzzy");

        Assert.True(found.Value!.IsEmpty);
    }

    [Fact]
    public void AnUnrelatedWordFindsNothingRatherThanEverything()
    {
        using var test = new TestDatabase();
        Add(test, "Panadol 500mg", "Paracetamol");
        Add(test, "Risek 20mg", "Omeprazole");

        var found = Smart(test, "refrigerator");

        Assert.True(found.Value!.IsEmpty);
        Assert.Equal("No product matches \"refrigerator\".", found.Value.SummaryLabel);
    }

    // ============================== the exact answer is never second-guessed ==============================

    [Fact]
    public void AnExactSearchStaysExact()
    {
        using var test = new TestDatabase();
        Add(test, "Panadol 500mg", "Paracetamol");

        var found = Smart(test, "panadol");

        Assert.False(found.Value!.IsFuzzy);
        Assert.Equal(string.Empty, found.Value.NoteLabel);
        Assert.Single(found.Value.Items);
    }

    [Fact]
    public void ABarcodeIsNeverSentThroughTheTypos()
    {
        using var test = new TestDatabase();
        var productId = Add(test, "Panadol 500mg", "Paracetamol");

        using var connection = test.Database.Open();
        test.Database.Execute(connection,
            "UPDATE products SET barcode = '8964000111012' WHERE id = @id;", new { id = productId });

        var found = Smart(test, "8964000111012");

        Assert.False(found.Value!.IsFuzzy);
        Assert.Single(found.Value.Items);
    }

    // ============================== the rules of the matcher itself ==============================

    [Fact]
    public void TheDistanceCountsOneSwapAsOne()
    {
        Assert.Equal(1, Fuzzy.Distance("augmnetin", "augmentin"));
        Assert.Equal(1, Fuzzy.Distance("panadl", "panadol"));
        Assert.Equal(3, Fuzzy.Distance("amoxilin", "amoxicillin"));
        Assert.Equal(0, Fuzzy.Distance("risek", "risek"));
    }

    [Fact]
    public void TheToleranceGrowsWithTheWordAndStopsAtThree()
    {
        Assert.Equal(0, Fuzzy.Tolerance(4));  // "zink" must not become "Zinc"
        Assert.Equal(1, Fuzzy.Tolerance(5));
        Assert.Equal(1, Fuzzy.Tolerance(7));
        Assert.Equal(2, Fuzzy.Tolerance(8));
        Assert.Equal(2, Fuzzy.Tolerance(11));
        Assert.Equal(3, Fuzzy.Tolerance(12));
        Assert.Equal(3, Fuzzy.Tolerance(40));
    }

    [Fact]
    public void ThePackagingWordsAndTheStrengthAreNotPartOfTheName()
    {
        Assert.Equal(new[] { "panadol" }, Fuzzy.Words("Panadol 500mg Tablets"));
        Assert.Equal(new[] { "risek" }, Fuzzy.Words("Risek 20 mg capsules"));
        Assert.Empty(Fuzzy.Words("500mg tablets"));
    }

    [Fact]
    public void ABeginningScoresHigherThanTheMiddleOfAName()
    {
        Assert.Equal(100, Fuzzy.WordScore("panadol", "panadol"));
        Assert.True(Fuzzy.WordScore("panad", "panadol") > Fuzzy.WordScore("adol", "panadol"));
    }

    [Fact]
    public void TheSuggestionSentenceNamesWhatWasTypedAndWhatWasFound()
    {
        var sentence = Fuzzy.SuggestionLabel("panadl", "Panadol 500mg");

        Assert.Contains("panadl", sentence);
        Assert.Contains("Panadol 500mg", sentence);
        Assert.Contains("Check the box", sentence);
        Assert.Equal(string.Empty, Fuzzy.SuggestionLabel("  ", "Panadol 500mg"));
    }

    // ============================== it obeys the shop's own filters ==============================

    [Fact]
    public void ACompanyFilterIsKeptWhenTheListIsOnlyASuggestion()
    {
        using var test = new TestDatabase();
        var first = Add(test, "Panadol 500mg", "Paracetamol");
        Add(test, "Panadol Extra", "Paracetamol + Caffeine");

        var companyId = test.Database.Tx(connection =>
        {
            test.Database.Execute(connection,
                "INSERT INTO companies(name, active) VALUES('GSK', 1);");
            return (int)test.Database.NextId(connection);
        });

        using (var connection = test.Database.Open())
        {
            test.Database.Execute(connection,
                "UPDATE products SET company_id = @companyId WHERE id = @id;",
                new { companyId, id = first });
        }

        var found = Smart(test, "panadl", companyId: companyId);

        Assert.True(found.Value!.IsFuzzy);
        Assert.Single(found.Value.Items);
        Assert.Equal("Panadol 500mg", found.Value.Items[0].Name);
    }

    // ============================== helpers ==============================

    private static int Add(TestDatabase test, string name, string generic)
    {
        var id = TestData.AddProduct(test, name: name);

        using var connection = test.Database.Open();

        test.Database.Execute(connection,
            "UPDATE products SET generic = @generic WHERE id = @id;", new { generic, id });

        return id;
    }

    private static ServiceResult<ProductSearchResult> Smart(
        TestDatabase test, string term, int? companyId = null) =>
        new ProductService(test.Database, test.Audit, test.Log).SearchSmart(term, companyId: companyId);
}
