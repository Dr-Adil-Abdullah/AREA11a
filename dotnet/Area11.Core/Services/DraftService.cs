using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// Safe local sale/purchase drafts (master Portion 5, Q1=A, D-100 shared).
///
/// **Why a draft is not a held cart.** A held cart parks a priced cart for a few minutes on the same
/// counter. A draft parks *incomplete work* - a sale without a customer, a purchase without a
/// supplier, lines whose stock is not there yet - under a key any permitted person can open, finish
/// and post, today or next week. Purchases have no hold at all; drafts cover both.
///
/// **The five promises:**
///   1. a draft changes **nothing**: no sale, purchase, payment, stock, balance, number or report row
///      until a separately validated final post (the `document_drafts` table is not in sync either);
///   2. final post runs the **same** `SaleService.Create` / `PurchaseService.Create` the counter and
///      the purchase page run, so every guard (D-93, discount caps, cost lock) is re-checked at post
///      time against the then-current stock - no guard is weakened or bypassed;
///   3. drafts are **shared** (D-100): whoever holds the page permission may open, update, post or
///      discard any draft of that kind - never by hiding, always by permission;
///   4. a post can never happen twice: the draft is pre-marked `posted` with a marker id **before**
///      the document is created, so a second post - or a retry after a crash - is refused;
///   5. a failed post restores the draft to `draft` with the latest payload, so no work is lost and
///      no half-written document exists.
///
/// **Crash honesty.** `Database.Tx` cannot nest, so the pre-mark, the document and the final mark are
/// three separate transactions. If the process dies between the document commit and the final mark,
/// the draft stays `posted` with marker id -1: the document **exists** (see the Bills / Purchases
/// page) and the draft can never post again. Such rows are listed flagged (`NeedsPostCheck`) and can
/// only be discarded, never reopened - reopening could double the money. Discarding one is audited.
/// </summary>
public sealed class DraftService
{
    /// <summary>A draft is lines and a partner, not a novel; past this the payload is refused.</summary>
    public const int MaxPayloadChars = 256 * 1024;

    /// <summary>Pre-mark id: set before the document exists, replaced by the real id after.</summary>
    public const int PostingMarkerId = -1;

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly SaleService _sales;
    private readonly PurchaseService _purchases;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public DraftService(
        Database db,
        AuditService audit,
        SaleService sales,
        PurchaseService purchases,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _audit = audit;
        _sales = sales;
        _purchases = purchases;
        _permissions = permissions;
        _log = log;
    }

    // ------------------------------------------------------------------ save

    /// <summary>
    /// Parks the counter's cart. Nothing is priced, nothing is posted: an incomplete sale is exactly
    /// what a draft is for. Pass <paramref name="draftId"/> to overwrite an open draft.
    /// </summary>
    public ServiceResult<DocumentDraft> SaveSaleDraft(User? user, SaleInput input, int draftId = 0)
    {
        if (!_permissions.Can(user, Modules.Pos))
        {
            return ServiceResult<DocumentDraft>.Fail("You do not have permission to save sale drafts.");
        }

        if (input.Lines.Count == 0)
        {
            return ServiceResult<DocumentDraft>.Fail("There is nothing to save - the cart is empty.");
        }

        return Save(user, DraftType.Sale, input, draftId);
    }

    /// <summary>
    /// Parks the purchase page. The supplier may be missing (0 = not chosen yet): choosing him is
    /// part of finishing the draft, not of saving it.
    /// </summary>
    public ServiceResult<DocumentDraft> SavePurchaseDraft(User? user, PurchaseInput input, int draftId = 0)
    {
        if (!_permissions.Can(user, Modules.Purchases))
        {
            return ServiceResult<DocumentDraft>.Fail("You do not have permission to save purchase drafts.");
        }

        if (input.Lines.Count == 0)
        {
            return ServiceResult<DocumentDraft>.Fail("There is nothing to save - add at least one line.");
        }

        return Save(user, DraftType.Purchase, input, draftId);
    }

