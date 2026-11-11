using System.Security;
using Area11.Core.Models;
using Area11.Core.Services;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// How Area11 starts and how it may be closed (Portion 16, roadmap #16). The promises that must hold:
///   • a fresh shop starts and closes by hand; auto and protected are opt-in, owner only;
///   • the Windows entry is written before the setting claims it - and when Windows refuses
///     (no administrator), nothing changes and the sentence says how to save again;
///   • closing permission belongs to the owner plus whoever he ticks - never a role default;
///   • the × prompt proves the secret with the same throttle as sign-in, and the black box knows
///     who unlocked, who was refused, and when Shift-at-startup reset the mode;
///   • recovery always lands on manual, even when the Windows entry is stuck.
/// The Windows entry is a memory fake here: the suite never touches the registry it runs on.
/// </summary>
public sealed class StartupCloseTests
{
    [Fact]
    public void FreshShop_StartsAndClosesByHand()
    {
        using var test = new TestDatabase();

        var startup = Startup(test, new FakeAutoStart());

        Assert.Equal(StartupService.Manual, startup.Mode);
        Assert.False(startup.IsProtected);
        Assert.False(startup.WantsAutoStart);
        Assert.Contains("by hand", startup.ModeLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void ModeValidation_RefusesUnknownWords()
    {
        using var test = new TestDatabase();

        Assert.False(test.Settings.Set(StartupService.ModeKey, "sometimes", TestData.Owner).Ok);
        Assert.True(test.Settings.Set(StartupService.ModeKey, "auto", TestData.Owner).Ok);
        Assert.True(test.Settings.Set(StartupService.ModeKey, "protected", TestData.Owner).Ok);
        Assert.True(test.Settings.Set(StartupService.ModeKey, "manual", TestData.Owner).Ok);
    }

    [Fact]
    public void OnlyTheOwner_ChangesTheMode()
    {
        using var test = new TestDatabase();
        var store = new FakeAutoStart();
        var startup = Startup(test, store);

        var refused = startup.SetMode(TestData.Cashier(test), "auto", @"C:\Area11\Area11.exe");

        Assert.False(refused.Ok);
        Assert.Contains("Only the owner", refused.Error!, StringComparison.Ordinal);
        Assert.False(store.Enabled);
        Assert.Equal(StartupService.Manual, startup.Mode);
    }

    [Fact]
    public void SetMode_Auto_RegistersWindowsFirst()
    {
        using var test = new TestDatabase();
        var store = new FakeAutoStart();
        var startup = Startup(test, store);

        var saved = startup.SetMode(TestData.Owner, "auto", @"C:\Area11\Area11.exe");

        Assert.True(saved.Ok, saved.Error);
        Assert.True(store.Enabled);
        Assert.Equal(@"C:\Area11\Area11.exe", store.ExePath);
        Assert.Equal(StartupService.Auto, startup.Mode);
        Assert.True(startup.WantsAutoStart);
        Assert.False(startup.IsProtected);
    }

    [Fact]
    public void SetMode_Manual_RemovesTheEntry()
    {
        using var test = new TestDatabase();
        var store = new FakeAutoStart();
        var startup = Startup(test, store);

        Assert.True(startup.SetMode(TestData.Owner, "protected", @"C:\Area11\Area11.exe").Ok);
        Assert.True(store.Enabled);

        var saved = startup.SetMode(TestData.Owner, "manual", @"C:\Area11\Area11.exe");

        Assert.True(saved.Ok, saved.Error);
        Assert.False(store.Enabled);
        Assert.Equal(StartupService.Manual, startup.Mode);
    }

    [Fact]
    public void WithoutAdmin_ThisAccountStillStarts()
    {
        using var test = new TestDatabase();
        var machine = new FakeAutoStart { ThrowAdmin = true };
        var user = new FakeAutoStart();
        var startup = Startup(test, machine, user);

        var saved = startup.SetMode(TestData.Owner, "protected", @"C:\Area11\Area11.exe");

        Assert.True(saved.Ok, saved.Error);
        Assert.False(machine.Enabled);
        Assert.True(user.Enabled);
        Assert.Equal(@"C:\Area11\Area11.exe", user.ExePath);
        Assert.Equal(StartupService.Protected, startup.Mode);
        Assert.Contains("this Windows account only", Assert.Single(saved.Warnings));
        Assert.Contains("this Windows account only", startup.ScopeText);
    }

    [Fact]
    public void WithoutAdminAnywhere_NothingChanges()
    {
        using var test = new TestDatabase();
        var machine = new FakeAutoStart { ThrowAdmin = true };
        var user = new FakeAutoStart { ThrowAdmin = true };
        var startup = Startup(test, machine, user);

        var refused = startup.SetMode(TestData.Owner, "protected", @"C:\Area11\Area11.exe");

        Assert.False(refused.Ok);
        Assert.Contains("administrator", refused.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nothing was changed", refused.Error!, StringComparison.Ordinal);
        Assert.False(machine.Enabled);
        Assert.False(user.Enabled);
        Assert.Equal(StartupService.Manual, startup.Mode);
    }

    [Fact]
    public void SecurityRefusal_FallsBackToThisAccount()
    {
        // The owner's screenshot, 11 Nov 2026: HKLM without an administrator answers with
        // SecurityException ("Requested registry access is not allowed."), not with
        // UnauthorizedAccessException - and the fallback must catch that shape too.
        using var test = new TestDatabase();
        var machine = new FakeAutoStart { ThrowSecurity = true };
        var user = new FakeAutoStart();
        var startup = Startup(test, machine, user);

        var saved = startup.SetMode(TestData.Owner, "auto", @"C:\Area11\Area11.exe");

        Assert.True(saved.Ok, saved.Error);
        Assert.False(machine.Enabled);
        Assert.True(user.Enabled);
        Assert.Equal(StartupService.Auto, startup.Mode);
        Assert.Contains("this Windows account only", Assert.Single(saved.Warnings));
    }

    [Fact]
    public void SecurityRefusalEverywhere_NothingChanges()
    {
        using var test = new TestDatabase();
        var machine = new FakeAutoStart { ThrowSecurity = true };
        var user = new FakeAutoStart { ThrowSecurity = true };
        var startup = Startup(test, machine, user);

        var refused = startup.SetMode(TestData.Owner, "auto", @"C:\Area11\Area11.exe");

        Assert.False(refused.Ok);
        Assert.Contains("administrator", refused.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nothing was changed", refused.Error!, StringComparison.Ordinal);
        Assert.False(machine.Enabled);
        Assert.False(user.Enabled);
        Assert.Equal(StartupService.Manual, startup.Mode);
    }

    [Fact]
    public void MachineSuccess_RemovesTheUserEntry()
    {
        using var test = new TestDatabase();
        var machine = new FakeAutoStart();
        var user = new FakeAutoStart();
        user.Enable(@"C:\Area11\Area11.exe");
        var startup = Startup(test, machine, user);

        var saved = startup.SetMode(TestData.Owner, "auto", @"C:\Area11\Area11.exe");

        Assert.True(saved.Ok, saved.Error);
        Assert.True(machine.Enabled);
        Assert.False(user.Enabled);
        Assert.Empty(saved.Warnings);
        Assert.Contains("every Windows account", startup.ScopeText);
    }

    [Fact]
    public void Manual_DisablesBothSides()
    {
        using var test = new TestDatabase();
        var machine = new FakeAutoStart();
        var user = new FakeAutoStart();
        machine.Enable(@"C:\Area11\Area11.exe");
        user.Enable(@"C:\Area11\Area11.exe");
        var startup = Startup(test, machine, user);

        // A previous admin save must not strand an entry neither side removes.
        var saved = startup.SetMode(TestData.Owner, "manual", @"C:\Area11\Area11.exe");

        Assert.True(saved.Ok, saved.Error);
        Assert.False(machine.Enabled);
        Assert.False(user.Enabled);
        Assert.Equal(StartupService.Manual, startup.Mode);
    }

    [Fact]
    public void Closing_BelongsToTheOwnerPlusAnExplicitTick()
    {
        Assert.Equal("Close the program", Modules.Label(Modules.CloseApp));
        Assert.Contains(Modules.CloseApp, Modules.All);
        Assert.True(PermissionService.RoleDefault(UserRole.Owner, Modules.CloseApp));
        Assert.False(PermissionService.RoleDefault(UserRole.Manager, Modules.CloseApp));
        Assert.False(PermissionService.RoleDefault(UserRole.Cashier, Modules.CloseApp));
    }

    [Fact]
    public void ExplicitTick_LetsStaffClose()
    {
        using var test = new TestDatabase();
        var permissions = new PermissionService(test.Database, test.Log, test.Audit);
        var staff = AddStaff(test, "Bilal", "1234");

        Assert.False(permissions.Can(staff, Modules.CloseApp));

        Assert.True(permissions.Set(TestData.Owner, staff.Id, Modules.CloseApp, true).Ok);
        Assert.True(permissions.Can(staff, Modules.CloseApp));
    }

    [Fact]
    public void VerifyClose_OwnerSecret_Unlocks()
    {
        using var test = new TestDatabase();
        var startup = Startup(test, new FakeAutoStart());

        // Empty name means the owner, exactly like sign-in; "area11" is the first-start secret.
        var unlocked = startup.VerifyClose(string.Empty, "area11");

        Assert.True(unlocked.Ok, unlocked.Error);
        Assert.True(unlocked.Value!.IsOwner);
        Assert.Equal(1, Audits(test, "close_unlocked"));
    }

    [Fact]
    public void VerifyClose_WrongSecret_Refused()
    {
        using var test = new TestDatabase();
        var startup = Startup(test, new FakeAutoStart());

        var refused = startup.VerifyClose(string.Empty, "definitely-wrong");

        Assert.False(refused.Ok);
        Assert.Contains("not correct", refused.Error!, StringComparison.Ordinal);
        Assert.Equal(1, Audits(test, "unlock_failed"));
        Assert.Equal(0, Audits(test, "close_unlocked"));
    }

    [Fact]
    public void VerifyClose_ProvedButUnpermitted_Refused()
    {
        using var test = new TestDatabase();
        var staff = AddStaff(test, "Waseem", "1234");
        var startup = Startup(test, new FakeAutoStart());

        var refused = startup.VerifyClose(staff.Name, "1234");

        Assert.False(refused.Ok);
        Assert.Contains("may not close", refused.Error!, StringComparison.Ordinal);
        Assert.Equal(1, Audits(test, "unlock"));          // the secret was right...
        Assert.Equal(1, Audits(test, "close_refused"));   // ...but the permission was not.
        Assert.Equal(0, Audits(test, "close_unlocked"));
    }

    [Fact]
    public void VerifyClose_PermittedStaff_Unlocks()
    {
        using var test = new TestDatabase();
        var staff = AddStaff(test, "Aslam", "1234");
        var permissions = new PermissionService(test.Database, test.Log, test.Audit);
        Assert.True(permissions.Set(TestData.Owner, staff.Id, Modules.CloseApp, true).Ok);

        var unlocked = Startup(test, new FakeAutoStart()).VerifyClose(staff.Name, "1234");

        Assert.True(unlocked.Ok, unlocked.Error);
        Assert.Equal(staff.Id, unlocked.Value!.Id);
        Assert.Equal(1, Audits(test, "close_unlocked"));
    }

    [Fact]
    public void GuessingAtThePrompt_IsSlowed()
    {
        using var test = new TestDatabase();
        var startup = Startup(test, new FakeAutoStart());

        // Five wrong guesses against a name nobody else uses, then the door holds shut.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.False(startup.VerifyClose("ghost-closing", "wrong").Ok);
        }

        var locked = startup.VerifyClose("ghost-closing", "wrong");

        Assert.False(locked.Ok);
        Assert.Contains("Too many attempts", locked.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetToManual_ClearsModeAndEntry()
    {
        using var test = new TestDatabase();
        var store = new FakeAutoStart();
        var startup = Startup(test, store);

        Assert.True(startup.SetMode(TestData.Owner, "protected", @"C:\Area11\Area11.exe").Ok);

        var reset = startup.ResetToManual();

        Assert.True(reset.Ok, reset.Error);
        Assert.False(store.Enabled);
        Assert.Equal(StartupService.Manual, startup.Mode);
        Assert.Equal(1, Audits(test, "protection_reset"));
    }

    [Fact]
    public void ResetToManual_SurvivesAStuckEntry()
    {
        using var test = new TestDatabase();
        var store = new FakeAutoStart { ThrowStuck = true };
        var startup = Startup(test, store);

        // The setting cannot be stranded on protected by a registry Windows will not release.
        var reset = startup.ResetToManual();

        Assert.True(reset.Ok, reset.Error);
        Assert.Equal(StartupService.Manual, startup.Mode);
        Assert.Equal(1, Audits(test, "protection_reset"));
    }

    // ============================== the rig ==============================

    private sealed class FakeAutoStart : IAutoStartStore
    {
        public bool Enabled { get; private set; }

        public string? ExePath { get; private set; }

        public bool ThrowAdmin { get; init; }

        public bool ThrowSecurity { get; init; }

        public bool ThrowStuck { get; init; }

        public void Enable(string exePath)
        {
            if (ThrowAdmin)
            {
                throw new UnauthorizedAccessException("access denied");
            }

            if (ThrowSecurity)
            {
                throw new SecurityException("Requested registry access is not allowed.");
            }

            Enabled = true;
            ExePath = exePath;
        }

        public void Disable()
        {
            if (ThrowAdmin)
            {
                throw new UnauthorizedAccessException("access denied");
            }

            if (ThrowStuck)
            {
                throw new InvalidOperationException("stuck");
            }

            Enabled = false;
            ExePath = null;
        }

        public bool IsEnabled() => Enabled;
    }

    private static StartupService Startup(TestDatabase test, IAutoStartStore machine, IAutoStartStore? user = null) =>
        new(test.Settings,
            new AuthService(test.Database, test.Audit, test.Settings, test.Log),
            new PermissionService(test.Database, test.Log, test.Audit),
            test.Audit,
            test.Log,
            machine,
            user ?? new FakeAutoStart());

    private static User AddStaff(TestDatabase test, string name, string pin)
    {
        var auth = new AuthService(test.Database, test.Audit, test.Settings, test.Log);
        var created = auth.CreateStaff(TestData.Owner, name, UserRole.Cashier, pin);
        Assert.True(created.Ok, created.Error);

        return new User { Id = created.Value, Name = name, Role = "cashier", Active = true };
    }

    private static long Audits(TestDatabase test, string action)
    {
        using var connection = test.Database.Open();

        return Convert.ToInt64(test.Database.Scalar(connection,
            "SELECT COUNT(1) FROM audit_logs WHERE action = @action;", new { action }) ?? 0L);
    }
}
