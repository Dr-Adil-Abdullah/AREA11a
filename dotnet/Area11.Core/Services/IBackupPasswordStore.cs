namespace Area11.Core.Services;

/// <summary>Host credential storage, separate from DB/settings/export/backup/audit. Never log values.</summary>
public interface IBackupPasswordStore
{
    string? Load();
    void Save(string password);
}
