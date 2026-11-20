using Area11.Core.Data;
using Area11.Core.Models;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>Bounded read/projection policy plus Desktop-enabled proof;a supplied ID alone is not authentication.</summary>
internal static class ProductReadAccess
{
    public static User Require(Database db, SqliteConnection connection, PermissionService permissions,
        User? actor, ProductReadPurpose purpose)
    {
        db.Sessions.Require(actor);
        var module = purpose switch
        {
            ProductReadPurpose.Catalog => Modules.Products,
            ProductReadPurpose.Counter => Modules.Pos,
            ProductReadPurpose.Purchase => Modules.Purchases,
            ProductReadPurpose.SupplierReturn => Modules.Suppliers,
            ProductReadPurpose.WriteOff => Modules.Stock,
            ProductReadPurpose.Expense => Modules.Cash,
            ProductReadPurpose.ProvisionalReturn => Modules.Returns,
            _ => throw new ValidationException("Choose a supported product reading workflow.")
        };
        if (actor is not { Active: true } || actor.Id <= 0)
            throw new ValidationException("Sign in with an active account to read products.");
        var current = db.QuerySingle<User>(connection,
            "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = actor.Id });
        if (current is not { Active: true } || current.Kind != actor.Kind)
            throw new ValidationException("The account has changed. Sign in again before reading products.");
        db.Sessions.Carry(actor, current);
        if (!permissions.Can(current, module))
            throw new ValidationException("You do not have permission for this product reading workflow.");
        return current;
    }

    public static void RequirePreparationCost(User reader, ProductReadPurpose purpose,
        PermissionService permissions, SettingsService settings)
    {
        if (purpose is ProductReadPurpose.Purchase or ProductReadPurpose.SupplierReturn
            or ProductReadPurpose.WriteOff or ProductReadPurpose.Expense)
            if (!CostPolicy.CanSee(permissions, settings, reader))
                throw new ValidationException("View Purchase Cost permission and the main financial visibility switch are required for cost preparation.");
    }

    public static void Project(Product product, ProductReadPurpose purpose, bool canSeeCost, bool canSeeProfit)
    {
        var financialRow = purpose is not (ProductReadPurpose.Counter or ProductReadPurpose.ProvisionalReturn);
        product.CostKnown = financialRow && canSeeCost;
        product.MarginKnown = financialRow && canSeeProfit;
        // Calculate before masking cost: profit-only readers must not get a fake 100% margin.
        product.MarginPercent = product.MarginKnown && product.RetailPaisa > 0
            ? (product.RetailPaisa - product.CostPaisa) * 100m / product.RetailPaisa : 0;
        if (!product.CostKnown) product.CostPaisa = 0;
    }
}
