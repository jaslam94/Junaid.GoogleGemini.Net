using Junaid.GoogleGemini.Net.Exceptions;

namespace Junaid.GoogleGemini.Net.Testing;

/// <summary>
/// Thrown in <see cref="CassetteMode.Replay"/> when a request has no matching recorded
/// interaction in the cassette.
/// </summary>
/// <remarks>
/// Derives from <see cref="GeminiException"/> (rather than plain <see cref="Exception"/>) so
/// <c>GeminiClient</c>'s error handling — which already special-cases and rethrows
/// <see cref="GeminiException"/> as-is — surfaces this unwrapped instead of burying it as an
/// <c>InnerException</c> on a generic failure.
/// </remarks>
public sealed class CassetteException : GeminiException
{
    public CassetteException(string message) : base(message)
    {
    }
}
