using System.IO;
using System.Text.Json;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>Documentation data only;not WPF/focus/ContextMenu or deployment execution.</summary>
public sealed class GuideLibraryTests
{
    private static GuideBundle Bundle()
    {
        var bundle = new GuideBundle { Version = "R1", Date = "2026-11-10" };
        foreach (var id in new[] { "sales", "stock", "customers", "money", "system", "basics" })
            bundle.Topics.Add(new GuideTopic { Id = id, Title = id, Path = id });
        bundle.Topics.Add(new GuideTopic
        {
            Id = "sales.no-bill", Parent = "sales", Title = "نو بل", Path = "سیل ← نو بل",
            Blocks = new() { new GuideBlock { Kind = "paragraph", Text = "اصل refund کی تصدیق" } }
        });
        bundle.Topics.Add(new GuideTopic
        {
            Id = "basics.login", Parent = "basics", Title = "Login", Path = "عام باتیں ← Login",
            Blocks = new() { new GuideBlock { Kind = "table", Rows = new() { new() { "Sign in", "داخلہ" } } } }
        });
        return bundle;
    }

    private static GuideLibrary Read(GuideBundle bundle)
    {
        var folder = Path.Combine(Path.GetTempPath(), "area11-guide-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder, "guide.json");
            File.WriteAllText(file, JsonSerializer.Serialize(bundle));
            return GuideLibrary.Load(file);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact] public void OpeningDataHasExactlySixRoots()
    {
        Assert.Equal(6, Read(Bundle()).Roots.Count);
        var packaged = GuideLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Guide", "guide.json"));
        Assert.Equal(6, packaged.Roots.Count);
        Assert.Equal("R2", packaged.Version);
        Assert.NotEmpty(packaged.Find("sales.no-bill")!.Blocks);
        Assert.NotEmpty(packaged.Find("system.settings.printing")!.Blocks);
        Assert.NotEmpty(packaged.Find("basics.login")!.Blocks);
    }
    [Fact] public void OriginalMetadataIsShared() { var g = Read(Bundle()); Assert.Equal("R1", g.Version); Assert.Equal("2026-11-10", g.Date); }
    [Fact] public void ChildKeepsItsStableParent() => Assert.Equal("sales.no-bill", Assert.Single(Read(Bundle()).Children("sales")).Id);
    [Fact] public void UnknownTopicIsNotInvented() => Assert.Null(Read(Bundle()).Find("fake.signup"));
    [Fact] public void SearchFindsArticleTextWithoutOpeningItsPage() => Assert.Equal("sales.no-bill", Assert.Single(Read(Bundle()).Search("refund")).Id);
    [Fact] public void SearchFindsTableCells() => Assert.Equal("basics.login", Assert.Single(Read(Bundle()).Search("Sign in")).Id);
    [Fact] public void SearchSupportsUrduLoginAlias() => Assert.Equal("basics.login", Assert.Single(Read(Bundle()).Search("لاگ ان")).Id);
    [Fact] public void EmptySearchShowsOnlySixRoots() => Assert.Equal(6, Read(Bundle()).Search("  ").Count);
    [Fact] public void MissingFileFailsWithoutCreatingAnything() => Assert.Throws<InvalidDataException>((Action)(() => GuideLibrary.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"))));
    [Fact] public void DuplicateIdentityRefuses() { var b = Bundle(); b.Topics.Add(b.Topics[0]); Assert.Throws<InvalidDataException>((Action)(() => Read(b))); }
    [Fact] public void MissingParentRefuses() { var b = Bundle(); b.Topics[^1].Parent = "unknown"; Assert.Throws<InvalidDataException>((Action)(() => Read(b))); }
    [Fact] public void MissingRelatedTopicRefuses() { var b = Bundle(); b.Topics[^1].Links.Add("unknown"); Assert.Throws<InvalidDataException>((Action)(() => Read(b))); }
    [Fact] public void CyclicHierarchyRefuses() { var b = Bundle(); b.Topics.Add(new GuideTopic { Id = "cycle", Title = "cycle", Parent = "cycle" }); Assert.Throws<InvalidDataException>((Action)(() => Read(b))); }
    [Fact] public void UnknownBlockIsNotExecuted() { var b = Bundle(); b.Topics[^1].Blocks.Add(new GuideBlock { Kind = "script", Text = "do not run" }); Assert.Throws<InvalidDataException>((Action)(() => Read(b))); }
    [Fact] public void InvalidDateRefuses() { var b = Bundle(); b.Date = "tomorrow"; Assert.Throws<InvalidDataException>((Action)(() => Read(b))); }
    [Fact] public void SeventhRootRefuses() { var b = Bundle(); b.Topics.Add(new GuideTopic { Id = "extra", Title = "extra" }); Assert.Throws<InvalidDataException>((Action)(() => Read(b))); }
    [Fact] public void NullTopicRefuses() { var b = Bundle(); b.Topics.Add(null!); Assert.Throws<InvalidDataException>((Action)(() => Read(b))); }
    [Fact] public void MissingGrandparentRefuses()
    {
        var b = Bundle(); b.Topics[^1].Parent = "later";
        b.Topics.Add(new GuideTopic { Id = "later", Title = "later", Parent = "missing" });
        Assert.Throws<InvalidDataException>((Action)(() => Read(b)));
    }
    [Fact] public void SearchFindsTitleNotInPath()
    {
        var b = Bundle(); b.Topics[^1].Title = "منفردعنوان";
        Assert.Equal("basics.login", Assert.Single(Read(b).Search("منفردعنوان")).Id);
    }
}
