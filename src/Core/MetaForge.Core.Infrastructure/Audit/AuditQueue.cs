using System.Threading.Channels;
using MetaForge.Application.DTOs;
using MetaForge.Application.Interfaces;

namespace MetaForge.Infrastructure.Audit;

public sealed class AuditQueue : IAuditQueue
{
    private readonly Channel<AuditLogEntry> _channel = Channel.CreateUnbounded<AuditLogEntry>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(AuditLogEntry entry, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(entry, cancellationToken);

    public IAsyncEnumerable<AuditLogEntry> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
