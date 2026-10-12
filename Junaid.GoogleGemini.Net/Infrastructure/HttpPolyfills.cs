using System.IO;

namespace Junaid.GoogleGemini.Net.Infrastructure;

/// <summary>
/// Small shims for cancellation-aware APIs that exist on net8+ but not on netstandard2.0. On the
/// older surface the token is honored where the BCL allows and otherwise ignored, so call sites can
/// stay single-source across all target frameworks.
/// </summary>
internal static class HttpPolyfills
{
    public static Task<string> ReadStringAsync(this HttpContent content, CancellationToken cancellationToken)
#if NET8_0_OR_GREATER
        => content.ReadAsStringAsync(cancellationToken);
#else
        => content.ReadAsStringAsync();
#endif

    public static Task<Stream> ReadStreamAsync(this HttpContent content, CancellationToken cancellationToken)
#if NET8_0_OR_GREATER
        => content.ReadAsStreamAsync(cancellationToken);
#else
        => content.ReadAsStreamAsync();
#endif

    public static Task<byte[]> ReadBytesAsync(this HttpContent content, CancellationToken cancellationToken)
#if NET8_0_OR_GREATER
        => content.ReadAsByteArrayAsync(cancellationToken);
#else
        => content.ReadAsByteArrayAsync();
#endif

#if NET8_0_OR_GREATER
    public static Task<string?> ReadLineCancelableAsync(this StreamReader reader, CancellationToken cancellationToken)
        => reader.ReadLineAsync(cancellationToken).AsTask();
#else
    /// <summary>
    /// <c>StreamReader.ReadLineAsync</c> has no token overload on netstandard2.0, so this honors the token
    /// itself. Before 6.8.0 the token was ignored here. A caller who cancelled a stream on .NET Framework,
    /// Mono or Unity got no cancellation: the stream ran to its end, and the cost governor recorded spend
    /// for a stream the caller had abandoned. The test suite first ran on .NET Framework in 6.8.0 and
    /// found this.
    ///
    /// It throws if the token is already cancelled, stops waiting as soon as the token is cancelled, and
    /// checks the token once more when a line has arrived. A read that is still pending when the caller
    /// stops waiting keeps running until its stream is disposed, which the streaming loops do when they exit.
    /// </summary>
    public static async Task<string?> ReadLineCancelableAsync(this StreamReader reader, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var read = reader.ReadLineAsync();
        if (!read.IsCompleted && cancellationToken.CanBeCanceled)
        {
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled))
            {
                if (await Task.WhenAny(read, cancelled.Task).ConfigureAwait(false) != read)
                {
                    // Observe the abandoned read's outcome, so a failure after disposal is not left unobserved.
                    _ = read.ContinueWith(
                        static t => _ = t.Exception,
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    throw new OperationCanceledException(cancellationToken);
                }
            }
        }

        var line = await read.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return line;
    }
#endif
}
