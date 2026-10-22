using System.IO;
using System.Text;
using Area11.Core.Models;

namespace Area11.Core.Services;

/// <summary>
/// The wire between Area11 and the cloud (F-48). Two calls only - send one document, read one document -
/// so the whole sync can be tested with a fake that never touches the network.
/// </summary>
public interface ICloudTransport
{
    SyncTransportResult Send(string url, string key, string table, string json);

    SyncTransportResult Fetch(string url, string key, string table, string shopId);
}

/// <summary>
/// Supabase over its REST endpoint (PostgREST), which is the whole reason no extra package is needed
/// (Spec 25.2, Q-02).
///
/// Why the calls are synchronous: the page runs them from a button, on a shop counter, where the owner
/// waits a second or two anyway. A synchronous call cannot deadlock against the WPF dispatcher, and
/// every test can exercise the service without an async fixture.
/// </summary>
public sealed class SupabaseTransport : ICloudTransport
{
    /// <summary>One client for the shop's lifetime: a new one per push would leak sockets.</summary>
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    public SyncTransportResult Send(string url, string key, string table, string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(url, table) + "?on_conflict=shop_id")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.TryAddWithoutValidation("apikey", key);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);

        // merge-duplicates turns the push into an upsert on shop_id: pushing twice updates the same row.
        request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");

        return Execute(request);
    }

    public SyncTransportResult Fetch(string url, string key, string table, string shopId)
    {
        var query = "?select=shop_name,sent_at,version,payload&shop_id=eq."
                    + Uri.EscapeDataString(shopId)
                    + "&order=sent_at.desc&limit=1";

        var request = new HttpRequestMessage(HttpMethod.Get, Endpoint(url, table) + query);

        request.Headers.TryAddWithoutValidation("apikey", key);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);

        return Execute(request);
    }

    /// <summary>"https://xyz.supabase.co" + table name -> the table's REST address.</summary>
    private static string Endpoint(string url, string table) =>
        url.Trim().TrimEnd('/') + "/rest/v1/" + Uri.EscapeDataString(table.Trim());

    /// <summary>
    /// One place that turns every possible failure of the wire into a value. Nothing here throws: a shop
    /// with no internet must see a sentence, never a stack trace (Z-07).
    /// </summary>
    private static SyncTransportResult Execute(HttpRequestMessage request)
    {
        try
        {
            using var response = Client.Send(request);

            // Read the body through a stream and a reader: both calls have existed since the first .NET,
            // so this works on whichever runtime the shop's PC ends up with.
            using var stream = response.Content.ReadAsStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var body = reader.ReadToEnd();

            return response.IsSuccessStatusCode
                ? SyncTransportResult.Success((int)response.StatusCode, body)
                : SyncTransportResult.Fail((int)response.StatusCode, body);
        }
        catch (TaskCanceledException)
        {
            return SyncTransportResult.Fail(0, string.Empty,
                "The cloud did not answer within 30 seconds.");
        }
        catch (HttpRequestException exception)
        {
            return SyncTransportResult.Fail(0, string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            // A bad address (a URL with a space, an empty host) throws here, before any request is made.
            return SyncTransportResult.Fail(0, string.Empty, exception.Message);
        }
    }
}
