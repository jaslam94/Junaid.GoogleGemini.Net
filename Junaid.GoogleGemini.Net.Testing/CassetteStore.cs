using System.Text.Json;

namespace Junaid.GoogleGemini.Net.Testing;

/// <summary>Loads and saves cassette files as indented, git-diffable JSON.</summary>
internal static class CassetteStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static async Task<CassetteFile> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new CassetteFile();
        }

        await using var stream = File.OpenRead(path);
        var cassette = await JsonSerializer.DeserializeAsync<CassetteFile>(stream, Options, cancellationToken)
            .ConfigureAwait(false);
        return cassette ?? new CassetteFile();
    }

    public static async Task SaveAsync(string path, CassetteFile cassette, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to a temp file then move, so a crash mid-write can't corrupt interactions that an
        // earlier request in this same run already recorded.
        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, cassette, Options, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, path, overwrite: true);
    }
}
