using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The shop's own expense categories (owner's word, Nov 2026): the eight historical words plus
/// whatever the owner adds, each under one of the two main types (running cost / stock goods).
/// Managing the list is owner-only (master-data precedent); recording against it needs only the
/// cash permission. Put-away hides a word from the picker but never rewrites old expenses, and
/// 'other' can be neither renamed nor put away - the fallback must always be there.
/// </summary>
public sealed class ExpenseCategoryService
{
    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public ExpenseCategoryService(Database db, AuditService audit, PermissionService permissions, LogService log)
    {
        _db = db;
        _audit = audit;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>Running costs first, then stock goods, each alphabetical. Hidden ones only if asked.</summary>
    public List<ExpenseCategory> List(bool includeInactive = false)
    {
        try
        {
            using var connection = _db.Open();

            return _db.Query<ExpenseCategory>(connection,
                @"SELECT id AS Id, name AS Name, type AS Type, active AS Active, created_at AS CreatedAt
FROM expense_categories" + (includeInactive ? string.Empty : " WHERE active = 1") + @"
ORDER BY CASE type WHEN 'running' THEN 0 ELSE 1 END, name COLLATE NOCASE;").ToList();
        }
        catch (Exception exception)
        {
            _log.Error("The expense categories could not be listed.", exception);
            return new List<ExpenseCategory>();
        }
    }

    /// <summary>The stored word an expense may use, or null when it is unknown or put away.</summary>
    public ExpenseCategory? FindActive(string? name)
    {
        var clean = (name ?? string.Empty).Trim();

        if (clean.Length == 0)
        {
            return null;
        }

        try
        {
            using var connection = _db.Open();

            return _db.Query<ExpenseCategory>(connection,
                @"SELECT id AS Id, name AS Name, type AS Type, active AS Active, created_at AS CreatedAt
FROM expense_categories WHERE active = 1 AND name = @name LIMIT 1;",
                new { name = clean }).FirstOrDefault();
        }
        catch (Exception exception)
        {
            _log.Error("The expense category could not be read.", exception);
            return null;
        }
    }

    /// <summary>The historical words print pretty; the owner's own words print as typed.</summary>
    public static string Label(string? category)
    {
        var clean = (category ?? string.Empty).Trim();

        if (clean.Length == 0)
        {
            return "Other";
        }

        return Array.IndexOf(ExpenseCategories.All, clean.ToLowerInvariant()) >= 0
            ? ExpenseCategories.Label(clean)
            : clean;
    }

    public ServiceResult<ExpenseCategory> Add(User? user, string? name, string? type)
    {
        if (user?.IsOwner != true)
        {
            return ServiceResult<ExpenseCategory>.Fail("Only the owner can add an expense category.");
        }

        var clean = (name ?? string.Empty).Trim();
        var kind = (type ?? string.Empty).Trim().ToLowerInvariant();

        if (clean.Length == 0 || clean.Length > 40)
        {
            return ServiceResult<ExpenseCategory>.Fail("The category name must be 1 to 40 letters.");
        }

        if (kind != ExpenseCategoryType.Running && kind != ExpenseCategoryType.Stock)
        {
            return ServiceResult<ExpenseCategory>.Fail("The category must be a running cost or stock goods.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var taken = Convert.ToInt64(_db.Scalar(connection,
                    "SELECT COUNT(1) FROM expense_categories WHERE name = @name;", new { name = clean }) ?? 0L);

                if (taken > 0)
                {
                    return ServiceResult<ExpenseCategory>.Fail("That category is already on the list.");
                }

                _db.Execute(connection,
                    "INSERT INTO expense_categories(name, type, active) VALUES(@name, @type, 1);",
                    new { name = clean, type = kind });

                var id = (int)_db.NextId(connection);
                var added = new ExpenseCategory { Id = id, Name = clean, Type = kind, Active = true };

                _audit.Record(connection, user, "Cash", "expense_category_add", "expense_categories",
                    id.ToString(), null, clean + " (" + ExpenseCategoryType.Label(kind) + ")");

                return ServiceResult<ExpenseCategory>.Success(added);
            });
        }
        catch (Exception exception)
        {
            _log.Error("The expense category could not be added.", exception);
            return ServiceResult<ExpenseCategory>.Fail("The expense category could not be added.");
        }
    }

    /// <summary>Renames the list word only; old expenses keep the spelling they were saved with.</summary>
    public ServiceResult Rename(User? user, int id, string? name)
    {
        if (user?.IsOwner != true)
        {
            return ServiceResult.Fail("Only the owner can rename an expense category.");
        }

        var clean = (name ?? string.Empty).Trim();

        if (clean.Length == 0 || clean.Length > 40)
        {
            return ServiceResult.Fail("The category name must be 1 to 40 letters.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var current = _db.Query<ExpenseCategory>(connection,
                    "SELECT id AS Id, name AS Name, type AS Type, active AS Active FROM expense_categories WHERE id = @id;",
                    new { id }).FirstOrDefault();

                if (current is null)
                {
                    return ServiceResult.Fail("That category is no longer on the list.");
                }

                if (string.Equals(current.Name, "other", StringComparison.OrdinalIgnoreCase))
                {
                    return ServiceResult.Fail("The 'Other' fallback cannot be renamed.");
                }

                var taken = Convert.ToInt64(_db.Scalar(connection,
                    "SELECT COUNT(1) FROM expense_categories WHERE name = @name AND id <> @id;",
                    new { name = clean, id }) ?? 0L);

                if (taken > 0)
                {
                    return ServiceResult.Fail("That category is already on the list.");
                }

                _db.Execute(connection,
                    "UPDATE expense_categories SET name = @name WHERE id = @id;",
                    new { name = clean, id });

                _audit.Record(connection, user, "Cash", "expense_category_rename", "expense_categories",
                    id.ToString(), current.Name, clean);

                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error("The expense category could not be renamed.", exception);
            return ServiceResult.Fail("The expense category could not be renamed.");
        }
    }

    public ServiceResult SetActive(User? user, int id, bool active)
    {
        if (user?.IsOwner != true)
        {
            return ServiceResult.Fail("Only the owner can change an expense category.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var current = _db.Query<ExpenseCategory>(connection,
                    "SELECT id AS Id, name AS Name, type AS Type, active AS Active FROM expense_categories WHERE id = @id;",
                    new { id }).FirstOrDefault();

                if (current is null)
                {
                    return ServiceResult.Fail("That category is no longer on the list.");
                }

                if (!active && string.Equals(current.Name, "other", StringComparison.OrdinalIgnoreCase))
                {
                    return ServiceResult.Fail("The 'Other' fallback cannot be put away.");
                }

                _db.Execute(connection,
                    "UPDATE expense_categories SET active = @active WHERE id = @id;",
                    new { active = active ? 1 : 0, id });

                _audit.Record(connection, user, "Cash", active ? "expense_category_restore" : "expense_category_hide",
                    "expense_categories", id.ToString(), current.Active ? "shown" : "hidden",
                    active ? "shown" : "hidden");

                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error("The expense category could not be changed.", exception);
            return ServiceResult.Fail("The expense category could not be changed.");
        }
    }
}
