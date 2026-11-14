using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>Dedicated owner-only pricing permissions. No manager or role default grants edits.</summary>
public sealed class PricingAllowanceService
{
    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly LogService _log;

    public PricingAllowanceService(Database db, AuditService audit, LogService log)
    {
        _db = db;
        _audit = audit;
        _log = log;
    }

    public PricingAllowance ForUser(User user)
    {
        using var connection = _db.Open();
        return Read(connection, user);
    }

    public PricingAllowance Read(SqliteConnection connection, User user)
    {
        // The caller's role snapshot cannot turn an arbitrary id into the owner.
        var person = _db.QuerySingle<User>(connection,
            "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = user.Id });
        if (person is not { Active: true }) throw new ValidationException("This staff account is not active.");
        var saved = _db.QuerySingle<PricingAllowance>(connection, @"
SELECT user_id, edit_rate, manual_reduction_percent, profit_discount, profit_percent,
       retail_discount, retail_percent, override_basis, extra_discount, extra_limit_paisa
FROM user_pricing_allowances WHERE user_id = @id;", new { id = person.Id });
        return saved ?? new PricingAllowance
        {
            UserId = person.Id,
            EditRate = person.IsOwner,
            ManualReductionPercent = person.IsOwner ? 100 : 0,
            ProfitDiscount = person.IsOwner,
            ProfitPercent = person.IsOwner ? 100 : 0,
            RetailDiscount = person.IsOwner,
            RetailPercent = person.IsOwner ? 100 : 0,
            OverrideBasis = person.IsOwner
        };
    }

    public ServiceResult Save(User actor, PricingAllowance value)
    {
        try
        {
            _db.Tx(connection =>
            {
                var owner = _db.QuerySingle<User>(connection,
                    "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = actor.Id });
                if (!actor.IsOwner || owner is not { IsOwner: true, Active: true })
                    throw new ValidationException("Only the owner can change pricing permissions.");
                var target = _db.QuerySingle<User>(connection,
                    "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = value.UserId });
                if (target is null) throw new ValidationException("Choose an existing person.");
                if (value.ManualReductionPercent is < 0 or > 100 || value.ProfitPercent is < 0 or > 100
                    || value.RetailPercent is < 0 or > 100)
                    throw new ValidationException("Every pricing limit must be between 0 and 100 percent.");
                if (value.ExtraLimitPaisa < 0)
                    throw new ValidationException("Extra Discount limit cannot be negative.");
                var before = Read(connection, target);
                _db.Execute(connection, @"
INSERT INTO user_pricing_allowances(user_id, edit_rate, manual_reduction_percent,
 profit_discount, profit_percent, retail_discount, retail_percent, override_basis, extra_discount, extra_limit_paisa)
VALUES(@userId, @editRate, @manual, @profitDiscount, @profit, @retailDiscount, @retail, @overrideBasis, @extra, @extraLimit)
ON CONFLICT(user_id) DO UPDATE SET edit_rate=excluded.edit_rate,
 manual_reduction_percent=excluded.manual_reduction_percent,
 profit_discount=excluded.profit_discount, profit_percent=excluded.profit_percent,
 retail_discount=excluded.retail_discount, retail_percent=excluded.retail_percent,
 override_basis=excluded.override_basis, extra_discount=excluded.extra_discount,
 extra_limit_paisa=excluded.extra_limit_paisa;", new
                {
                    userId = value.UserId, editRate = value.EditRate ? 1 : 0,
                    manual = (double)value.ManualReductionPercent,
                    profitDiscount = value.ProfitDiscount ? 1 : 0, profit = (double)value.ProfitPercent,
                    retailDiscount = value.RetailDiscount ? 1 : 0, retail = (double)value.RetailPercent,
                    overrideBasis = value.OverrideBasis ? 1 : 0,
                    extra = value.ExtraDiscount ? 1 : 0, extraLimit = value.ExtraLimitPaisa
                });
                _audit.Record(connection, actor, "Users", "pricing_permissions", "user_pricing_allowances",
                    value.UserId.ToString(), oldValue: JsonSerializer.Serialize(before), newValue: JsonSerializer.Serialize(value));
            });
            return ServiceResult.Success();
        }
        catch (ValidationException exception) { return ServiceResult.Fail(exception.Message); }
        catch (Exception exception)
        {
            _log.Error("Pricing permissions could not be saved.", exception);
            return ServiceResult.Fail("Pricing permissions could not be saved.");
        }
    }

    public static string Basis(string? value) => value switch
    {
        "profit" => "profit",
        "retail" => "retail",
        _ => throw new ValidationException("Choose profit or selling total as the discount basis.")
    };

    public static void CheckDiscount(PricingAllowance allowance, string basis, decimal percent)
    {
        Basis(basis);
        var limit = basis == "profit"
            ? (allowance.ProfitDiscount ? allowance.ProfitPercent : 0)
            : (allowance.RetailDiscount ? allowance.RetailPercent : 0);
        if (percent < 0 || percent > limit)
            throw new ValidationException($"Your {basis} discount limit is {limit:0.##}%. The requested discount is not allowed.");
    }
}
