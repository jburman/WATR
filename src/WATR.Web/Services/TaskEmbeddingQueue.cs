using System.Threading.Channels;

namespace WATR.Web.Services;

public class TaskEmbeddingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid taskId) => _channel.Writer.TryWrite(taskId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
