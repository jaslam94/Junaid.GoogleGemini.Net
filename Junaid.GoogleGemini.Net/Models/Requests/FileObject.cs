using Junaid.GoogleGemini.Net.Infrastructure.Utilities;

namespace Junaid.GoogleGemini.Net.Models.Requests
{
    /// <summary>
    /// A file payload (currently an image) with its bytes and file name. Immutable and validated
    /// at construction so it cannot represent an invalid file.
    /// </summary>
    public class FileObject
    {
        /// <summary>The raw file bytes.</summary>
        public byte[] FileContent { get; }

        /// <summary>The file name (used to infer the MIME type).</summary>
        public string FileName { get; }

        /// <summary>Creates a validated file object.</summary>
        /// <param name="fileContent">The file bytes; must not be null.</param>
        /// <param name="fileName">The file name; must not be null or whitespace.</param>
        public FileObject(byte[] fileContent, string fileName)
        {
            FileContent = fileContent ?? throw new ArgumentException("File content cannot be null or empty.", nameof(fileContent));
            FileName = !string.IsNullOrWhiteSpace(fileName)
                ? fileName
                : throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            ValidateImage();
        }

        private void ValidateImage()
        {
            try
            {
                FileUtilities.ValidateImageFile(FileContent, FileName);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException("Invalid image file.", ex);
            }
        }
    }
}