    private ServiceResult<DocumentDraft> Save<TInput>(User? user, string type, TInput input, int draftId)
    {
        string payload;
        try
        {
            payload = JsonSerializer.Serialize(input, Json.WriterOptions);
        }
        catch (Exception exception)
        {
            _log.Error("A draft could not be packed.", exception);
            return ServiceResult<DocumentDraft>.Fail("This draft could not be saved.");
        }

        if (payload.Length > MaxPayloadChars)
        {
            return ServiceResult<DocumentDraft>.Fail("This draft is too large to save.");
        }

        var name = (user?.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return ServiceResult<DocumentDraft>.Fail("The signed-in user has no name, so a draft cannot be saved.");
        }

        try
        {
            if (draftId > 0)
            {
                var updated = _db.Tx(connection =>
                {
                    var row = ReadRow(connection, draftId);
                    if (row is null)
                    {
                        return ServiceResult<DocumentDraft>.Fail("That draft is not in the list any more.");
                    }
                    if (row.Status != DraftStatus.Draft || row.DraftType != type)
                    {
                        return ServiceResult<DocumentDraft>.Fail("Only an open draft can be updated.");
                    }

                    _db.Execute(connection, @"
UPDATE document_drafts SET payload_json = @payload, updated_at = @at WHERE id = @id;",
                        new { payload, at = Clock.Now(), id = draftId });
                    _audit.Record(connection, user, DraftType.ModuleFor(type), "draft_updated",
                        "document_drafts", draftId.ToString(), newValue: row.DraftKey);
                    row.PayloadJson = payload;
                    return ServiceResult<DocumentDraft>.Success(row);
                });
                return updated;
            }

            // A readable key: type, minute and randomness. A collision retries with a fresh key.
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var key = type + "-" + TimeSource.Now.ToString("yyyyMMdd-HHmmss")
                    + "-" + Guid.NewGuid().ToString("N")[..4];
                try
                {
                    var created = _db.Tx(connection =>
                    {
                        _db.Execute(connection, @"
INSERT INTO document_drafts(draft_key, draft_type, payload_json, status, created_by_id,
                            created_by_name, created_at, updated_at)
VALUES(@key, @type, @payload, 'draft', @byId, @by, @at, @at);",
                            new
                            {
                                key,
                                type,
                                payload,
                                byId = user!.Id,
                                by = name,
                                at = Clock.Now()
                            });
                        var id = (int)_db.NextId(connection);
                        _audit.Record(connection, user, DraftType.ModuleFor(type), "draft_saved",
                            "document_drafts", id.ToString(), newValue: key);
                        return ReadRow(connection, id)!;
                    });
                    return ServiceResult<DocumentDraft>.Success(created);
                }
                catch (Exception exception) when (IsUniqueViolation(exception) && attempt < 4)
                {
                    // Same second, same random suffix: try another key.
                }
            }

            return ServiceResult<DocumentDraft>.Fail("This draft could not be saved. Please try again.");
        }
        catch (ValidationException exception)
        {
            return ServiceResult<DocumentDraft>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("A draft could not be saved.", exception);
            return ServiceResult<DocumentDraft>.Fail("The draft could not be saved.");
        }
    }

    // ------------------------------------------------------------------ list & open

    /// <summary>Open drafts of one kind, newest first, plus any interrupted-post markers.</summary>
    public ServiceResult<List<DraftSummary>> List(User? user, string draftType)
    {
        var type = DraftType.Clean(draftType);
        if (!DraftType.IsKnown(type))
        {
            return ServiceResult<List<DraftSummary>>.Fail("That kind of draft does not exist.");
        }

        if (!_permissions.Can(user, DraftType.ModuleFor(type)))
        {
            return ServiceResult<List<DraftSummary>>.Fail("You do not have permission to see these drafts.");
        }

        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<DocumentDraft>(connection, @"
SELECT id, draft_key AS draftKey, draft_type AS draftType, payload_json AS payloadJson,
       status, created_by_id AS createdById, created_by_name AS createdByName,
       created_at AS createdAt, updated_at AS updatedAt,
       posted_document_id AS postedDocumentId, posted_at AS postedAt, discard_reason AS discardReason
FROM document_drafts
WHERE draft_type = @type AND (status = 'draft' OR (status = 'posted' AND posted_document_id = @marker))
ORDER BY updated_at DESC, id DESC;",
                new { type, marker = PostingMarkerId });

            var summaries = new List<DraftSummary>();
            foreach (var row in rows)
            {
                var (lines, partnerId) = DescribePayload(row.PayloadJson);
                summaries.Add(new DraftSummary
                {
                    Id = row.Id,
                    DraftKey = row.DraftKey,
                    DraftType = row.DraftType,
                    CreatedByName = row.CreatedByName,
                    UpdatedAt = row.UpdatedAt,
                    Lines = lines,
                    PartnerName = PartnerName(connection, type, partnerId),
                    NeedsPostCheck = row is { Status: DraftStatus.Posted, PostedDocumentId: PostingMarkerId }
                });
            }

            return ServiceResult<List<DraftSummary>>.Success(summaries);
        }
        catch (Exception exception)
        {
            _log.Error("Drafts could not be listed.", exception);
            return ServiceResult<List<DraftSummary>>.Fail("The drafts could not be loaded.");
        }
    }

    public ServiceResult<SaleInput> OpenSale(User? user, int draftId) =>
        Open<SaleInput>(user, draftId, DraftType.Sale);

    public ServiceResult<PurchaseInput> OpenPurchase(User? user, int draftId) =>
        Open<PurchaseInput>(user, draftId, DraftType.Purchase);

    private ServiceResult<TInput> Open<TInput>(User? user, int draftId, string type)
        where TInput : class
    {
        if (!_permissions.Can(user, DraftType.ModuleFor(type)))
        {
            return ServiceResult<TInput>.Fail("You do not have permission to open these drafts.");
        }

        try
        {
            using var connection = _db.Open();
            var row = ReadRow(connection, draftId);
            if (row is null || row.DraftType != type)
            {
                return ServiceResult<TInput>.Fail("That draft is not in the list any more.");
            }
            if (row.Status == DraftStatus.Posted && row.PostedDocumentId == PostingMarkerId)
            {
                return ServiceResult<TInput>.Fail(
                    "A previous post of this draft was interrupted. If the document is on its page, the post " +
                    "succeeded - discard this row. It can never post again, so no double document is possible.");
            }
            if (row.Status != DraftStatus.Draft)
            {
                return ServiceResult<TInput>.Fail("Only an open draft can be opened.");
            }

            TInput? input;
            try
            {
                input = JsonSerializer.Deserialize<TInput>(row.PayloadJson, Json.ReadOptions);
            }
            catch (JsonException exception)
            {
                _log.Error($"Draft {draftId} could not be read.", exception);
                return ServiceResult<TInput>.Fail("This draft is damaged and cannot be opened. Discard it and start again.");
            }

            if (input is null)
            {
                return ServiceResult<TInput>.Fail("This draft is damaged and cannot be opened. Discard it and start again.");
            }

            return ServiceResult<TInput>.Success(input);
        }
        catch (Exception exception)
        {
            _log.Error("A draft could not be opened.", exception);
            return ServiceResult<TInput>.Fail("The draft could not be opened.");
        }
    }

    // ------------------------------------------------------------------ discard

    /// <summary>
    /// Puts a draft away (D-00b spirit): the row stays as `discarded`, never deleted. Posted rows -
    /// including interrupted-post markers - can also be cleared this way once verified.
    /// </summary>
    public ServiceResult Discard(User? user, int draftId, string? reason = null)
    {
        // The kind is read first so the permission gate runs before any transaction opens.
        DocumentDraft? probe;
        try
        {
            using var probeConnection = _db.Open();
            probe = ReadRow(probeConnection, draftId);
        }
        catch (Exception exception)
        {
            _log.Error("A draft could not be discarded.", exception);
            return ServiceResult.Fail("The draft could not be discarded.");
        }

        if (probe is null)
        {
            return ServiceResult.Fail("That draft is not in the list any more.");
        }
        if (!_permissions.Can(user, DraftType.ModuleFor(probe.DraftType)))
        {
            return ServiceResult.Fail("You do not have permission to discard this draft.");
        }

        var cleanReason = (reason ?? string.Empty).Trim();
        if (cleanReason.Length > 200)
        {
            return ServiceResult.Fail("The reason is too long - keep it under 200 letters.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var row = ReadRow(connection, draftId);
                if (row is null)
                {
                    return ServiceResult.Fail("That draft is not in the list any more.");
                }
                if (row.Status == DraftStatus.Discarded)
                {
                    return ServiceResult.Success();
                }
                if (row is { Status: DraftStatus.Posted, PostedDocumentId: not PostingMarkerId })
                {
                    return ServiceResult.Fail("A posted draft is history and cannot be discarded.");
                }

                _db.Execute(connection, @"
UPDATE document_drafts
SET status = 'discarded', discard_reason = @reason, updated_at = @at
WHERE id = @id;",
                    new { reason = cleanReason.Length == 0 ? null : cleanReason, at = Clock.Now(), id = draftId });
                _audit.Record(connection, user, DraftType.ModuleFor(row.DraftType), "draft_discarded",
                    "document_drafts", draftId.ToString(), oldValue: row.DraftKey,
                    details: cleanReason);
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error("A draft could not be discarded.", exception);
            return ServiceResult.Fail("The draft could not be discarded.");
        }
    }

    // ------------------------------------------------------------------ final post

    /// <summary>
    /// Posts an open sale draft through the counter's own `Create`: every guard is re-checked at
    /// post time. Pass the cart's current input when the opener edited it; otherwise the stored
    /// payload posts. A failure restores the draft with the latest payload.
    /// </summary>
    public ServiceResult<SaleResult> PostSale(User? user, int draftId, SaleInput? current = null)
    {
        if (!_permissions.Can(user, Modules.Pos))
        {
            return ServiceResult<SaleResult>.Fail("You do not have permission to post sale drafts.");
        }

        SaleInput input;
        if (current is not null)
        {
            if (current.Lines.Count == 0)
            {
                return ServiceResult<SaleResult>.Fail("There is nothing to post - the cart is empty.");
            }
            input = current;
        }
        else
        {
            var stored = OpenSale(user, draftId);
            if (!stored.Ok || stored.Value is null)
            {
                return ServiceResult<SaleResult>.Fail(stored.Error ?? "The draft could not be opened.");
            }
            input = stored.Value;
        }

        return Post(draftId, DraftType.Sale, input,
            payload => _sales.Create(payload, user!),
            (connection, id) => Finalize(connection, user, draftId, DraftType.Sale, id));
    }

    /// <summary>
    /// Posts an open purchase draft through the purchase page's own `Create`. Same terminal rules
    /// as <see cref="PostSale"/>.
    /// </summary>
    public ServiceResult<PurchaseResult> PostPurchase(User? user, int draftId, PurchaseInput? current = null)
    {
        if (!_permissions.Can(user, Modules.Purchases))
        {
            return ServiceResult<PurchaseResult>.Fail("You do not have permission to post purchase drafts.");
        }

        PurchaseInput input;
        if (current is not null)
        {
            if (current.Lines.Count == 0)
            {
                return ServiceResult<PurchaseResult>.Fail("There is nothing to post - add at least one line.");
            }
            input = current;
        }
        else
        {
            var stored = OpenPurchase(user, draftId);
            if (!stored.Ok || stored.Value is null)
            {
                return ServiceResult<PurchaseResult>.Fail(stored.Error ?? "The draft could not be opened.");
            }
            input = stored.Value;
        }

        return Post(draftId, DraftType.Purchase, input,
            payload => _purchases.Create(payload, user!),
            (connection, id) => Finalize(connection, user, draftId, DraftType.Purchase, id));
    }

    private ServiceResult<TResult> Post<TInput, TResult>(
        int draftId, string type, TInput input,
        Func<TInput, ServiceResult<TResult>> create,
        Func<Microsoft.Data.Sqlite.SqliteConnection, int, ServiceResult> finalize)
        where TResult : class
    {
        string payload;
        try
        {
            payload = JsonSerializer.Serialize(input, Json.WriterOptions);
        }
        catch (Exception exception)
        {
            _log.Error("A draft could not be packed for posting.", exception);
            return ServiceResult<TResult>.Fail("This draft could not be posted.");
        }

        if (payload.Length > MaxPayloadChars)
        {
            return ServiceResult<TResult>.Fail("This draft is too large to post.");
        }

        try
        {
            // Pre-mark first: from this committed moment the draft is terminal, so no second post -
            // by another person or a retry after a crash - can create a second document.
            _db.Tx(connection =>
            {
                var row = ReadRow(connection, draftId);
                if (row is null || row.DraftType != type)
                {
                    throw new ValidationException("That draft is not in the list any more.");
                }
                if (row.Status != DraftStatus.Draft)
                {
                    throw new ValidationException("Only an open draft can be posted.");
                }

                _db.Execute(connection, @"
UPDATE document_drafts
SET payload_json = @payload, status = 'posted',
    posted_document_id = @marker, posted_at = @at, updated_at = @at
WHERE id = @id;",
                    new { payload, marker = PostingMarkerId, at = Clock.Now(), id = draftId });
            });
        }
        catch (ValidationException exception)
        {
            return ServiceResult<TResult>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("A draft could not be prepared for posting.", exception);
            return ServiceResult<TResult>.Fail("The draft could not be posted.");
        }

        ServiceResult<TResult> created;
        try
        {
            created = create(input);
        }
        catch (Exception exception)
        {
            RestoreAfterFailure(draftId, type);
            _log.Error("A draft could not be posted.", exception);
            return ServiceResult<TResult>.Fail("The draft could not be posted. It is kept as a draft.");
        }

        if (!created.Ok || created.Value is null)
        {
            RestoreAfterFailure(draftId, type);
            return ServiceResult<TResult>.Fail((created.Error ?? "The draft could not be posted.") + " The draft is kept.");
        }

        try
        {
            var documentId = created.Value switch
            {
                SaleResult sale => sale.SaleId,
                PurchaseResult purchase => purchase.PurchaseId,
                _ => 0
            };
            _db.Tx(connection => finalize(connection, documentId));
        }
        catch (Exception exception)
        {
            // The document EXISTS (its transaction committed); only the final mark failed. The
            // draft stays flagged for a post check and can never post again - see the class note.
            _log.Error("A posted draft could not be marked final.", exception);
        }

        return created;
    }

    private void RestoreAfterFailure(int draftId, string type)
    {
        try
        {
            _db.Tx(connection =>
            {
                _db.Execute(connection, @"
UPDATE document_drafts
SET status = 'draft', posted_document_id = NULL, posted_at = NULL, updated_at = @at
WHERE id = @id AND status = 'posted' AND posted_document_id = @marker;",
                    new { at = Clock.Now(), id = draftId, marker = PostingMarkerId });
            });
        }
        catch (Exception exception)
        {
            _log.Error("A failed draft post could not restore the draft row.", exception);
        }
    }

    private ServiceResult Finalize(
        Microsoft.Data.Sqlite.SqliteConnection connection, User? user, int draftId, string type, int documentId)
    {
        _db.Execute(connection, @"
UPDATE document_drafts SET posted_document_id = @document, updated_at = @at WHERE id = @id;",
            new { document = documentId, at = Clock.Now(), id = draftId });
        _audit.Record(connection, user, DraftType.ModuleFor(type), "draft_posted",
            "document_drafts", draftId.ToString(), newValue: documentId.ToString());
        return ServiceResult.Success();
    }

    // ------------------------------------------------------------------ readers

    private DocumentDraft? ReadRow(Microsoft.Data.Sqlite.SqliteConnection connection, int draftId) =>
        _db.QuerySingle<DocumentDraft>(connection, @"
SELECT id, draft_key AS draftKey, draft_type AS draftType, payload_json AS payloadJson,
       status, created_by_id AS createdById, created_by_name AS createdByName,
       created_at AS createdAt, updated_at AS updatedAt,
       posted_document_id AS postedDocumentId, posted_at AS postedAt, discard_reason AS discardReason
FROM document_drafts WHERE id = @id;",
            new { id = draftId });

    private static (int Lines, int PartnerId) DescribePayload(string payloadJson)
    {
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;
            var lines = root.TryGetProperty("Lines", out var linesElement)
                && linesElement.ValueKind == JsonValueKind.Array
                ? linesElement.GetArrayLength()
                : 0;
            var partner = 0;
            if (root.TryGetProperty("CustomerId", out var customer)
                && customer.ValueKind == JsonValueKind.Number
                && customer.TryGetInt32(out var customerId))
            {
                partner = customerId;
            }
            else if (root.TryGetProperty("SupplierId", out var supplier)
                && supplier.ValueKind == JsonValueKind.Number
                && supplier.TryGetInt32(out var supplierId))
            {
                partner = supplierId;
            }
            return (lines, partner);
        }
        catch (JsonException)
        {
            return (0, 0);
        }
    }

    private string PartnerName(Microsoft.Data.Sqlite.SqliteConnection connection, string type, int partnerId)
    {
        if (partnerId <= 0)
        {
            return string.Empty;
        }

        try
        {
            var table = type == DraftType.Purchase ? "suppliers" : "customers";
            // The table name comes from this file's own list, never from the caller.
            return _db.QuerySingle<string>(connection,
                "SELECT name FROM " + table + " WHERE id = @id;", new { id = partnerId })
                ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static bool IsUniqueViolation(Exception exception) =>
        exception.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
}
