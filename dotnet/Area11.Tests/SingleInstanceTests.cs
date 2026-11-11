using System.Threading;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// One shop, one copy (owner's word, Nov 2026): a second Area11 must close with a sentence, never
/// open beside the first and sell from the same shelf.
///
/// The lock is a named system mutex, so the "second copy" in these tests is a second THREAD: a
/// mutex is recursive on its own thread, and two guards on one thread would both claim to be
/// first. In the shop the two copies are two processes - always different threads - so the tests
/// prove the shape that matters. Every name is a throwaway GUID: the suite must never grab (or
/// refuse) the real shop's lock on the owner's PC.
/// </summary>
public sealed class SingleInstanceTests
{
    [Fact]
    public void TheFirstCopy_HoldsTheLock()
    {
        using var first = SingleInstanceGuard.TryAcquire(NewName());

        Assert.True(first.IsFirst);
    }

    [Fact]
    public void ASecondCopy_IsRefusedWhileTheFirstHoldsIt()
    {
        var name = NewName();
        using var first = SingleInstanceGuard.TryAcquire(name);

        Assert.True(first.IsFirst);

        var secondFirst = true;
        var second = new Thread(() =>
        {
            using var guard = SingleInstanceGuard.TryAcquire(name);
            secondFirst = guard.IsFirst;
        });

        second.Start();
        second.Join();

        Assert.False(secondFirst);
    }

    [Fact]
    public void AfterTheFirstCloses_ANewCopyMayOpen()
    {
        var name = NewName();

        using (var first = SingleInstanceGuard.TryAcquire(name))
        {
            Assert.True(first.IsFirst);
        }

        using var next = SingleInstanceGuard.TryAcquire(name);

        Assert.True(next.IsFirst);
    }

    [Fact]
    public void AKilledCopy_DoesNotBarTheShopDoor()
    {
        // A Task Manager kill (or a power cut) dies holding the lock; Windows reports the mutex
        // abandoned, and the next start must take it over - not refuse to open.
        var name = NewName();

        var holder = new Thread(() =>
        {
            var mutex = new Mutex(false, name);
            mutex.WaitOne();
            // No release, no dispose: this thread dies with the lock in its hand.
        });

        holder.Start();
        holder.Join();

        using var next = SingleInstanceGuard.TryAcquire(name);

        Assert.True(next.IsFirst);
    }

    private static string NewName() => @"Local\Area11_Test_" + Guid.NewGuid().ToString("N");
}
