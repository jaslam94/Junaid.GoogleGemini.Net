using Junaid.GoogleGemini.Net.Infrastructure.Utilities;

namespace Junaid.GoogleGemini.Net.Infrastructure.Helpers
{
    /// <summary>
    /// DEPRECATED: Use FileUtilities instead
    /// </summary>
    [Obsolete("This class is deprecated. Use FileUtilities.GetMimeType() instead. Will be removed in v7.0.0")]
    public class MimeTypeHelper
    {
        private static readonly Dictionary<string, string> MimeTypeMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".bmp", "image/bmp" },
            { ".gif", "image/gif" },
            { ".jpeg", "image/jpeg" },
            { ".jpg", "image/jpeg" },
            { ".png", "image/png" },
            { ".tiff", "image/tiff" },
            { ".tif", "image/tiff" },
        };

        /// <summary>
        /// DEPRECATED: Use FileUtilities.GetMimeType() instead
        /// </summary>
        [Obsolete("Use FileUtilities.GetMimeType() instead")]
        public static string GetMimeType(string fileName)
        {
            return FileUtilities.GetMimeType(fileName);
        }
    }
}