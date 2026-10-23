using Area11.Core.Utils;
using System;
using System.Linq;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// An empty page must say why it is empty (v0.33.0, D-89).
///
/// Why: a blank grid is the most common thing a shopkeeper sees and the least useful. "Nothing here
/// yet" sends him to add a medicine; "nothing matches what you typed" sends him to widen the search.
/// One sentence for both is how a shopkeeper concludes that the program lost his data.
/// </summary>
public sealed class EmptyPageTests
{
    [Fact]
    public void AnEmptyPage_SaysNothingHereYet()
    {
        var hint = EmptyPage.For("Products");

        Assert.Equal("Nothing here yet.", hint.Headline);
        Assert.Contains("Nothing here yet", hint.Sentence);
    }

    [Fact]
    public void ASearchThatFindsNothing_IsNotAnEmptyShop()
    {
        var hint = EmptyPage.For("Products", searched: true);

        // The distinction that matters: the rows may exist, the box is too narrow.
        Assert.Equal("Nothing matches what you typed.", hint.Headline);
        Assert.Contains("Clear the search box", hint.Advice);
        Assert.DoesNotContain("Nothing here yet", hint.Sentence);
    }

    [Fact]
    public void EveryPageTheShopCanOpen_HasItsOwnWords()
    {
        var pages = EmptyPage.KnownPages.ToList();

        Assert.True(pages.Count >= 25, $"only {pages.Count} pages have an empty-page sentence");

        foreach (var page in pages)
        {
            Assert.False(string.IsNullOrWhiteSpace(page));

            var advice = EmptyPage.For(page).Advice;

            Assert.True(advice.Length >= 30, $"{page}: the advice is too short to help - \"{advice}\"");
            Assert.DoesNotContain("No data", advice);
            Assert.DoesNotContain("no data", advice);
        }

        // Two pages sharing one sentence means one of them is telling the operator the wrong thing.
        var repeated = pages
            .GroupBy(page => EmptyPage.For(page).Advice, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" / ", group))
            .ToList();

        Assert.Empty(repeated);
    }

    [Fact]
    public void TheAdvice_IsTheShopsOwnWords_NotAGenericSentence()
    {
        // Three pages, three different answers - each one names the thing that fills it.
        Assert.Contains("Excel / CSV", EmptyPage.For("Products").Advice);
        Assert.Contains("counter", EmptyPage.For("Bills").Advice);
        Assert.Contains("reorder", EmptyPage.For("Reorder pad").Advice);
        Assert.Contains("works alone", EmptyPage.For("Second counter (LAN)").Advice);
    }

    [Fact]
    public void APageNobodyWroteWordsFor_StillGetsAnAnswer()
    {
        var hint = EmptyPage.For("A page that does not exist");

        // An unknown title must not leave a blank card: the card exists to explain, and a blank
        // explanation is worse than the empty grid it replaced.
        Assert.False(string.IsNullOrWhiteSpace(hint.Advice));
        Assert.Equal("Nothing here yet.", hint.Headline);
        Assert.Contains("A page that does not exist", hint.PageTitle);
    }

    [Fact]
    public void APageWithRows_GetsNoHintAtAll()
    {
        // This is the shape the window binds to: null means "hide the card", so no page has to keep
        // a second flag in step with its list.
        Assert.Null(EmptyPage.When(nothing: false, "Products"));
    }

    [Fact]
    public void APageWithNoRows_GetsAHintAndKeepsItsOwnTitle()
    {
        var hint = EmptyPage.When(nothing: true, "Bills");

        Assert.NotNull(hint);
        Assert.Equal("Bills", hint!.PageTitle);
    }

    [Fact]
    public void TheSentence_IsTheHeadlineAndTheAdvice()
    {
        var hint = EmptyPage.For("Stock-take");

        // One string for anything that can only show one line, and it must not swallow a half.
        Assert.Equal(hint.Headline + " " + hint.Advice, hint.Sentence);
    }
}
