using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Spec 22.4 — new record, deleted record, changed fields only, and no-change.</summary>
public sealed class AuditDiffTests
{
    private sealed class Sample
    {
        public string Name { get; set; } = string.Empty;

        public long RetailPaisa { get; set; }

        public long CostPaisa { get; set; }
    }

    [Fact]
    public void Compare_NewRecord_KeepsFullNewValue()
    {
        var (oldValue, newValue) = AuditDiff.Compare(null, new Sample { Name = "Panadol", RetailPaisa = 3000 });

        Assert.Null(oldValue);
        Assert.NotNull(newValue);
        Assert.Contains("Panadol", newValue);
        Assert.Contains("3000", newValue);
    }

    [Fact]
    public void Compare_DeletedRecord_KeepsFullOldValue()
    {
        var (oldValue, newValue) = AuditDiff.Compare(new Sample { Name = "Panadol" }, null);

        Assert.NotNull(oldValue);
        Assert.Null(newValue);
    }

    [Fact]
    public void Compare_Edit_KeepsOnlyChangedFields()
    {
        var before = new Sample { Name = "Panadol", RetailPaisa = 3000, CostPaisa = 2000 };
        var after = new Sample { Name = "Panadol", RetailPaisa = 3500, CostPaisa = 2000 };

        var (oldValue, newValue) = AuditDiff.Compare(before, after);

        Assert.NotNull(oldValue);
        Assert.NotNull(newValue);
        Assert.Contains("RetailPaisa", oldValue);
        Assert.DoesNotContain("CostPaisa", oldValue);
        Assert.Contains("3000", oldValue);
        Assert.Contains("3500", newValue);
    }

    [Fact]
    public void Compare_NoChange_ProducesNothing()
    {
        var before = new Sample { Name = "Panadol", RetailPaisa = 3000 };
        var after = new Sample { Name = "Panadol", RetailPaisa = 3000 };

        var (oldValue, newValue) = AuditDiff.Compare(before, after);

        Assert.Null(oldValue);
        Assert.Null(newValue);
    }
}
