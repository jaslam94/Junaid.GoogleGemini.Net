using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Examples
{
    /// <summary>
    /// Examples demonstrating the unified utilities and configuration system
    /// </summary>
    public static class ConfigurationExamples
    {
        /// <summary>
        /// Modern configuration approach using utilities
        /// </summary>
        public static void ModernConfigurationExample()
        {
            // Using ConfigurationUtilities for easy setup
            var options = ConfigurationUtilities.CreateDefaultOptions("your-api-key");
            
            // Or production-optimized
            var prodOptions = ConfigurationUtilities.CreateProductionOptions("your-api-key");
            
            // Or development-optimized
            var devOptions = ConfigurationUtilities.CreateDevelopmentOptions("your-api-key");

            // Request options using constants
            var requestOptions = GeminiRequestOptions.Creative(GeminiConstants.Models.Gemini15Pro);
            var fastOptions = GeminiRequestOptions.Fast(); // Uses GeminiConstants.Models.Fastest
            var codeOptions = GeminiRequestOptions.Code(GeminiConstants.Models.Recommended);
        }

        /// <summary>
        /// Environment variable configuration with validation
        /// </summary>
        public static void EnvironmentVariableExample()
        {
            // Use ConfigurationUtilities for enhanced environment support
            var apiKey = ConfigurationUtilities.GetApiKeyFromEnvironment();
            
            if (ConfigurationUtilities.IsValidApiKeyFormat(apiKey))
            {
                var options = ConfigurationUtilities.CreateDefaultOptions(apiKey);
                Console.WriteLine("SUCCESS: Valid API key loaded from environment");
            }
            else
            {
                Console.WriteLine("ERROR: Invalid or missing API key in environment");
            }
        }

        /// <summary>
        /// Safety configuration examples
        /// </summary>
        public static void SafetyConfigurationExample()
        {
            // Using ConfigurationUtilities for safety settings
            var defaultSafety = ConfigurationUtilities.CreateSafetySettings(
                ConfigurationUtilities.GetDefaultSafetyThresholds());
            
            var strictSafety = ConfigurationUtilities.CreateStrictSafetySettings();
            var permissiveSafety = ConfigurationUtilities.CreatePermissiveSafetySettings();

            // Custom safety settings using constants
            var customSafety = new Dictionary<string, string>
            {
                { GeminiConstants.SafetyCategories.Harassment, GeminiConstants.SafetyThresholds.High },
                { GeminiConstants.SafetyCategories.HateSpeech, GeminiConstants.SafetyThresholds.Medium },
                { GeminiConstants.SafetyCategories.SexuallyExplicit, GeminiConstants.SafetyThresholds.Low }
            };
            var customSettings = ConfigurationUtilities.CreateSafetySettings(customSafety);
        }

        /// <summary>
        /// File validation examples using FileUtilities
        /// </summary>
        public static void FileValidationExample()
        {
            var imageBytes = File.ReadAllBytes("example.jpg");
            var fileName = "example.jpg";

            try
            {
                // Comprehensive file validation
                FileUtilities.ValidateImageFile(imageBytes, fileName);
                
                // Additional checks
                var mimeType = FileUtilities.GetMimeType(fileName);
                var isImage = FileUtilities.IsImageFile(fileName);
                var isValidContent = imageBytes.IsImageContent();
                var fileSize = FileUtilities.FormatFileSize(imageBytes.Length);
                
                Console.WriteLine($"SUCCESS: Valid image: {fileName}");
                Console.WriteLine($"   MIME Type: {mimeType}");
                Console.WriteLine($"   File Size: {fileSize}");
                Console.WriteLine($"   Content Valid: {isValidContent}");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"ERROR: Invalid image: {ex.Message}");
            }
        }

        /// <summary>
        /// Validation utilities examples
        /// </summary>
        public static void ValidationExample()
        {
            try
            {
                // Text validation
                ValidationUtilities.ValidateTextInput("Hello world", "prompt", GeminiConstants.Limits.MaxTextLength);
                
                // Model validation
                ValidationUtilities.ValidateModelName(GeminiConstants.Models.Recommended);
                
                // Multiple validations with summary
                var validationResults = new[]
                {
                    (ConfigurationUtilities.IsValidApiKeyFormat("AIzaSyC..."), "API Key Format"),
                    (FileUtilities.IsImageFile("test.jpg"), "Image File Extension"),
                    (true, "Mock validation passed")
                };
                
                var summary = ValidationUtilities.CreateValidationSummary(validationResults);
                
                Console.WriteLine(summary);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"ERROR: Validation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Configuration comparison showing the improvement
        /// </summary>
        public static void ConfigurationComparison()
        {
            // Before: Scattered helper classes and constants (REMOVED in v6.0.0)
            // After: Unified utility system
            // - FileUtilities (file handling, MIME types, validation)
            // - GeminiConstants (all constants in one place)
            // - ConfigurationUtilities (configuration management)
            // - ValidationUtilities (comprehensive validation)
            // - Consistent patterns and error handling

            Console.WriteLine("SUCCESS: Helper classes consolidated successfully!");
            Console.WriteLine("INFO: Reduced from 7+ helper classes to 4 utility classes");
            Console.WriteLine("INFO: Better organization and discoverability");
            Console.WriteLine("INFO: Enhanced validation and error handling");
            Console.WriteLine("INFO: Improved performance with optimized implementations");
            Console.WriteLine("INFO: Comprehensive documentation and examples");
        }

        /// <summary>
        /// Migration example for developers updating from old helpers (removed in v6.0.0)
        /// </summary>
        public static void MigrationExample()
        {
            Console.WriteLine("Migration Guide:");
            Console.WriteLine("================");
            
            Console.WriteLine("OLD (removed in v6.0.0):");
            Console.WriteLine("  MimeTypeHelper.GetMimeType(fileName)");
            Console.WriteLine("  image.IsImage()");
            Console.WriteLine("  SafetyCategory.Harassment");
            Console.WriteLine("  GeminiModels.Recommended");
            Console.WriteLine();
            
            Console.WriteLine("NEW (unified utilities):");
            Console.WriteLine("  FileUtilities.GetMimeType(fileName)");
            Console.WriteLine("  FileUtilities.IsImageContent(image)");
            Console.WriteLine("  GeminiConstants.SafetyCategories.Harassment");
            Console.WriteLine("  GeminiConstants.Models.Recommended");
            Console.WriteLine();
            
            Console.WriteLine("Additional utilities now available:");
            Console.WriteLine("  ConfigurationUtilities.CreateDefaultOptions()");
            Console.WriteLine("  ValidationUtilities.ValidateTextInput()");
            Console.WriteLine("  FileUtilities.ValidateImageFile()");
        }
    }
}