using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Area11.Core.Data;
using Area11.Core.Models;

namespace Area11.Core.Services;

/// <summary>Database-instance-bound, non-serializable authentication proof. Desktop enables this
/// once at composition; legacy isolated service tests may run without this boundary. Not a remote
/// API token, protection from hostile in-process code/raw SQL, or a durable posting identity.</summary>
public sealed class AuthenticatedSessionService
{
    private readonly Database _db;
    private readonly ConditionalWeakTable<User, Lease> _proofs = new();
    private int _enabled;
    internal AuthenticatedSessionService(Database db) => _db = db;
    public bool Enabled => Volatile.Read(ref _enabled) != 0;
    public void Enable() => Interlocked.Exchange(ref _enabled, 1); // Deliberately no disable/reset API.

    private sealed class Lease
    {
        public required int Id { get; init; }
        public required UserRole Role { get; init; }
        public required string CredentialStamp { get; init; }
        public required bool CloseOnly { get; init; }
        public long Started { get; } = Stopwatch.GetTimestamp();
        public int Revoked;
    }
    private sealed class Credentials
    {
        public int Id { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool Active { get; set; }
        public string? PasswordHash { get; set; }
        public string? PinHash { get; set; }
    }
    private Credentials? Read(int id) => _db.Read(c => _db.QuerySingle<Credentials>(c,
        "SELECT id, role, active, password_hash, pin_hash FROM users WHERE id = @id;", new { id }));
    private static string Stamp(string? password, string? pin) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes((password?.Length ?? 0) + ":" + password + ":" + pin)));

    // Only AuthService,after secret verification,may mint a proof. Compare the exact verified
    // hashes again: a reset between Verify and Issue must not bind the old password to new hashes.
    internal void Issue(User user, string? passwordHash, string? pinHash, bool closeOnly)
    {
        if (!Enabled) return;
        var row = Read(user.Id);
        if (row is not { Active: true } || UserRoles.Parse(row.Role) != user.Kind
            || !string.Equals(Stamp(row.PasswordHash, row.PinHash), Stamp(passwordHash, pinHash), StringComparison.Ordinal))
            throw new ValidationException("The account changed during verification. Sign in again.");
        _proofs.Add(user, new Lease { Id = user.Id, Role = user.Kind,
            CredentialStamp = Stamp(passwordHash, pinHash), CloseOnly = closeOnly });
    }

    internal void IssueRecoveredOwner(User user)
    {
        if (!Enabled) return;
        var row = Read(user.Id) ?? throw new ValidationException("The owner account no longer exists.");
        Issue(user, row.PasswordHash, row.PinHash, closeOnly: false);
    }

    private Lease Proof(User? actor, bool closeOnlyAllowed)
    {
        if (actor is not { Active: true } || !_proofs.TryGetValue(actor, out var lease)
            || Volatile.Read(ref lease.Revoked) != 0 || actor.Id != lease.Id || actor.Kind != lease.Role
            || (lease.CloseOnly && !closeOnlyAllowed)
            || Stopwatch.GetElapsedTime(lease.Started) >= (lease.CloseOnly ? TimeSpan.FromMinutes(2) : TimeSpan.FromHours(12)))
            throw new ValidationException("The authenticated session is no longer valid. Sign in again.");
        return lease;
    }

    public void Require(User? actor, bool closeOnlyAllowed = false)
    {
        if (!Enabled) return;
        var lease = Proof(actor, closeOnlyAllowed);
        var row = Read(lease.Id);
        if (row is not { Active: true } || UserRoles.Parse(row.Role) != lease.Role
            || !string.Equals(lease.CredentialStamp, Stamp(row.PasswordHash, row.PinHash), StringComparison.Ordinal))
        {
            Interlocked.Exchange(ref lease.Revoked, 1);
            throw new ValidationException("The account or password/PIN changed. Sign in again.");
        }
    }

    // Existing readers resolve a fresh User for current names/roles. Carry the SAME lease,never
    // mint from a database ID;revocation invalidates every carried reader together.
    internal void Carry(User? source, User current)
    {
        if (!Enabled) return;
        var lease = Proof(source, closeOnlyAllowed: true);
        if (!current.Active || current.Id != lease.Id || current.Kind != lease.Role)
            throw new ValidationException("The account has changed. Sign in again.");
        if (!ReferenceEquals(source, current)) _proofs.Add(current, lease);
    }

    public void Revoke(User? user)
    {
        if (user is not null && _proofs.TryGetValue(user, out var lease))
            Interlocked.Exchange(ref lease.Revoked, 1);
    }
}
