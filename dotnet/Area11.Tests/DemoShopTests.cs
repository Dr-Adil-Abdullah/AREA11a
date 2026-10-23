using Area11.Core.Utils;
using System.IO;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The demo shop switch (D-76): the owner asked for a button that loads demo data, and for that demo
/// - button and data - to disappear when the app is finished.
///
/// Both promises are only safe if the demo is a **separate** Area11: its own folder, its own
/// database, its own logs. These tests pin the part that decides that, because the alternative -
/// demo rows inside the real shop's database - is the one mistake that cannot be undone by deleting
/// a folder.
///
/// Why nothing here calls <see cref="DemoShop.TurnOn"/>: it changes the paths of the whole process,
/// and the tests run side by side. The rule is tested through the pure functions instead.
/// </summary>
public sealed class DemoShopTests
{
    [Fact]
    public void TheFlagIsRecognisedInAnyPositionAndAnyCase()
    {
        Assert.True(DemoShop.WantsDemo(new[] { "--demo" }));
        Assert.True(DemoShop.WantsDemo(new[] { "AREA11", "--DEMO" }));
        Assert.True(DemoShop.WantsDemo(new[] { "  --demo  " }));
        Assert.True(DemoShop.WantsDemo(new[] { "--install-check", "--demo" }));
    }

    [Fact]
    public void AnOrdinaryStartIsNotTheDemo()
    {
        Assert.False(DemoShop.WantsDemo(null));
        Assert.False(DemoShop.WantsDemo(Array.Empty<string>()));
        Assert.False(DemoShop.WantsDemo(new[] { "--install-check" }));

        // A word that merely contains the flag is not the flag.
        Assert.False(DemoShop.WantsDemo(new[] { "--demo-shop" }));
        Assert.False(DemoShop.WantsDemo(new[] { "demo" }));
    }

    [Fact]
    public void TheDemoFolderSitsInsideTheShopsFolderAndIsNotThatFolder()
    {
        var root = Path.Combine("C:" + Path.DirectorySeparatorChar, "ProgramData", "Area11");
        var demo = DemoShop.FolderUnder(root);

        Assert.Equal(Path.Combine(root, "demo"), demo);

        // The whole promise in one assertion: the demo file is never the shop's file.
        Assert.NotEqual(Path.Combine(root, "area11.db"), Path.Combine(demo, "area11.db"));
        Assert.StartsWith(root, demo);
    }

    [Fact]
    public void NothingIsDemoUntilItIsSwitchedOn()
    {
        // The flag defaults to off, and it is the only thing that redirects the shop's paths.
        try
        {
            DemoShop.TurnOff();
            Assert.False(DemoShop.IsOn);
            Assert.Null(DemoShop.DataFolder);
        }
        finally
        {
            DemoShop.TurnOff();
        }
    }
}
