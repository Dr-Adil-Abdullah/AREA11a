using System;
using Area11.Core.Utils;
using Xunit;

namespace Area11.Tests;

/// <summary>
/// The shop's time (v0.35.0, D-92): taken from a time server when one answers, from the computer
/// when none does - and never allowed to stop the till.
///
/// `Probe` is the seam: the tests never touch the network, so they run on a shop PC with the cable
/// unplugged exactly as they run here.
/// </summary>
public sealed class TimeSourceTests : IDisposable
{
    public TimeSourceTests() => TimeSource.Reset();

    public void Dispose() => TimeSource.Reset();

    [Fact]
    public void An_online_answer_is_used_and_every_stamp_follows_it()
    {
        var answer = DateTimeOffset.Now.AddSeconds(90);
        TimeSource.Probe = () => answer;

        var accepted = TimeSource.TrySync(200);

        Assert.True(accepted);
        Assert.True(TimeSource.IsOnline);
        Assert.Equal(TimeOrigin.Online, TimeSource.Origin);
        Assert.InRange(TimeSource.Offset.TotalSeconds, 89, 91);

        // The one line that matters: the bills of the whole program move with it.
        Assert.InRange((TimeSource.Now - DateTime.Now).TotalSeconds, 85, 95);
        Assert.Contains("online", TimeSource.Note);
    }

    [Fact]
    public void With_no_answer_the_computer_clock_is_kept_and_the_till_carries_on()
    {
        TimeSource.Probe = () => null;

        var accepted = TimeSource.TrySync(200);

        Assert.False(accepted);
        Assert.False(TimeSource.IsOnline);
        Assert.Equal(TimeSpan.Zero, TimeSource.Offset);
        Assert.Contains("no time server answered", TimeSource.Note);
        Assert.InRange((TimeSource.Now - DateTime.Now).TotalSeconds, -5, 5);
    }

    [Fact]
    public void A_probe_that_throws_is_the_same_as_no_answer()
    {
        TimeSource.Probe = () => throw new InvalidOperationException("no route to host");

        var accepted = TimeSource.TrySync(200);

        Assert.False(accepted);
        Assert.Equal(TimeOrigin.Computer, TimeSource.Origin);
        Assert.Contains("could not be asked", TimeSource.Note);
    }

    [Fact]
    public void An_answer_more_than_two_days_out_is_treated_as_a_wrong_answer()
    {
        // A server that says 1980, or one that answers in another world - either way the shop's
        // bills must not jump. The computer clock is kept and the reason is written down.
        TimeSource.Probe = () => DateTimeOffset.Now.AddDays(-3);

        var accepted = TimeSource.TrySync(200);

        Assert.False(accepted);
        Assert.Equal(TimeOrigin.Computer, TimeSource.Origin);
        Assert.Contains("too large to be drift", TimeSource.Note);
    }

    [Fact]
    public void The_edge_that_separates_drift_from_a_wrong_answer_is_two_days()
    {
        // The number is asserted, not a 1.9-day answer: applying a two-day correction, even for a
        // millisecond, would move every stamp another test is reading at the same moment.
        Assert.Equal(TimeSpan.FromDays(2), TimeSource.LargestAcceptedOffset);
    }

    [Fact]
    public void The_line_on_the_settings_page_says_where_the_time_came_from()
    {
        var before = TimeSource.DescribeForOwner();

        TimeSource.Probe = () => DateTimeOffset.Now.AddSeconds(2);
        TimeSource.TrySync(200);

        var online = TimeSource.DescribeForOwner();

        TimeSource.Probe = () => null;
        TimeSource.TrySync(200);

        var offline = TimeSource.DescribeForOwner();

        Assert.Contains("not reached a time server", before);
        Assert.Contains("time server (checked", online);
        Assert.Contains("No time server answered", offline);
        Assert.Contains("every 20 minutes", offline);
    }

    [Fact]
    public void A_difference_is_printed_in_words_a_shopkeeper_can_read()
    {
        Assert.Equal("less than a second", TimeSource.Describe(TimeSpan.FromMilliseconds(400)));
        Assert.Equal("+2.0 s", TimeSource.Describe(TimeSpan.FromSeconds(2)));
        Assert.Equal("-2.0 min", TimeSource.Describe(TimeSpan.FromMinutes(-2)));
        Assert.Equal("+3.5 h", TimeSource.Describe(TimeSpan.FromHours(3.5)));
    }

    [Fact]
    public void A_later_online_answer_replaces_the_earlier_offline_verdict()
    {
        // The line comes back at noon (D-92: "sync the moment there is internet"). The morning's
        // bills are already stamped with the computer clock; from here on the server's time is used.
        TimeSource.Probe = () => null;
        TimeSource.TrySync(200);
        Assert.False(TimeSource.IsOnline);

        TimeSource.Probe = () => DateTimeOffset.Now.AddMinutes(4);
        TimeSource.TrySync(200);

        Assert.True(TimeSource.IsOnline);
        Assert.InRange(TimeSource.Offset.TotalMinutes, 3.9, 4.1);
    }
}
