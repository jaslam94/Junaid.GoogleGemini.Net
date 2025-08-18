using Junaid.GoogleGemini.Net.Models.Functions;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Implementation of the function service for managing Gemini function calls
    /// </summary>
    public class FunctionService : IFunctionService
    {
        private readonly ConcurrentDictionary<string, (FunctionDefinition Definition, Func<Dictionary<string, object>, Task<object>> Handler)> _functions = new();
        private readonly ILogger<FunctionService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public FunctionService(ILogger<FunctionService> logger)
        {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
        }

        /// <inheritdoc/>
        public void RegisterFunction(FunctionDefinition definition, Func<Dictionary<string, object>, Task<object>> handler)
        {
            if (string.IsNullOrEmpty(definition.Name))
            {
                throw new ArgumentException("Function name cannot be empty", nameof(definition));
            }

            if (_functions.TryAdd(definition.Name, (definition, handler)))
            {
                _logger.LogDebug("Function {FunctionName} registered", definition.Name);
            }
            else
            {
                _logger.LogWarning("Function {FunctionName} already exists", definition.Name);
                throw new InvalidOperationException($"Function {definition.Name} is already registered");
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<FunctionDefinition> GetAvailableFunctions()
        {
            return _functions.Values.Select(f => f.Definition).ToList().AsReadOnly();
        }

        /// <inheritdoc/>
        public async Task<FunctionResult> CallFunctionAsync(FunctionCall functionCall)
        {
            if (!_functions.TryGetValue(functionCall.Name, out var function))
            {
                _logger.LogError("Function {FunctionName} not found", functionCall.Name);
                return new FunctionResult
                {
                    Name = functionCall.Name,
                    Error = $"Function {functionCall.Name} not found"
                };
            }

            try
            {
                // Use JsonDocument for better handling of JsonElement objects
                using var doc = JsonDocument.Parse(functionCall.Arguments);
                var arguments = ConvertJsonElementToObject(doc.RootElement);

                if (arguments is not Dictionary<string, object> argDict)
                {
                    throw new JsonException("Function arguments must be a JSON object");
                }

                // Validate required parameters
                var missingParams = function.Definition.Parameters.Required
                    .Where(param => !argDict.ContainsKey(param))
                    .ToList();

                if (missingParams.Any())
                {
                    throw new ArgumentException(
                        $"Missing required parameters: {string.Join(", ", missingParams)}");
                }

                var result = await function.Handler(argDict);
                var response = JsonSerializer.Serialize(result, _jsonOptions);

                return new FunctionResult
                {
                    Name = functionCall.Name,
                    Response = response
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Function {FunctionName} execution failed", functionCall.Name);
                return new FunctionResult
                {
                    Name = functionCall.Name,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Converts JsonElement to appropriate .NET objects
        /// </summary>
        private static object ConvertJsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject()
                    .ToDictionary(prop => prop.Name, prop => ConvertJsonElementToObject(prop.Value)),
                JsonValueKind.Array => element.EnumerateArray()
                    .Select(ConvertJsonElementToObject).ToArray(),
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                _ => element.ToString()
            };
        }

        /// <inheritdoc/>
        public bool HasFunction(string functionName)
        {
            return _functions.ContainsKey(functionName);
        }

        /// <inheritdoc/>
        public FunctionDefinition? GetFunctionDefinition(string functionName)
        {
            return _functions.TryGetValue(functionName, out var function) ? function.Definition : null;
        }

        /// <inheritdoc/>
        public bool UnregisterFunction(string functionName)
        {
            if (_functions.TryRemove(functionName, out _))
            {
                _logger.LogDebug("Function {FunctionName} unregistered", functionName);
                return true;
            }

            return false;
        }
    }
}