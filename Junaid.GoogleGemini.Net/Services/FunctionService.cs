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
                _logger.LogInformation("Function {FunctionName} registered successfully", definition.Name);
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
                _logger.LogInformation("Calling function {FunctionName}", functionCall.Name);

                var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    functionCall.Arguments,
                    _jsonOptions);

                if (arguments == null)
                {
                    throw new JsonException("Failed to deserialize function arguments");
                }

                // Validate required parameters
                var missingParams = function.Definition.Parameters.Required
                    .Where(param => !arguments.ContainsKey(param))
                    .ToList();

                if (missingParams.Any())
                {
                    throw new ArgumentException(
                        $"Missing required parameters: {string.Join(", ", missingParams)}");
                }

                var result = await function.Handler(arguments);
                var response = JsonSerializer.Serialize(result, _jsonOptions);

                _logger.LogInformation("Function {FunctionName} executed successfully", functionCall.Name);

                return new FunctionResult
                {
                    Name = functionCall.Name,
                    Response = response
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing function {FunctionName}", functionCall.Name);
                return new FunctionResult
                {
                    Name = functionCall.Name,
                    Error = ex.Message
                };
            }
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
                _logger.LogInformation("Function {FunctionName} unregistered successfully", functionName);
                return true;
            }

            _logger.LogWarning("Function {FunctionName} not found for unregistration", functionName);
            return false;
        }
    }
}