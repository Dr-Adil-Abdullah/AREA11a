using System.Net;
using System.Net.Sockets;

namespace Area11.Core.Utils;

/// <summary>
/// The one place that talks to a **time server** (v0.35.0, D-92). NTP over UDP, no package, no
/// dependency, and every failure is "no answer" rather than an exception in the till's path.
///
/// Why NTP and not a web page: it is one 48-byte packet and a reply, it works on any Windows PC with
/// no certificate, no proxy and no DNS trickery beyond the name itself, and it is what Windows'
/// own time service uses. Three servers are tried in turn so one closed port in a shop's router does
/// not decide the shop's clock.
/// </summary>
internal static class NtpTime
{
    /// <summary>Seconds between 1900-01-01 (NTP's zero) and 1970-01-01 (Unix's zero).</summary>
    private const long NtpToUnixSeconds = 2208988800L;

    private static readonly string[] Servers =
    {
        "time.windows.com",
        "pool.ntp.org",
        "time.google.com"
    };

    /// <summary>The true time, or null when no server answered in time. Never throws.</summary>
    public static DateTimeOffset? Query(int timeoutMilliseconds)
    {
        foreach (var server in Servers)
        {
            try
            {
                var answer = AskOne(server, timeoutMilliseconds);

                if (answer is not null)
                {
                    return answer;
                }
            }
            catch (Exception)
            {
                // A server that cannot be reached is not a fault of the shop; try the next one.
            }
        }

        return null;
    }

    private static DateTimeOffset? AskOne(string server, int timeoutMilliseconds)
    {
        var addresses = Dns.GetHostAddresses(server);

        if (addresses.Length == 0)
        {
            return null;
        }

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.ReceiveTimeout = Math.Clamp(timeoutMilliseconds, 250, 10_000);
        socket.SendTimeout = socket.ReceiveTimeout;

        // LI = 0 (no leap warning), version 3, mode 3 (client).
        var request = new byte[48];
        request[0] = 0x1B;

        socket.Connect(new IPEndPoint(addresses[0], 123));
        socket.Send(request);

        var response = new byte[48];
        var received = socket.Receive(response);

        if (received < 48 || response[0] == 0)
        {
            return null;
        }

        // The transmit timestamp is the last 8 bytes: 4 of seconds since 1900, 4 of fraction.
        var seconds = ReadBigEndian(response, 40);
        var fraction = ReadBigEndian(response, 44);

        if (seconds < NtpToUnixSeconds)
        {
            return null;
        }

        var secondsSinceUnix = (long)seconds - NtpToUnixSeconds;
        var ticks = (long)(fraction * (10_000_000.0 / 4_294_967_296.0));

        try
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(secondsSinceUnix).AddTicks(ticks);

            // A server that answers with a year outside living memory is a wrong answer, not a clock.
            return time.Year is >= 2000 and <= 2100 ? time : null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static uint ReadBigEndian(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << 24) |
        ((uint)buffer[offset + 1] << 16) |
        ((uint)buffer[offset + 2] << 8) |
        buffer[offset + 3];
}
