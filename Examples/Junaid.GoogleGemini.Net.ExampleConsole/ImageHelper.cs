namespace Junaid.GoogleGemini.Net.ExampleConsole;

/// <summary>
/// Helper class for image-related operations in examples
/// </summary>
public static class ImageHelper
{
    private const string SampleImagesDir = "sample-images";
    private const string TestImageFileName = "test-image.png";
    
    private static readonly string[] ImageExtensions = { "*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp", "*.webp" };
    
    // Minimal 1x1 PNG image in base64 for testing
    private const string SimplePngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    /// <summary>
    /// Creates or finds a sample image for vision examples
    /// </summary>
    /// <returns>Path to the image file, or null if creation failed</returns>
    public static async Task<string?> CreateOrFindSampleImageAsync()
    {
        var sampleImagesPath = Path.Combine(Directory.GetCurrentDirectory(), SampleImagesDir);

        EnsureDirectoryExists(sampleImagesPath);

        // Try to find existing images first
        var existingImage = FindExistingImage(sampleImagesPath);
        if (existingImage != null)
            return existingImage;

        // Create a simple test image if none found
        return await CreateSimpleTestImageAsync(sampleImagesPath);
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private static string? FindExistingImage(string directory)
    {
        foreach (var extension in ImageExtensions)
        {
            var files = Directory.GetFiles(directory, extension);
            if (files.Length > 0)
            {
                return files[0];
            }
        }
        return null;
    }

    private static async Task<string?> CreateSimpleTestImageAsync(string directory)
    {
        try
        {
            var testImagePath = Path.Combine(directory, TestImageFileName);
            var imageBytes = Convert.FromBase64String(SimplePngBase64);
            await File.WriteAllBytesAsync(testImagePath, imageBytes);
            return testImagePath;
        }
        catch
        {
            return null;
        }
    }
}