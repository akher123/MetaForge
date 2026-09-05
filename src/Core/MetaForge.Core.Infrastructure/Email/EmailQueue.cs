using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace MetaForge.Infrastructure.Email;

public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(int emailMessageId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(emailMessageId, cancellationToken);

    public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
