using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using LogisticsApp.Models;

namespace LogisticsApp.Services;

public class AuditLogChannel
{
    public Channel<AuditLog> Channel { get; }

    public AuditLogChannel()
    {
        var options = new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        Channel = System.Threading.Channels.Channel.CreateBounded<AuditLog>(options);
    }

    public async ValueTask AddLogAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        await Channel.Writer.WriteAsync(log, cancellationToken).ConfigureAwait(false);
    }
}