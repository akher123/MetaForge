using MetaForge.Application.DTOs;
using MetaForge.Application.Interfaces;
using MetaForge.Domain.Audit;

namespace MetaForge.Infrastructure.Audit;

/// <summary>
/// Entity Framework implementation of audit log persistence.
/// </summary>
public sealed class EfAuditLogStore : IAuditLogStore
{
    private readonly MetaForgeDbContext _dbContext;

    public EfAuditLogStore(MetaForgeDbContext dbContext) => _dbContext = dbContext;

    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            EntityName = entry.EntityName,
            RecordId = entry.RecordId,
            Action = entry.Action,
            UserName = entry.UserName,
            Timestamp = entry.TimestampUtc,
            OldValue = entry.OldValue,
            NewValue = entry.NewValue
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
