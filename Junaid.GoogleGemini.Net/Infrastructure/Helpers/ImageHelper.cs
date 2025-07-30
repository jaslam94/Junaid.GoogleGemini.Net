using System.Text;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;

namespace Junaid.GoogleGemini.Net.Infrastructure.Helpers
{
    /// <summary>
    /// DEPRECATED: Use FileUtilities instead
    /// </summary>
    [Obsolete("This class is deprecated. Use FileUtilities.IsImageContent() instead. Will be removed in v7.0.0")]
    public static class ImageHelper
    {
        /// <summary>
        /// DEPRECATED: Use FileUtilities.IsImageContent() instead
        /// </summary>
        [Obsolete("Use FileUtilities.IsImageContent() instead")]
        public static bool IsImage(this byte[] fileBytes)
        {
            return FileUtilities.IsImageContent(fileBytes);
        }
    }
}