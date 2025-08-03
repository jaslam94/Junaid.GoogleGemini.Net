using System.Text;

namespace Junaid.GoogleGemini.Net.Infrastructure.Utilities
{
    /// <summary>
    /// Unified utility class containing file and media-related helper methods
    /// </summary>
    public static class FileUtilities
    {
        #region MIME Type Detection

        private static readonly Dictionary<string, string> MimeTypeMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            // Image formats
            { ".bmp", "image/bmp" },
            { ".gif", "image/gif" },
            { ".jpeg", "image/jpeg" },
            { ".jpg", "image/jpeg" },
            { ".png", "image/png" },
            { ".tiff", "image/tiff" },
            { ".tif", "image/tiff" },
            { ".webp", "image/webp" },
            { ".ico", "image/x-icon" },
            { ".svg", "image/svg+xml" },
            
            // Audio formats (for future multimodal support)
            { ".mp3", "audio/mpeg" },
            { ".wav", "audio/wav" },
            { ".flac", "audio/flac" },
            { ".aac", "audio/aac" },
            
            // Video formats (for future multimodal support)
            { ".mp4", "video/mp4" },
            { ".avi", "video/x-msvideo" },
            { ".mov", "video/quicktime" },
            { ".webm", "video/webm" },
            
            // Document formats
            { ".pdf", "application/pdf" },
            { ".txt", "text/plain" },
            { ".json", "application/json" },
            { ".xml", "application/xml" }
        };

        /// <summary>
        /// Gets MIME type from file name extension
        /// </summary>
        /// <param name="fileName">File name with extension</param>
        /// <returns>MIME type string</returns>
        /// <exception cref="ArgumentNullException">Thrown when fileName is null</exception>
        public static string GetMimeType(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var extension = Path.GetExtension(fileName);

            if (extension != null && MimeTypeMappings.TryGetValue(extension.ToLowerInvariant(), out var mimeType))
            {
                return mimeType;
            }

            // Default to application/octet-stream if the mapping is not found
            return "application/octet-stream";
        }

        /// <summary>
        /// Checks if file extension represents a supported image format
        /// </summary>
        /// <param name="fileName">File name with extension</param>
        /// <returns>True if supported image format</returns>
        public static bool IsImageFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            var mimeType = GetMimeType(fileName);
            return mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Image Content Detection

        private static readonly byte[][] ImageHeaders = 
        {
            Encoding.ASCII.GetBytes("BM"),              // BMP
            Encoding.ASCII.GetBytes("GIF"),             // GIF  
            new byte[] { 137, 80, 78, 71 },             // PNG
            new byte[] { 73, 73, 42 },                  // TIFF (Intel byte order)
            new byte[] { 77, 77, 42 },                  // TIFF (Motorola byte order)
            new byte[] { 255, 216, 255, 224 },          // JPEG
            new byte[] { 255, 216, 255, 225 },          // JPEG CANON
            new byte[] { 255, 216, 255, 219 },          // JPEG
            new byte[] { 255, 216, 255, 238 },          // JPEG
            new byte[] { 0x52, 0x49, 0x46, 0x46 },      // WEBP (starts with "RIFF")
        };

        /// <summary>
        /// Determines if byte array contains image data by checking file headers
        /// </summary>
        /// <param name="fileBytes">File content as byte array</param>
        /// <returns>True if file appears to be an image</returns>
        public static bool IsImageContent(this byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length == 0)
                return false;

            return ImageHeaders.Any(header => 
                fileBytes.Length >= header.Length && 
                header.SequenceEqual(fileBytes.Take(header.Length)));
        }

        /// <summary>
        /// Validates image file for Gemini API requirements
        /// </summary>
        /// <param name="fileBytes">Image file content</param>
        /// <param name="fileName">Image file name</param>
        /// <param name="maxSizeBytes">Maximum allowed file size (default: 20MB)</param>
        /// <exception cref="ArgumentException">Thrown for invalid image files</exception>
        public static void ValidateImageFile(byte[] fileBytes, string fileName, int maxSizeBytes = 20 * 1024 * 1024)
        {
            if (fileBytes == null || fileBytes.Length == 0)
                throw new ArgumentException("Image file content cannot be null or empty");

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Image file name cannot be null or empty");

            if (fileBytes.Length > maxSizeBytes)
                throw new ArgumentException($"Image file size ({fileBytes.Length:N0} bytes) exceeds maximum allowed size ({maxSizeBytes:N0} bytes)");

            if (!IsImageFile(fileName))
                throw new ArgumentException($"File extension '{Path.GetExtension(fileName)}' is not a supported image format");

            if (!fileBytes.IsImageContent())
                throw new ArgumentException("File content does not appear to be a valid image");
        }

        #endregion

        #region File Size Utilities

        /// <summary>
        /// Converts bytes to human-readable string
        /// </summary>
        /// <param name="bytes">Number of bytes</param>
        /// <returns>Human-readable size string</returns>
        public static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:N1} {suffixes[counter]}";
        }

        /// <summary>
        /// Gets supported image file extensions
        /// </summary>
        /// <returns>Array of supported image extensions</returns>
        public static string[] GetSupportedImageExtensions()
        {
            return MimeTypeMappings
                .Where(kvp => kvp.Value.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .ToArray();
        }

        #endregion
    }
}