using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>Current parent-tab visibility, not an authorization token or financial projection.</summary>
public sealed class PartnerAccessService
{
    private readonly Database _db;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public PartnerAccessService(Database db, PermissionService permissions, LogService log)
    {
        _db = db;
        _permissions = permissions;
        _log = log;
    }

    public ServiceResult<PartnerAccess> Read(User? user)
    {
        try
        {
            using var connection = _db.Open();
            var reader = SnapshotReadAccess.Require(_db, connection, _permissions, user,
                Modules.Customers, Modules.Suppliers);
            return ServiceResult<PartnerAccess>.Success(new PartnerAccess
            {
                Customers = _permissions.Can(reader, Modules.Customers),
                Suppliers = _permissions.Can(reader, Modules.Suppliers)
            });
        }
        catch (ValidationException exception) { return ServiceResult<PartnerAccess>.Fail(exception.Message); }
        catch (Exception exception)
        {
            _log.Error("Partner tab access could not be read.", exception);
            return ServiceResult<PartnerAccess>.Fail("Partner tab access could not be confirmed. Refresh or sign in again.");
        }
    }
}
