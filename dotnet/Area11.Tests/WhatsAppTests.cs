using System.IO;
using System.Text;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// Portion 12: one shared road to WhatsApp. The native-app link spells the phone number the same
/// way on every page, the PDF writer produces a real file without any library, and the profile
/// nudges say who owes what. Opening WhatsApp is not tested - only a human with the app can prove
/// that - but everything the shop prepares before the tap is pinned here.
/// </summary>
public sealed class WhatsAppTests
{
    // ============================== the phone number ==============================

    [Fact]
    public void APakistaniMobileBecomes92()
    {
        Assert.Equal("923001234567", WhatsAppService.NormalizePhone("0300-1234567"));
    }

    [Fact]
    public void Plus92AndBare92PassThrough()
    {
        Assert.Equal("923001234567", WhatsAppService.NormalizePhone("+92 300 1234567"));
        Assert.Equal("923001234567", WhatsAppService.NormalizePhone("923001234567"));
    }

    [Fact]
    public void ABlankPhoneMeansPickAContact()
    {
        Assert.Null(WhatsAppService.NormalizePhone(null));
        Assert.Null(WhatsAppService.NormalizePhone("   "));
    }

    [Fact]
    public void TheAppLinkCarriesPhoneAndText()
    {
        var link = WhatsAppService.BuildAppLink("0300-1234567", "Salam, your bill");

        Assert.StartsWith("whatsapp://send?phone=923001234567&text=", link);
        Assert.Contains(Uri.EscapeDataString("Salam, your bill"), link);
    }

    [Fact]
    public void TheAppLinkWithoutAPhoneOmitsIt()
    {
        var link = WhatsAppService.BuildAppLink(null, "hello");

        Assert.Equal("whatsapp://send?text=hello", link);
    }

    [Fact]
    public void TheWebLinkMatchesTheWaMeShape()
    {
        Assert.Equal(
            "https://wa.me/923001234567?text=hi",
            WhatsAppService.BuildWebLink("03001234567", "hi"));
        Assert.Equal(
            "https://wa.me/?text=hi",
            WhatsAppService.BuildWebLink(null, "hi"));
    }

    // ============================== the PDF writer ==============================

    [Fact]
    public void ThePdfStartsWithAHeaderAndCarriesTheTitle()
    {
        var text = Encoding.ASCII.GetString(PdfWriter.Write("Bill INV-0001", new[] { "Panadol", "Rs 100" }));

        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("Bill INV-0001", text);
        Assert.Contains("Panadol", text);
        Assert.Contains("trailer", text);
    }

    [Fact]
    public void ThePdfEscapesParentheses()
    {
        var text = Encoding.ASCII.GetString(PdfWriter.Write("T", new[] { "Augmentin (625mg)" }));

        Assert.Contains("Augmentin \\(625mg\\)", text);
    }

    [Fact]
    public void UrduBecomesAnHonestPlaceholderOnThePdf()
    {
        var text = Encoding.ASCII.GetString(PdfWriter.Write("T", new[] { "علی رضا" }));

        Assert.DoesNotContain("علی", text);
        Assert.Contains("?", text);
    }

    [Fact]
    public void ALongDocumentPaginates()
    {
        var lines = Enumerable.Range(1, 150).Select(index => "line " + index).ToList();

        var text = Encoding.ASCII.GetString(PdfWriter.Write("Long", lines));

        Assert.Contains("/Count 3", text);
        Assert.Contains("line 150", text);
    }

    [Fact]
    public void WrapBreaksLongLinesAt95Columns()
    {
        var chunks = PdfWriter.Wrap(new[] { new string('x', 200) });

        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, chunk => Assert.True(chunk.Length <= PdfWriter.Columns));
    }

    // ============================== the profile nudges ==============================

    [Fact]
    public void TheCustomerNudgeNamesWhoOwesWhat()
    {
        using var test = new TestDatabase();
        var service = new WhatsAppService(test.Settings, test.Log);

        var text = service.BuildCustomerProfile("Ali Raza", "0300-1234567", 150000, 500000);

        Assert.Contains("Ali Raza", text);
        Assert.Contains(Money.Format(150000), text);
        Assert.Contains("outstanding", text);
    }

    [Fact]
    public void ASettledKhataSaysThankYou()
    {
        using var test = new TestDatabase();
        var service = new WhatsAppService(test.Settings, test.Log);

        var text = service.BuildCustomerProfile("Ali Raza", null, 0, 0);

        Assert.Contains("settled", text);
    }

    [Fact]
    public void TheSupplierNoteNamesThePayable()
    {
        using var test = new TestDatabase();
        var service = new WhatsAppService(test.Settings, test.Log);

        var text = service.BuildSupplierProfile("Sami Pharma", "City agency", 250000);

        Assert.Contains("Sami Pharma", text);
        Assert.Contains("City agency", text);
        Assert.Contains(Money.Format(250000), text);
    }

    // ============================== the prepared file ==============================

    [Fact]
    public void PreparePdfWritesARealFileWithASafeName()
    {
        using var test = new TestDatabase();
        var folder = Path.Combine(Path.GetTempPath(), "area11-test-" + Guid.NewGuid().ToString("N"));

        try
        {
            var stored = test.Settings.Set(MasterFoundationSettings.WhatsAppExportDirectory, folder, TestData.Owner);
            Assert.True(stored.Ok, stored.Error);

            var service = new WhatsAppService(test.Settings, test.Log);
            var pdf = service.PreparePdf("Bill INV-1", new[] { "line one" }, "bill INV-1?.pdf");

            Assert.True(pdf.Ok, pdf.Error);
            Assert.StartsWith(folder, pdf.Value!);
            Assert.EndsWith("bill INV-1_.pdf", pdf.Value);
            Assert.True(File.Exists(pdf.Value));
            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(File.ReadAllBytes(pdf.Value!)));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Fact]
    public void PreparePdfRefusesABlankTitle()
    {
        using var test = new TestDatabase();
        var service = new WhatsAppService(test.Settings, test.Log);

        var pdf = service.PreparePdf("  ", new[] { "line" }, "x.pdf");

        Assert.False(pdf.Ok);
    }
}
