using System.Reflection;

namespace Junaid.GoogleGemini.Net.Infrastructure;

/// <summary>
/// The version of this package, read from the assembly. The telemetry source and the request headers
/// both use it. Do not copy the version into a constant. <c>GeminiTelemetry</c> did that, and its
/// copy still said 6.4.1 seven releases later because no release step touched it.
/// </summary>
internal static class GeminiVersion
{
    /// <summary>
    /// The package version, for example <c>6.8.1</c> or <c>6.9.0-beta.1</c>. Any build metadata that
    /// follows a <c>+</c> (for example a source commit hash) is removed.
    /// </summary>
    internal static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var assembly = typeof(GeminiVersion).Assembly;

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational!.IndexOf('+');
            return plus >= 0 ? informational.Substring(0, plus) : informational;
        }

        var version = assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
