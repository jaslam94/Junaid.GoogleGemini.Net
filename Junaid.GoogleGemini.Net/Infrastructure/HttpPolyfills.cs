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

    public static Task<string?> ReadLineCancelableAsync(this StreamReader reader, CancellationToken cancellationToken)
#if NET8_0_OR_GREATER
        => reader.ReadLineAsync(cancellationToken).AsTask();
#else
        => reader.ReadLineAsync();
#endif
}
