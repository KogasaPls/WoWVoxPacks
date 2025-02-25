using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace BigWigsVoiceGCP;

public static class Utils
{
    public static async IAsyncEnumerable<T> ConsumeBuffered<T>(
        this IAsyncEnumerable<T> source, int capacity,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        Channel<T> channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            SingleWriter = true, SingleReader = true
        });
        using CancellationTokenSource completionCts = new();

        Task producer = Task.Run(async () =>
        {
            try
            {
                await foreach (T item in source.WithCancellation(completionCts.Token)
                                   .ConfigureAwait(false))
                {
                    await channel.Writer.WriteAsync(item, completionCts.Token).ConfigureAwait(false);
                }
            }
            catch (ChannelClosedException) { } // Ignore
            finally { channel.Writer.TryComplete(); }
        }, completionCts.Token);

        try
        {
            await foreach (T item in channel.Reader.ReadAllAsync(cancellationToken)
                               .ConfigureAwait(false))
            {
                yield return item;
                cancellationToken.ThrowIfCancellationRequested();
            }

            await producer.ConfigureAwait(false); // Propagate possible source error
        }
        finally
        {
            // Prevent fire-and-forget in case the enumeration is abandoned
            if (!producer.IsCompleted)
            {
                await completionCts.CancelAsync();
                channel.Writer.TryComplete();
                await Task.WhenAny(producer).ConfigureAwait(false);
            }
        }
    }
}
